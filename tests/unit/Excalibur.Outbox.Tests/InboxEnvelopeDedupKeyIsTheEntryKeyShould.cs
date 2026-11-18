// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Text;
using System.Text.Json;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Middleware.Inbox;
using Excalibur.Dispatch.Options.Configuration;

using DeliveryInboxOptions = Excalibur.Dispatch.Options.Delivery.InboxOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// The identity the retry drain seeds into the dispatch context must be the key the STORE holds the entry
/// under, on BOTH of the drain's conversion paths — the legacy path and the binary-envelope path.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why the two values must agree.</b> The drain holds a lease keyed on the entry, puts that key in the
/// lease claim it presents, and separately seeds the context identity from the converted message. The
/// inbox stage resolves an identity from the context and compares it against the claim to decide whether
/// the caller is the claim holder or a competitor. Those are two values produced in two different methods
/// over one logical identity, so nothing structural keeps them in step — only an arm that binds their
/// agreement does.
/// </para>
/// <para>
/// <b>Defect.</b> The envelope branch of the conversion set the identity from the deserialized envelope's
/// own producer-minted <c>Guid</c> instead of the entry key. The legacy branch has always used the entry
/// key. So the two branches disagreed about the identity of the same entry, and the stage compared the
/// claim against a value no store row is keyed by.
/// </para>
/// <para>
/// <b>Why this binds a fourth path and not just the envelope branch.</b> The invariant arm asserts the
/// AGREEMENT of the two values rather than the provenance of either, as a theory over both conversion
/// paths. A branch added later that derives the identity from anywhere other than the entry key fails it
/// without the arm being touched.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The arms drive the real <see cref="InboxProcessor"/> over a real in-memory inbox
/// store — the store's own lease refusal is the mechanism under test, and a mock returns what it was told
/// — and the real <see cref="InboxMiddleware"/> over the same store, reached through the dispatcher the
/// drain resolves. <see cref="DecodeTheEnvelope_SoTheEnvelopeBranchIsTheOneUnderTest"/> is the required
/// positive control: the envelope branch is selected by the payload's leading format marker, so a payload
/// framed wrongly would silently exercise the legacy branch and every envelope arm would pass having
/// tested nothing. <see cref="RunTheHandler_OnTheLegacyPath"/> is the liveness control for the
/// handler-ran arm: a harness that never reaches a handler on either path satisfies no claim about which
/// path reached one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "1")]
public sealed class InboxEnvelopeDedupKeyIsTheEntryKeyShould
{
	private const string EntryKey = "inbox-envelope-dedup-key";

	/// <summary>
	/// The envelope's own identity. Fixed rather than random so an arm can ask the store whether a row was
	/// created under it — which is what the mismatch produces on a store that has no such row yet.
	/// </summary>
	private static readonly Guid EnvelopeOwnId = new("11111111-2222-3333-4444-555555555555");

	// Mirrors both production writers: the fully qualified name is persisted as HandlerType, the short
	// name as MessageType.
	private static readonly string HandlerTypeName =
		typeof(DedupKeyProbeMessage).FullName ?? typeof(DedupKeyProbeMessage).Name;

	private static readonly string MessageTypeName = typeof(DedupKeyProbeMessage).Name;

	/// <summary>
	/// THE INVARIANT. The identity seeded into the context is the one placed in the lease claim, on both
	/// conversion paths.
	/// </summary>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task SeedTheContextWithTheKeyItClaimed(bool throughTheEnvelopeBranch)
	{
		await using var harness = await Harness.CreateAsync(throughTheEnvelopeBranch).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var context = harness.DispatchedContext;
		context.ShouldNotBeNull(
			"liveness first: an arm whose drain never dispatched would satisfy any claim about what the "
			+ "dispatch carried");

		var claim = context.GetInboxLeaseFeature();
		claim.ShouldNotBeNull("the drain must present the claim it holds, or the stage cannot recognise it");

		context.MessageId.ShouldBe(
			claim.MessageId,
			"the stage resolves an identity from the context and compares it, ordinally, against the claim "
			+ "the drain presents. Two values that disagree make the claim holder look like a competitor on "
			+ "its own entry");

		claim.MessageId.ShouldBe(
			EntryKey,
			"and the value both carry must be the key the store is keyed by — agreement on a third value "
			+ "would satisfy the arm above while addressing a row that does not exist");
	}

	/// <summary>
	/// THE REGRESSION ARM. The handler runs on the binary-envelope path, and the entry reaches its
	/// terminal state.
	/// </summary>
	[Fact]
	public async Task RunTheHandler_OnTheBinaryEnvelopePath()
	{
		await using var harness = await Harness.CreateAsync(throughTheEnvelopeBranch: true).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.HandlerInvocations.ShouldBe(
			1,
			"the handler is the work. A drain that reports a completion without reaching it has lost the "
			+ "message while looking successful — which is why this assertion, not the entry's status, is "
			+ "the one that must hold");

		var entry = await harness.GetEntryAsync(EntryKey).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"and the entry must be finalized under its own key; left non-terminal it is re-admitted on "
			+ "every drain until it dead-letters");
	}

	/// <summary>
	/// THE REGRESSION ARM THAT SEES THE SUPPRESSION. The handler runs on the envelope path even when the
	/// store already holds a terminal row under the envelope's own identity.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why the arm above is not sufficient, measured rather than reasoned.</b> With the entry key
	/// discarded, the stage resolves the envelope's producer-minted identity, finds the store has no row
	/// under it, is GRANTED a fresh lease, and runs the handler against a phantom entry — so the
	/// handler-ran arm above stays GREEN against the defect. The suppression the stage's own comment
	/// describes needs the resolved identity to name a row that is NOT claimable.
	/// </para>
	/// <para>
	/// <b>And that row is the normal state, not a contrivance.</b> The receive path resolves a delivery
	/// carrying a binary envelope to the envelope's identity, so a row under the producer's id is what a
	/// first delivery leaves behind; the phantom created by the arm above leaves one too. A retry drain of
	/// the same message then resolves that same identity and finds it terminal.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task RunTheHandler_WhenTheStoreAlreadyHoldsARowUnderTheEnvelopeOwnIdentity()
	{
		await using var harness = await Harness
			.CreateAsync(throughTheEnvelopeBranch: true, withTerminalRowUnderEnvelopeOwnId: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.HandlerInvocations.ShouldBe(
			1,
			"the drain holds a live lease on THIS entry, so the stage's refusal is the drain's own term "
			+ "coming back at it. Resolving an identity the drain never claimed makes the holder look like a "
			+ "competitor, and the handler is skipped on a dispatch that reports success");

		var entry = await harness.GetEntryAsync(EntryKey).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"and the entry must still reach its terminal state: a refused dispatch leaves it non-terminal "
			+ "and it is re-admitted, suppressed and refused again on every subsequent drain");
	}

	/// <summary>
	/// THE ARM THAT OBSERVES THE STUCK ENTRY instead of reading it from the drain's source: two drains,
	/// one handler invocation, and the entry terminal rather than re-admitted.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This closes a measurement gap that was recorded rather than hidden.</b> The arm above asserts the
	/// entry's status too, but that assertion sits AFTER its handler assertion, so a run in which the
	/// handler is skipped never reaches it: the consequence for the entry was argued from the drain's
	/// refusal path rather than seen. Here the entry's fate is the only subject, so it is observed.
	/// </para>
	/// <para>
	/// <b>One drain, not two, and the reason is measured rather than chosen.</b> When the resolved identity
	/// names an unclaimable row the stage declines and the drain returns BEFORE finalizing — deliberately,
	/// since finalizing an invocation that never happened is the worse of the two failures. Observing a
	/// SECOND pass would be the most direct evidence of re-admission, and this harness cannot provide it:
	/// draining twice throws <c>ChannelClosedException</c>, because the processor's channel is completed by
	/// the first pass. So the two durable consequences are asserted instead of the repetition itself.
	/// </para>
	/// <para>
	/// <b>Both assertions are load-bearing and they catch different halves.</b> The status is what the
	/// re-admission query selects on, so a non-terminal entry IS a re-admitted one. The dedup record is
	/// what stops a later redelivery of the same message from being handled again; the drain writes it
	/// immediately after the point it returns from on a refusal, so its absence is the refusal's signature
	/// and it distinguishes "finalized" from "finalized and recorded".
	/// </para>
	/// </remarks>
	[Fact]
	public async Task CompleteTheEntry_RatherThanReadmitItOnEveryDrain()
	{
		await using var harness = await Harness
			.CreateAsync(throughTheEnvelopeBranch: true, withTerminalRowUnderEnvelopeOwnId: true)
			.ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var entry = await harness.GetEntryAsync(EntryKey).ConfigureAwait(false);
		entry.ShouldNotBeNull();
		entry.Status.ShouldBe(
			InboxStatus.Processed,
			"the drain must finalize the entry under its own key. The entry is seeded Failed with an "
			+ "attempt spent, which is precisely what the re-admission query selects, so an entry still "
			+ "Failed here is not merely unfinalized -- it is selected again on every subsequent drain, "
			+ "forever. That is the stuck state, observed rather than argued from the refusal path");

		(await harness.IsProcessedAsync(EntryKey).ConfigureAwait(false)).ShouldBeTrue(
			"and the identity must be recorded as handled. The drain writes that record immediately after "
			+ "the statement it returns from when the stage refuses, so its absence is the refusal's "
			+ "signature and would leave a later redelivery of this message free to run the handler again");
	}

	/// <summary>
	/// LIVENESS CONTROL for the arms above: the legacy path reaches a handler too, so a green envelope arm
	/// is not merely a harness that always reaches one.
	/// </summary>
	[Fact]
	public async Task RunTheHandler_OnTheLegacyPath()
	{
		await using var harness = await Harness.CreateAsync(throughTheEnvelopeBranch: false).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.HandlerInvocations.ShouldBe(
			1, "the legacy path has always carried the entry key and must still reach the handler");
	}

	/// <summary>
	/// POSITIVE CONTROL. The envelope branch is selected by the payload's leading format marker, so this
	/// arm proves the envelope arms exercise that branch rather than falling through to the legacy one.
	/// </summary>
	[Fact]
	public async Task DecodeTheEnvelope_SoTheEnvelopeBranchIsTheOneUnderTest()
	{
		await using var harness = await Harness.CreateAsync(throughTheEnvelopeBranch: true).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		harness.EnvelopeDecodes.ShouldBe(
			1,
			"without this, a payload framed without the format marker would drain through the legacy branch "
			+ "and every envelope arm would pass having tested the wrong one");
	}

	/// <summary>
	/// SAFETY. No row is created under the envelope's own identity.
	/// </summary>
	/// <remarks>
	/// Separate from the invariant arm because they fail independently: an identity that disagrees with the
	/// claim does not merely fail a comparison, it names a key the store has no row for — so the stage
	/// claims and finalizes a phantom entry under the producer's Guid while the real entry's accounting
	/// happens elsewhere.
	/// </remarks>
	[Fact]
	public async Task NotCreateARowUnderTheEnvelopeOwnIdentity()
	{
		await using var harness = await Harness.CreateAsync(throughTheEnvelopeBranch: true).ConfigureAwait(false);

		_ = await harness.Processor.DispatchPendingMessagesAsync(CancellationToken.None).ConfigureAwait(false);

		var phantom = await harness.GetEntryAsync(EnvelopeOwnId.ToString()).ConfigureAwait(false);
		phantom.ShouldBeNull(
			"an entry keyed by the envelope's producer-minted identity is a second record of one delivery: "
			+ "its attempts, its lease and its terminal status are invisible to every caller that addresses "
			+ "the real entry");
	}

	/// <summary>
	/// Decodes cleanly through the registry and carries no identity of its own, so the stage resolves the
	/// identity the drain seeded onto the context rather than one off the message.
	/// </summary>
	private sealed record DedupKeyProbeMessage(string Id) : IDispatchEvent;

	/// <summary>
	/// Drives the real <see cref="InboxProcessor"/> over a real in-memory inbox store holding one seeded,
	/// re-admittable entry, with the real <see cref="InboxMiddleware"/> standing where the dispatch
	/// pipeline's inbox stage stands.
	/// </summary>
	private sealed class Harness : IAsyncDisposable
	{
		private readonly ServiceProvider _services;
		private readonly IInboxStore _store;
		private readonly StubEnvelopeDeserializer? _deserializer;
		private int _handlerInvocations;

		private Harness(
			ServiceProvider services,
			IInboxStore store,
			InboxProcessor processor,
			StubEnvelopeDeserializer? deserializer)
		{
			_services = services;
			_store = store;
			_deserializer = deserializer;
			Processor = processor;
		}

		public InboxProcessor Processor { get; }

		public int HandlerInvocations => Volatile.Read(ref _handlerInvocations);

		public int EnvelopeDecodes => _deserializer?.Decodes ?? 0;

		/// <summary>Gets the context the drain handed to the dispatcher, as the stage received it.</summary>
		public IMessageContext? DispatchedContext { get; private set; }

		public static async Task<Harness> CreateAsync(
			bool throughTheEnvelopeBranch,
			bool withTerminalRowUnderEnvelopeOwnId = false)
		{
			MessageTypeRegistry.RegisterType<DedupKeyProbeMessage>();

			var services = new ServiceCollection();
			_ = services.AddLogging();
			_ = services.AddInMemoryInboxStore();

			var body = ProbeBody();
			var deserializer = throughTheEnvelopeBranch ? new StubEnvelopeDeserializer(body) : null;

			// One shared dispatcher instance rather than a per-scope fake, so the arms can observe what the
			// drain actually reached. A per-scope fake is unobservable from the test.
			var dispatcher = A.Fake<IDispatcher>();

			_ = services.AddScoped(_ => dispatcher);
			var provider = services.BuildServiceProvider();

			// AddInMemoryInboxStore registers the contract under the provider key, not unkeyed.
			var store = provider.GetRequiredKeyedService<IInboxStore>("inmemory");

			_ = await store.CreateEntryAsync(
				EntryKey,
				HandlerTypeName,
				MessageTypeName,
				throughTheEnvelopeBranch ? EnvelopeFramed(body) : body,
				new Dictionary<string, object>(StringComparer.Ordinal),
				CancellationToken.None).ConfigureAwait(false);

			// Put the entry into the state the re-admission drain selects: Failed, one attempt spent, and
			// last attempted long enough ago to clear the drain's always-on re-admission floor.
			await store.MarkFailedAsync(
				EntryKey, HandlerTypeName, "seeded failure", CancellationToken.None).ConfigureAwait(false);

			var seeded = await store.GetEntryAsync(EntryKey, HandlerTypeName, CancellationToken.None)
				.ConfigureAwait(false)
				?? throw new InvalidOperationException("Seeded inbox entry was not stored under its own key.");
			seeded.LastAttemptAt = DateTimeOffset.UtcNow.AddHours(-1);

			if (withTerminalRowUnderEnvelopeOwnId)
			{
				// What a first delivery of this message leaves behind: a terminal row keyed by the envelope's
				// own identity. The drain is not keyed by it and must never resolve to it.
				var envelopeOwnKey = EnvelopeOwnId.ToString();

				_ = await store.CreateEntryAsync(
					envelopeOwnKey,
					HandlerTypeName,
					MessageTypeName,
					body,
					new Dictionary<string, object>(StringComparer.Ordinal),
					CancellationToken.None).ConfigureAwait(false);

				await store.MarkProcessedAsync(envelopeOwnKey, HandlerTypeName, CancellationToken.None)
					.ConfigureAwait(false);
			}

			var processor = new InboxProcessor(
				CreateDrainOptions(),
				store,
				provider,
				new DispatchJsonSerializer(),
				NullLogger<InboxProcessor>.Instance,
				envelopeDeserializer: deserializer,
				circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance,
				deadLetterQueue: Excalibur.Dispatch.ErrorHandling.NullDeadLetterQueue.Instance);

			processor.Init("dispatcher-envelope-dedup-key");

			var harness = new Harness(provider, store, processor, deserializer);

			// The REAL inbox stage, over the SAME store, standing where the pipeline stands. The defect is a
			// disagreement between the identity the drain seeds and the claim it presents, and the stage is
			// the only thing that compares them -- so a dispatcher that merely records the call cannot
			// observe the consequence.
			var stage = new InboxMiddleware(
				Options.Create(new InboxConfigurationOptions { Enabled = true }),
				store,
				deduplicator: null,
				new DispatchJsonSerializer(),
				NullLogger<InboxMiddleware>.Instance);

			_ = A.CallTo(() => dispatcher.DispatchAsync(
					A<IDispatchMessage>._,
					A<IMessageContext>._,
					A<CancellationToken>._))
				.ReturnsLazily(call => harness.RunThroughStageAsync(
					stage,
					call.GetArgument<IDispatchMessage>(0)!,
					call.GetArgument<IMessageContext>(1)!,
					call.GetArgument<CancellationToken>(2)));

			return harness;
		}

		public ValueTask<InboxEntry?> GetEntryAsync(string messageId) =>
			_store.GetEntryAsync(messageId, HandlerTypeName, CancellationToken.None);

		/// <summary>Asks the store whether it has recorded this identity as already handled.</summary>
		public ValueTask<bool> IsProcessedAsync(string messageId) =>
			_store.IsProcessedAsync(messageId, HandlerTypeName, CancellationToken.None);

		public async ValueTask DisposeAsync()
		{
			await Processor.DisposeAsync().ConfigureAwait(false);
			await _services.DisposeAsync().ConfigureAwait(false);
		}

		/// <summary>The body the probe deserializes from, and the bytes the envelope carries as its payload.</summary>
		private static byte[] ProbeBody() =>
			Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new DedupKeyProbeMessage(EntryKey)));

		/// <summary>
		/// The legacy branch is chosen when the payload does NOT open with the envelope format marker, so
		/// the marker is what selects the branch under test — not the presence of a deserializer alone.
		/// </summary>
		private static byte[] EnvelopeFramed(byte[] body) => [0x01, .. body];

		private async Task<IMessageResult> RunThroughStageAsync(
			InboxMiddleware stage,
			IDispatchMessage message,
			IMessageContext context,
			CancellationToken cancellationToken)
		{
			DispatchedContext = context;

			return await stage.InvokeAsync(
				message,
				context,
				(_, _, _) =>
				{
					_ = Interlocked.Increment(ref _handlerInvocations);
					return new ValueTask<IMessageResult>(MessageResult.Success());
				},
				cancellationToken).ConfigureAwait(false);
		}

		private static IOptions<DeliveryInboxOptions> CreateDrainOptions() =>
			Options.Create(new DeliveryInboxOptions
			{
				Capacity =
				{
					QueueCapacity = 1,
					ProducerBatchSize = 1,
					ConsumerBatchSize = 1,
					PerRunTotal = 1,
					ParallelProcessingDegree = 1,
				},
				MaxAttempts = 5,
				BatchTuning =
				{
					EnableBatchDatabaseOperations = false,
				},
			});

		/// <summary>
		/// Returns an envelope whose payload is the probe body and whose own identity is deliberately NOT
		/// the entry key. Hand-written rather than a mock: the interface takes a span, and implementing it
		/// directly binds the interface's own contract.
		/// </summary>
		private sealed class StubEnvelopeDeserializer(byte[] body) : IBinaryEnvelopeDeserializer
		{
			private int _decodes;

			public int Decodes => Volatile.Read(ref _decodes);

			public EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data)
			{
				_ = Interlocked.Increment(ref _decodes);

				return new EnvelopeData
				{
					MessageId = EnvelopeOwnId,

					// The SHORT name, exactly as the legacy branch reads it off the entry, so the only
					// difference between the two branches under test is the identity.
					MessageType = MessageTypeName,
					Payload = body,
					Timestamp = DateTimeOffset.UtcNow,
				};
			}

			public EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data) =>
				throw new NotSupportedException("The inbox drain never reads an outbox envelope.");
		}
	}
}
