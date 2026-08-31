// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Regression lock: one drain run must queue <b>every</b> entry a message has, not just the first. The
/// unit of work is the composite <c>(InboxEntry.MessageId, InboxEntry.HandlerType)</c> — the pair the
/// work item carries and every completion path marks — so a message handled by several handlers holds
/// several rows that must all be drained.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect.</b> The producer's in-flight guard was keyed on <c>MessageId</c> alone. The first entry for
/// a message claimed the id, and every sibling entry for the same message under a different
/// <c>HandlerType</c> was silently skipped for the rest of the run. Against a store that re-offers a
/// skipped row this is worse than a deferral: the producer keeps reserving it, keeps skipping it, and
/// the batch is never empty, so the loop has no exit and the drain call never returns — no other
/// message in that store is processed again either. Measured against the in-memory store; whether a
/// given store re-offers is what decides spin versus deferral.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> This drives the real <see cref="InboxProcessor"/> against a real inbox store
/// through the supported registration, and asserts the persisted state of both rows rather than an
/// argument captured from a mock. Pre-fix the second entry is never queued, stays
/// <see cref="InboxStatus.Failed"/> ⇒ RED; post-fix both reach their terminal state ⇒ GREEN.
/// <see cref="SeedTwoDistinctRowsForOneMessage_SoThereIsASiblingToSkip"/> is the positive control: if the
/// store collapsed the two entries into one row there would be no sibling and the assertion would pass
/// while exercising nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "1")]
public sealed class InboxProcessorCompositeInFlightKeyShould
{
	private const string SharedMessageId = "inbox-composite-in-flight";

	// One message, two handlers — the shape the composite key exists for. Both rows carry the same
	// MessageType (the registry lookup key); only HandlerType differs, exactly as the production writers
	// persist it.
	private static readonly string FirstHandler =
		typeof(CompositeKeyProbeMessage).FullName ?? typeof(CompositeKeyProbeMessage).Name;

	private static readonly string SecondHandler = FirstHandler + "+SecondHandler";

	private static readonly string MessageTypeName = typeof(CompositeKeyProbeMessage).Name;

	[Fact]
	public async Task SeedTwoDistinctRowsForOneMessage_SoThereIsASiblingToSkip()
	{
		// Positive control for the arm below. Both rows must exist and be re-admittable before the drain,
		// or "the second one was drained too" is satisfied by a store that never held a second one.
		await using var harness = await TwoHandlerDrainHarness.CreateAsync().ConfigureAwait(false);

		var first = await harness.GetEntryAsync(FirstHandler).ConfigureAwait(false);
		var second = await harness.GetEntryAsync(SecondHandler).ConfigureAwait(false);

		FirstHandler.ShouldNotBe(SecondHandler);
		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		first.Status.ShouldBe(InboxStatus.Failed);
		second.Status.ShouldBe(InboxStatus.Failed, "both rows must start re-admittable for the drain to reach them");
	}

	[Fact]
	public async Task DrainEveryHandlerEntryForOneMessageInASingleRun()
	{
		await using var harness = await TwoHandlerDrainHarness.CreateAsync().ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var first = await harness.GetEntryAsync(FirstHandler).ConfigureAwait(false);
		var second = await harness.GetEntryAsync(SecondHandler).ConfigureAwait(false);

		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		first.Status.ShouldBe(InboxStatus.Processed);
		second.Status.ShouldBe(
			InboxStatus.Processed,
			"every (MessageId, HandlerType) entry must be queued in one run; keyed on MessageId alone the "
			+ "first entry claims the id and its siblings are skipped until a later drain");
	}

	/// <summary>
	/// Drives the real <see cref="InboxProcessor"/> over a real inbox store holding two seeded,
	/// re-admittable entries for a single message id under two different handler types.
	/// </summary>
	private sealed class TwoHandlerDrainHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;

		private TwoHandlerDrainHarness(ServiceProvider services, IInboxStore store, InboxProcessor processor)
		{
			_services = services;
			Store = store;
			Processor = processor;
		}

		public IInboxStore Store { get; }

		public InboxProcessor Processor { get; }

		public static async Task<TwoHandlerDrainHarness> CreateAsync()
		{
			MessageTypeRegistry.RegisterType<CompositeKeyProbeMessage>();

			var dispatcher = A.Fake<IDispatcher>();

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();
			_ = services.AddScoped(_ => dispatcher);
			var provider = services.BuildServiceProvider();

			// AddInMemoryInboxStore registers the contract under the provider key, not unkeyed.
			var store = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			var payload = Encoding.UTF8.GetBytes(
				JsonSerializer.Serialize(new CompositeKeyProbeMessage(SharedMessageId)));

			foreach (var handler in new[] { FirstHandler, SecondHandler })
			{
				_ = await store.CreateEntryAsync(
					SharedMessageId,
					handler,
					MessageTypeName,
					payload,
					new Dictionary<string, object>(StringComparer.Ordinal),
					CancellationToken.None).ConfigureAwait(false);

				// Put each row into the state the re-admission drain selects: Failed, one attempt spent, and
				// last attempted long enough ago to clear the drain's always-on re-admission floor.
				await store.MarkFailedAsync(SharedMessageId, handler, "seeded failure", CancellationToken.None)
					.ConfigureAwait(false);

				var seeded = await store.GetEntryAsync(SharedMessageId, handler, CancellationToken.None)
					.ConfigureAwait(false)
					?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
				seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);
			}

			// Both entries are recognised as already-handled duplicates: the one success path that does not
			// depend on payload deserialization, so this arm isolates the producer's in-flight key.
			var deduplicationStore = A.Fake<IDeduplicationStore>();
			_ = A.CallTo(() => deduplicationStore.ContainsAsync(A<string>._, A<CancellationToken>._))
				.Returns(true);

			var processor = new InboxProcessor(
				CreateOptions(),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance,
				deduplicationStore: deduplicationStore);

			processor.Init("dispatcher-composite-in-flight");

			return new TwoHandlerDrainHarness(provider, store, processor);
		}

		public ValueTask<InboxEntry?> GetEntryAsync(string handlerType) =>
			Store.GetEntryAsync(SharedMessageId, handlerType, CancellationToken.None);

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		// Room for both entries in a single run — the whole point of the arm. A PerRunTotal of 1 would end
		// the run after the first entry and hide the skip.
		private static IOptions<DeliveryInboxOptions> CreateOptions() =>
			Options.Create(new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 8,
					ProducerBatchSize = 8,
					ConsumerBatchSize = 8,
					PerRunTotal = 8,
					ParallelProcessingDegree = 2,
				},
				MaxAttempts = 3,
				BatchTuning =
				{
					EnableBatchDatabaseOperations = false,
				},
			});
	}

	/// <summary>A probe message standing in for one handled by more than one handler.</summary>
	private sealed record CompositeKeyProbeMessage(string Id) : IDispatchEvent;
}
