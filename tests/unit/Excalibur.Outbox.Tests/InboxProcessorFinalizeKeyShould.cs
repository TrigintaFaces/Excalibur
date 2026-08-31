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
/// Regression lock: the inbox drain must finalize an entry with the composite key the STORE holds it
/// under — <c>(InboxEntry.MessageId, InboxEntry.HandlerType)</c> — not with a key re-derived from the
/// converted message.
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect.</b> The drain collected <c>(message.ExternalMessageId, message.MessageType)</c> into a
/// tuple whose second field is named <c>HandlerType</c>, and handed that to <c>MarkProcessedAsync</c> /
/// <c>MarkFailedAsync</c>. Both production writers persist <c>HandlerType</c> as the message type's
/// <b>fully qualified</b> name and <c>MessageType</c> as its <b>short</b> name, so for any namespaced
/// message the two columns hold different strings and the finalize addressed a row that does not exist:
/// a successfully re-dispatched entry was never marked processed, stayed <see cref="InboxStatus.Failed"/>,
/// and was re-admitted — running the handler again on every drain until it dead-lettered.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> These arms drive the real <see cref="InboxProcessor"/> against a real inbox store
/// obtained through the supported registration, so the assertion is on the entry's persisted state rather
/// than on an argument captured from a mock that accepts any key. Pre-fix the store reports the row
/// missing and the drain never finalizes it ⇒ RED; post-fix the entry reaches its terminal state ⇒ GREEN.
/// <see cref="UseANamespacedProbeType_SoTheTwoKeyColumnsActuallyDiffer"/> is the positive control: a probe
/// type in the global namespace has <c>FullName == Name</c>, which dissolves the mismatch and would let
/// every arm pass without exercising it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "1")]
public sealed class InboxProcessorFinalizeKeyShould
{
	private const string ProbeMessageId = "inbox-finalize-key";

	// Mirrors both production writers exactly: InboxMiddleware and MessageInbox each persist the fully
	// qualified name as HandlerType and the short name as MessageType.
	private static readonly string HandlerTypeName =
		typeof(FinalizeKeyProbeMessage).FullName ?? typeof(FinalizeKeyProbeMessage).Name;

	private static readonly string MessageTypeName = typeof(FinalizeKeyProbeMessage).Name;

	[Fact]
	public void UseANamespacedProbeType_SoTheTwoKeyColumnsActuallyDiffer()
	{
		// Positive control for every arm below. The two key columns only diverge for a namespaced type; a
		// global-namespace probe would make the arms pass while exercising nothing.
		HandlerTypeName.ShouldNotBe(
			MessageTypeName,
			"the probe must be namespaced — otherwise FullName == Name and the mismatch under test cannot occur");
	}

	[Fact]
	public async Task MarkASucceededEntryProcessed_SoItIsNotReAdmittedAndRunAgain()
	{
		// A re-admitted entry that succeeds — here recognised as an already-handled duplicate, the one
		// success path that does not depend on payload deserialization — must reach Processed.
		await using var harness = await InboxDrainHarness.CreateAsync(maxAttempts: 3, dispatchIsDuplicate: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a succeeded entry must be finalized under the key the store holds it by; left Failed it is "
			+ "re-admitted on the next drain and the handler runs again");
	}

	[Fact]
	public async Task DeserializeThePayloadAndDispatchIt_WhenTheDrainActuallyReachesTheHandler()
	{
		// The arms either side of this one never reach the deserializer: the duplicate arm short-circuits
		// before dispatch, and the failure arms are satisfied by a dispatcher that fails for its own reasons.
		// This arm drives the whole path -- a real store, a real re-admitted entry holding real UTF-8 JSON,
		// and a dispatcher that succeeds -- so the stored payload must actually deserialize and be handed to
		// the handler before the entry can be finalized.
		await using var harness = await InboxDrainHarness
			.CreateAsync(maxAttempts: 3, dispatchIsDuplicate: false, dispatchSucceeds: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		// Without this the arm would pass on any path that finalizes the entry without dispatching it -- which
		// is exactly how a payload that never deserialized stayed invisible behind a green suite.
		A.CallTo(() => harness.Dispatcher.DispatchAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"a dispatched entry must reach its terminal state; left Failed it is re-admitted and the handler "
			+ "runs again on every drain until it dead-letters");
	}

	[Fact]
	public async Task RecordARetryAgainstTheEntry_WhenDispatchFailsBelowTheDeadLetterCeiling()
	{
		await using var harness = await InboxDrainHarness.CreateAsync(maxAttempts: 3, dispatchIsDuplicate: false)
			.ConfigureAwait(false);

		var before = await harness.GetEntryAsync().ConfigureAwait(false);
		before.ShouldNotBeNull();
		var retriesBefore = before.RetryCount;

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.RetryCount.ShouldBe(
			retriesBefore + 1,
			"the retry mark must land on the entry's own row; against a key that matches nothing the attempt "
			+ "is never counted and the entry can never reach the dead-letter ceiling");
	}

	[Fact]
	public async Task RecordTheDeadLetterOutcomeAgainstTheEntry_WhenDispatchFailsAtTheCeiling()
	{
		// MaxAttempts 2: the seeded entry carries one attempt, so it is still re-admitted (RetryCount <
		// MaxAttempts) and the drain's next attempt reaches the dead-letter branch.
		await using var harness = await InboxDrainHarness.CreateAsync(maxAttempts: 2, dispatchIsDuplicate: false)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var entry = await harness.GetEntryAsync().ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.LastError.ShouldNotBeNull();
		entry.LastError.ShouldContain(
			"Max retries exceeded",
			Case.Sensitive,
			"the dead-letter mark must land on the entry's own row, or the entry is left looking retryable");
	}

	/// <summary>
	/// Drives the real <see cref="InboxProcessor"/> over a real inbox store holding one seeded,
	/// re-admittable entry keyed exactly the way the production writers key it.
	/// </summary>
	private sealed class InboxDrainHarness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;

		private InboxDrainHarness(
			ServiceProvider services,
			IInboxStore store,
			InboxProcessor processor,
			IDispatcher dispatcher)
		{
			_services = services;
			Store = store;
			Processor = processor;
			Dispatcher = dispatcher;
		}

		public IInboxStore Store { get; }

		public InboxProcessor Processor { get; }

		public IDispatcher Dispatcher { get; }

		public static async Task<InboxDrainHarness> CreateAsync(
			int maxAttempts,
			bool dispatchIsDuplicate,
			bool dispatchSucceeds = false)
		{
			MessageTypeRegistry.RegisterType<FinalizeKeyProbeMessage>();

			// One shared dispatcher instance rather than a per-scope fake, so an arm can assert whether the
			// drain actually reached the handler. A per-scope fake is unobservable from the test.
			var dispatcher = A.Fake<IDispatcher>();
			if (dispatchSucceeds)
			{
				_ = A.CallTo(() => dispatcher.DispatchAsync(
						A<IDispatchMessage>._,
						A<IMessageContext>._,
						A<CancellationToken>._))
					.Returns(Task.FromResult<IMessageResult>(MessageResult.Success()));
			}

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();
			_ = services.AddScoped(_ => dispatcher);
			var provider = services.BuildServiceProvider();

			// AddInMemoryInboxStore registers the contract under the provider key, not unkeyed.
			var store = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			var payload = Encoding.UTF8.GetBytes(
				JsonSerializer.Serialize(new FinalizeKeyProbeMessage(ProbeMessageId)));

			_ = await store.CreateEntryAsync(
				ProbeMessageId,
				HandlerTypeName,
				MessageTypeName,
				payload,
				new Dictionary<string, object>(StringComparer.Ordinal),
				CancellationToken.None).ConfigureAwait(false);

			// Put the entry into the state the re-admission drain selects: Failed, one attempt spent, and
			// last attempted long enough ago to clear the drain's always-on re-admission floor.
			await store.MarkFailedAsync(
				ProbeMessageId, HandlerTypeName, "seeded failure", CancellationToken.None).ConfigureAwait(false);

			var seeded = await store.GetEntryAsync(ProbeMessageId, HandlerTypeName, CancellationToken.None)
				.ConfigureAwait(false)
				?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
			seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);

			var deduplicationStore = A.Fake<IDeduplicationStore>();
			_ = A.CallTo(() => deduplicationStore.ContainsAsync(A<string>._, A<CancellationToken>._))
				.Returns(dispatchIsDuplicate);

			var processor = new InboxProcessor(
				CreateOptions(maxAttempts),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance,
				deduplicationStore: deduplicationStore);

			processor.Init("dispatcher-finalize-key");

			return new InboxDrainHarness(provider, store, processor, dispatcher);
		}

		public ValueTask<InboxEntry?> GetEntryAsync() =>
			Store.GetEntryAsync(ProbeMessageId, HandlerTypeName, CancellationToken.None);

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		private static IOptions<DeliveryInboxOptions> CreateOptions(int maxAttempts) =>
			Options.Create(new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 1,
					ProducerBatchSize = 1,
					ConsumerBatchSize = 1,
					PerRunTotal = 1,
					ParallelProcessingDegree = 2,
				},
				MaxAttempts = maxAttempts,
				BatchTuning =
				{
					EnableBatchDatabaseOperations = false,
				},
			});
	}

	/// <summary>A namespaced probe message: its <c>FullName</c> and <c>Name</c> differ.</summary>
	private sealed record FinalizeKeyProbeMessage(string Id) : IDispatchEvent;
}
