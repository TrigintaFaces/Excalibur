// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Registry;
using Excalibur.Dispatch.Serialization;

using FakeItEasy;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using DeliveryOutboxOptions = Excalibur.Dispatch.Options.Delivery.OutboxDeliveryOptions;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// The claim identity a store stamps on a reserved message must still be there when the OUTBOX PROCESSOR
/// reports that message's failure, because it is the term the claim-scoped fence compares.
/// </summary>
/// <remarks>
/// <para>
/// <b>One of TWO drains, and the summary used to say "the drain".</b> The definite article claimed a
/// singularity that does not exist: <c>MessageBusOutboxPublisher</c> is a second drain with its own claim
/// of the same property, and this file asserts nothing about it. A reader who found this arm and took it
/// for the whole would have concluded the identity was bound for the outbox when it was bound for half of
/// it. The sibling lives in the messaging test project and is named for its own subject, so neither file
/// can be read as covering the other.
/// </para>
/// <para>
/// <b>Defect.</b> The store stamps the identity onto the reserved outbound message, and the drain's own
/// conversion to the internal message type sets it back to <see langword="null"/> on both branches. Every
/// failure call site then passes that null, and the capability lookup is guarded by a non-empty check on
/// the identity - so it resolves to nothing and the write takes the unscoped route. The store offers the
/// claim-scoped surface, implements it, and is never asked for it.
/// </para>
/// <para>
/// <b>Why it survived every reading.</b> One comment sits on two adjacent assignments and is accurate
/// about the second: the sibling property genuinely is absent from the source type. A reviewer who checks
/// the comment against the line it fits finds it correct and moves on.
/// </para>
/// <para>
/// <b>Both conversion branches, because a half-fix is otherwise invisible.</b> The drain converts through
/// an envelope branch or a legacy branch depending on whether a binary envelope deserializer is present
/// and the payload carries the format marker. Each branch has its own copy of the field block, so each
/// discards the identity independently. An arm that exercised only one would go green against a fix
/// applied to only that one - measured: with the envelope branch alone repaired, the legacy-branch arm
/// stays red, and the converse would read as a clean pass.
/// </para>
/// <para>
/// <b>Only the identity is at stake here, not the whole field block.</b> The sibling assignment beside
/// each discard nulls a timeout the source type does not carry - measured, zero occurrences on the
/// outbound message against a passing control - so that null is correct and its comment is true. Four
/// assignments, two defects.
/// </para>
/// <para>
/// <b>What this does NOT decide.</b> It asserts nothing about a store that does not offer the claim-scoped
/// surface. Whether such a store should fall back or fail fast is an open contract question, and an arm
/// written before it settles would lock in whichever answer its author guessed. This store offers the
/// capability, so the requirement here is unambiguous however that question lands.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxClaimIdentitySurvivesConversionShould
{
	private const string StampedClaim = "claim-alpha";

	/// <summary>
	/// SAFETY. The failure write is the one that carries the claim, not the one that omits it.
	/// </summary>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Report_the_failure_through_the_claim_scoped_route(bool throughTheEnvelopeBranch)
	{
		var store = await DrainAsync(throughTheEnvelopeBranch);

		store.FailureMarks.ShouldBeGreaterThan(
			0,
			"liveness first: an arm whose drain never reached a failure mark would satisfy any claim about "
			+ "which route that mark took");

		store.UnscopedFailureMarks.ShouldBe(
			0,
			"this store implements the claim-scoped surface and stamped an identity on the message it "
			+ "handed out. A failure recorded through the unscoped member is a write no fence can refuse, "
			+ "so a superseded tenure's failure lands on a row the live tenure owns");
	}

	/// <summary>
	/// SAFETY. The identity that arrives is the one the store stamped, not a substitute.
	/// </summary>
	/// <remarks>
	/// Separate from the arm above because routing and payload fail independently: a fix that reached the
	/// claim-scoped member while passing an empty or regenerated identity would satisfy that arm entirely
	/// and still compare the wrong term.
	/// </remarks>
	[Theory]
	[InlineData(false)]
	[InlineData(true)]
	public async Task Present_the_identity_the_store_stamped(bool throughTheEnvelopeBranch)
	{
		var store = await DrainAsync(throughTheEnvelopeBranch);

		store.FailureMarks.ShouldBeGreaterThan(0, "the drain must reach a failure mark");

		store.PresentedClaim.ShouldBe(
			StampedClaim,
			"the fence compares the identity stamped on the row against the one the caller presents, so a "
			+ "regenerated or emptied identity is refused for the same reason a superseded one is - and the "
			+ "caller cannot tell the two apart");
	}

	/// <summary>
	/// LIVENESS. A store that REFUSES one message's failure report must cost that message and nothing else.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is the property the returned outcome exists to protect, and it is why the refusal is not a
	/// throw.</b> Every call site that reports a delivery failure sits inside the drain's own
	/// <c>catch</c> block, where a sibling <c>catch</c> clause on the same <c>try</c> cannot run. An
	/// exception raised from the completion therefore escapes the whole cycle and abandons every message
	/// the caller still legitimately holds - and a lost claim concerns exactly ONE row, while the tenure
	/// stays intact.
	/// </para>
	/// <para>
	/// <b>What this does NOT prove.</b> It says nothing about whether a real store evaluates the claim and
	/// the mutation in one atomic action - a fake returns what it was told, so atomicity is a property of
	/// the statement and only real infrastructure can observe it. That arm is separate and belongs on a
	/// live database.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task Cost_one_message_when_the_store_declines_the_report()
	{
		var store = await DrainAsync(
			throughTheEnvelopeBranch: false,
			messagesToServe: 2,
			scopedOutcome: OutboxCompletionOutcome.ClaimLost);

		store.FailureMarks.ShouldBe(
			2,
			"the store declined the first report, which concerns one row and leaves the tenure intact. A "
			+ "caller that unwinds or returns early there abandons the messages it still owns - so the "
			+ "second message reaching its own failure mark is what says the refusal cost one row");

		store.UnscopedFailureMarks.ShouldBe(
			0,
			"a refusal must not push the caller onto the route that carries no claim: that would turn a "
			+ "declined write into an unrefusable one, which is the opposite of the guard's purpose");
	}

	private static async Task<ClaimObservingStore> DrainAsync(
		bool throughTheEnvelopeBranch,
		int messagesToServe = 1,
		OutboxCompletionOutcome scopedOutcome = OutboxCompletionOutcome.Applied)
	{
		MessageTypeRegistry.RegisterType<UndispatchableProbe>();

		var store = new ClaimObservingStore(throughTheEnvelopeBranch, messagesToServe)
		{
			ScopedOutcome = scopedOutcome,
		};

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = messagesToServe,
			MaxAttempts = 5,
			BatchProcessing = { ParallelProcessingDegree = 1 },
		});

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			envelopeDeserializer: throughTheEnvelopeBranch ? new StubEnvelopeDeserializer() : null,
			circuitBreakerRegistry: PassThroughCircuitBreakerRegistry.Instance);

		processor.Init("claim-identity-conversion-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token);

		return store;
	}

	/// <summary>
	/// The body the probe type deserializes from, and the same bytes the envelope carries as its payload.
	/// </summary>
	private static byte[] ProbeBody() => System.Text.Encoding.UTF8.GetBytes("{}");

	/// <summary>
	/// The legacy branch is chosen when the payload does NOT open with the envelope format marker, so the
	/// marker is what selects the branch under test - not the deserializer alone.
	/// </summary>
	private static byte[] EnvelopeFramedBody() => [0x01, .. ProbeBody()];

	/// <summary>
	/// Returns an envelope whose payload is the probe body, so the envelope branch decodes and then fails
	/// to dispatch exactly as the legacy branch does. Hand-written rather than a mock: the interface takes
	/// a span, and implementing it directly binds the interface's own contract.
	/// </summary>
	private sealed class StubEnvelopeDeserializer : IBinaryEnvelopeDeserializer
	{
		public EnvelopeData? DeserializeInboxEnvelope(ReadOnlySpan<byte> data) => OutboxEnvelope();

		public EnvelopeData? DeserializeOutboxEnvelope(ReadOnlySpan<byte> data) => OutboxEnvelope();

		private static EnvelopeData OutboxEnvelope() =>
			new()
			{
				MessageId = Guid.NewGuid(),
				MessageType = typeof(UndispatchableProbe).FullName,
				Payload = ProbeBody(),
				Timestamp = DateTimeOffset.UtcNow,
			};
	}

	/// <summary>
	/// Hands out one message carrying a stamped claim identity, then nothing so the drain terminates, and
	/// records which failure route the drain took.
	/// </summary>
	/// <remarks>
	/// Implements the claim-scoped capability DIRECTLY rather than through a base class, so the assertion
	/// binds the interface's own requirement. Capability discovery is by instance type, so implementing it
	/// is all that is needed for the drain to find it.
	/// </remarks>
	private sealed class ClaimObservingStore(bool envelopeFormat, int messagesToServe = 1)
		: IOutboxStore, IClaimScopedOutboxStore, IDeadLetterableOutboxStore
	{
		private int _served;

		public int FailureMarks { get; private set; }

		public int UnscopedFailureMarks { get; private set; }

		public string? PresentedClaim { get; private set; }

		/// <summary>
		/// Gets or sets what the store reports back for a claim-scoped completion. Defaults to
		/// <see cref="OutboxCompletionOutcome.Applied"/>; an arm sets it to a refusal to drive the caller.
		/// </summary>
		public OutboxCompletionOutcome ScopedOutcome { get; set; } = OutboxCompletionOutcome.Applied;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, CancellationToken cancellationToken)
		{
			if (Interlocked.Increment(ref _served) > messagesToServe)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			}

			// The message must DECODE and then fail to dispatch. A type the registry cannot resolve is a
			// decode failure, which the drain treats as terminal for the row and dead-letters directly --
			// never reaching the failure path this arm is about. So the type is registered and the payload
			// is valid, and the dispatch fails instead because no dispatcher can be resolved.
			var message = new OutboundMessage
			{
				Id = "msg-" + Guid.NewGuid().ToString("N"),
				MessageType = typeof(UndispatchableProbe).FullName!,
				Destination = "test-destination",
				Payload = envelopeFormat ? EnvelopeFramedBody() : ProbeBody(),
				CreatedAt = DateTimeOffset.UtcNow,
				Status = OutboxStatus.Staged,
				RetryCount = 0,
				DispatcherId = StampedClaim,
			};

			return new ValueTask<IEnumerable<OutboundMessage>>([message]);
		}

		public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, string claimIdentity,
			CancellationToken cancellationToken)
		{
			FailureMarks++;
			PresentedClaim = claimIdentity;
			return new ValueTask<OutboxCompletionOutcome>(ScopedOutcome);
		}

		public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(
			string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt,
			string claimIdentity, CancellationToken cancellationToken)
		{
			FailureMarks++;
			PresentedClaim = claimIdentity;
			return new ValueTask<OutboxCompletionOutcome>(ScopedOutcome);
		}

		public ValueTask MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			FailureMarks++;
			UnscopedFailureMarks++;
			return ValueTask.CompletedTask;
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask MarkDeadLetteredAsync(
			string messageId, string reason, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask EnqueueAsync(
			IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}

/// <summary>
/// Decodes cleanly and cannot be dispatched: the drain resolves its handler from a service provider that
/// supplies nothing, so the dispatch throws and the row takes the retryable failure path rather than the
/// terminal decode-failure one.
/// </summary>
public sealed class UndispatchableProbe : IDispatchMessage
{
}
