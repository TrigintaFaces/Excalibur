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
/// Liveness lock: a drain run must deliver every message in its budget, however large the budget is.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect.</b> The producer's in-flight guard was a bounded <c>Channel</c> that nothing ever read,
/// constructed at <c>Init</c> with capacity <c>QueueCapacity + ProducerBatchSize</c>. Every distinct id
/// written to it stayed there, so once that many had accrued the producer's write blocked with no
/// completion path. The stall is total rather than partial: the drain awaits the producer before the
/// consumer, and the channel's <c>Complete()</c> sits in the producer's <c>finally</c>, which never runs
/// — so the drain call does not return at all.
/// </para>
/// <para>
/// <b>Every shipped configuration had bound &lt; PerRunTotal</b>, so every one of them could reach it:
/// the DI preset at 5,100 against 10,000, the raw defaults at 1,100 against 1,000, and the tuning
/// presets below those. The rows below bracket that: one small and fast, one at the shipped DI preset's
/// own capacity so the actual shipped numbers are covered rather than argued about.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> Each row's message count exceeds that row's old bound, which is what makes it
/// RED-by-construction on the pre-fix code — and only the count exceeding the bound does, which is why
/// <see cref="ExceedTheOldBoundThatEachRowIsSizedAgainst"/> asserts that relationship directly. A run
/// sized at or below the bound passes on the broken code and proves nothing.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxDrainQueueBoundShould
{
	// queueCapacity, producerBatchSize, messageCount. Old bound was queueCapacity + producerBatchSize.
	public static TheoryData<int, int, int> Budgets => new()
	{
		{ 200, 50, 400 },      // small and fast: old bound 250
		{ 5_000, 100, 5_200 }, // the shipped DI preset's capacity: old bound 5,100
	};

	[Theory]
	[MemberData(nameof(Budgets))]
	public void ExceedTheOldBoundThatEachRowIsSizedAgainst(int queueCapacity, int producerBatchSize, int messageCount)
	{
		// Positive control for the arm below. A row sized at or under its own bound is green on the broken
		// code, so the assertion there would prove nothing.
		var oldBound = queueCapacity + producerBatchSize;
		messageCount.ShouldBeGreaterThan(
			oldBound,
			"the row must push past the bound it is sized against, or it cannot detect the stall");
	}

	[Theory]
	[MemberData(nameof(Budgets))]
	public async Task DeliverEveryMessageInTheBudget(int queueCapacity, int producerBatchSize, int messageCount)
	{
		await using var harness = await BulkDrainHarness
			.CreateAsync(queueCapacity, producerBatchSize, messageCount)
			.ConfigureAwait(false);

		// Pre-fix this call never returns: the producer blocks on the bounded channel and the consumer
		// never sees Complete(). The token bounds the damage so a regression fails rather than hangs.
		using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
		_ = await harness.Processor.DispatchPendingMessagesAsync(cts.Token).ConfigureAwait(false);

		var processed = await harness.CountProcessedAsync().ConfigureAwait(false);
		processed.ShouldBe(
			messageCount,
			$"every message in a {messageCount} budget must drain; the producer's in-flight guard must not "
			+ $"carry a bound (the old one was {queueCapacity + producerBatchSize})");
	}

	private sealed class BulkDrainHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;
		private readonly int _messageCount;

		private BulkDrainHarness(ServiceProvider services, IInboxStore store, InboxProcessor processor, int messageCount)
		{
			_services = services;
			Store = store;
			Processor = processor;
			_messageCount = messageCount;
		}

		public IInboxStore Store { get; }

		public InboxProcessor Processor { get; }

		private static string HandlerTypeName =>
			typeof(BulkProbeMessage).FullName ?? typeof(BulkProbeMessage).Name;

		public static async Task<BulkDrainHarness> CreateAsync(int queueCapacity, int producerBatchSize, int messageCount)
		{
			MessageTypeRegistry.RegisterType<BulkProbeMessage>();

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();
			_ = services.AddScoped(_ => A.Fake<IDispatcher>());
			var provider = services.BuildServiceProvider();

			var store = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			for (var i = 0; i < messageCount; i++)
			{
				var id = MessageId(i);
				var payload = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new BulkProbeMessage(id)));

				_ = await store.CreateEntryAsync(
					id,
					HandlerTypeName,
					typeof(BulkProbeMessage).Name,
					payload,
					new Dictionary<string, object>(StringComparer.Ordinal),
					CancellationToken.None).ConfigureAwait(false);

				// Put each row into the state the re-admission drain selects.
				await store.MarkFailedAsync(id, HandlerTypeName, "seeded", CancellationToken.None).ConfigureAwait(false);
				var seeded = await store.GetEntryAsync(id, HandlerTypeName, CancellationToken.None).ConfigureAwait(false)
					?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
				seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);
			}

			// Every message is recognised as an already-handled duplicate: the success path that does not
			// depend on payload deserialization, so this isolates the producer's in-flight guard.
			var deduplicationStore = A.Fake<IDeduplicationStore>();
			_ = A.CallTo(() => deduplicationStore.ContainsAsync(A<string>._, A<CancellationToken>._)).Returns(true);

			var processor = new InboxProcessor(
				Options.Create(new DeliveryInboxOptions
				{
					Capacity =
					{
						QueueCapacity = queueCapacity,
						ProducerBatchSize = producerBatchSize,
						ConsumerBatchSize = producerBatchSize,
						PerRunTotal = messageCount,
						ParallelProcessingDegree = 4,
					},
					MaxAttempts = 3,
					BatchTuning = { EnableBatchDatabaseOperations = false },
				}),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance,
				deduplicationStore: deduplicationStore);

			processor.Init("dispatcher-queue-bound");

			return new BulkDrainHarness(provider, store, processor, messageCount);
		}

		public async Task<int> CountProcessedAsync()
		{
			var processed = 0;
			for (var i = 0; i < _messageCount; i++)
			{
				var entry = await Store.GetEntryAsync(MessageId(i), HandlerTypeName, CancellationToken.None)
					.ConfigureAwait(false);
				if (entry?.Status == InboxStatus.Processed)
				{
					processed++;
				}
			}

			return processed;
		}

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		private static string MessageId(int index) => $"queue-bound-{index}";
	}

	private sealed record BulkProbeMessage(string Id) : IDispatchEvent;
}
