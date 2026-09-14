// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
/// When a leadership tenure is active and the store offers a fenced completion, the drain must USE it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists: the capability was reachable and nothing asserted that it was reached.</b> Two
/// arms already prove the fenced capabilities survive the encrypting decorator, and both are decorator
/// tests — they resolve a capability and never run a drain. So a regression that deleted the drain's probe
/// entirely would leave every suite green while every completion silently returned to the unfenced member.
/// That is the same shape as the defect this seam was built to close, one level up: a guard that is present
/// and not consulted.
/// </para>
/// <para>
/// <b>Authored by the implementer of the code it binds, which is disclosed rather than hidden.</b> The
/// independent author was unavailable and the set was blocked on this arm; an arm written by the person who
/// wrote the branch is worth less than one written against the contract by someone else, and it is worth
/// considerably more than the nothing it replaces. It should be re-derived, and the mutations below are
/// stated so that re-derivation is cheap.
/// </para>
/// <para>
/// <b>Scope.</b> This binds ROUTING only: which member the drain calls. It says nothing about whether the
/// store evaluates the fence atomically — a fake returns what it was told, and atomicity is a property of
/// the statement, observable only against real infrastructure.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxPrefersTheFencedCompletionShould
{
	private const string StampedClaim = "claim-alpha";
	private const long Tenure = 7;

	/// <summary>
	/// SAFETY. An active tenure reports its failure through the member that carries the fence.
	/// </summary>
	[Fact]
	public async Task Report_the_failure_through_the_fenced_member_when_a_tenure_is_active()
	{
		var store = await DrainAsync(fencingActive: true);

		store.FencedFailureMarks.ShouldBeGreaterThan(
			0,
			"liveness first: an arm whose drain never reached a failure mark would satisfy any claim about "
			+ "which member that mark used");

		store.UnfencedFailureMarks.ShouldBe(
			0,
			"a failure recorded through a member that carries no fencing token is a write no fence can "
			+ "refuse, so a superseded tenure's report lands on a row the live tenure owns. The fenced "
			+ "member exists precisely to make that write refusable, and routing past it wastes it");

		store.PresentedAuthority.ClaimIdentity.ShouldBe(
			StampedClaim,
			"the authority must carry the identity the store stamped, not a substitute");
		store.PresentedAuthority.FencingToken.ShouldBe(
			Tenure,
			"and the tenure the gate is currently holding");
	}

	/// <summary>
	/// LIVENESS. With no tenure, the pre-existing unfenced route still runs.
	/// </summary>
	/// <remarks>
	/// The twin of the arm above, and not a formality: making the fenced member the only route would break
	/// every deployment that runs a single writer with no leader election, which is a supported
	/// configuration. A fix that protects the fenced case by breaking the unfenced one is not a fix.
	/// </remarks>
	[Fact]
	public async Task Still_report_through_the_unfenced_route_when_no_tenure_exists()
	{
		var store = await DrainAsync(fencingActive: false);

		store.FencedFailureMarks.ShouldBe(
			0,
			"with no leadership gate there is no tenure to present, so the fenced member must not be chosen");

		(store.UnfencedFailureMarks + store.ClaimScopedFailureMarks).ShouldBeGreaterThan(
			0,
			"but the failure must still be recorded: an unfenced deployment is supported, not degraded");
	}

	/// <summary>
	/// SAFETY. The TERMINAL transition takes the fenced member too, and this is the one that destroys a row.
	/// </summary>
	/// <remarks>
	/// Separate from the failure-report arms because it is a separate branch in a separate method, and
	/// because the consequence differs in kind. A failure report written by a superseded tenure corrupts a
	/// status; a terminal transition written by one REMOVES the outbox row on the stores whose idiom is to
	/// delete. Routing past the fence there is the only completion in this subsystem that a live successor
	/// cannot undo by re-draining.
	/// </remarks>
	[Fact]
	public async Task Take_the_terminal_transition_through_the_fenced_member_when_a_tenure_is_active()
	{
		var store = await DrainAsync(fencingActive: true, maxAttempts: 1);

		store.FencedDeadLetters.ShouldBeGreaterThan(
			0,
			"liveness first: an arm whose drain never reached the terminal transition would satisfy any "
			+ "claim about which member performed it");

		store.UnfencedDeadLetters.ShouldBe(
			0,
			"the unfenced terminal member matches on message id alone, so a superseded tenure reaching its "
			+ "attempt ceiling ends a message a live successor still holds. On a store that deletes the row "
			+ "this is the completion with no way back");

		store.PresentedDeadLetterToken.ShouldBe(
			Tenure,
			"and it must present the tenure the gate currently holds, not a default");
	}

	/// <summary>
	/// LIVENESS. With no tenure the pre-existing terminal route still runs.
	/// </summary>
	[Fact]
	public async Task Still_dead_letter_through_the_unfenced_route_when_no_tenure_exists()
	{
		var store = await DrainAsync(fencingActive: false, maxAttempts: 1);

		store.FencedDeadLetters.ShouldBe(
			0,
			"with no leadership gate there is no tenure to present");

		store.UnfencedDeadLetters.ShouldBeGreaterThan(
			0,
			"but an exhausted message must still reach a terminal state, or it is re-claimed forever");
	}

	private static async Task<FencedObservingStore> DrainAsync(bool fencingActive, int maxAttempts = 5)
	{
		MessageTypeRegistry.RegisterType<UndispatchableProbe>();

		var store = new FencedObservingStore();

		var options = Options.Create(new DeliveryOutboxOptions
		{
			QueueCapacity = 8,
			ProducerBatchSize = 1,
			ConsumerBatchSize = 1,
			PerRunTotal = 1,
			MaxAttempts = maxAttempts,
			BatchProcessing = { ParallelProcessingDegree = 1 },
		});

		ILeaderProcessingGate? gate = null;
		if (fencingActive)
		{
			gate = A.Fake<ILeaderProcessingGate>();
			A.CallTo(() => gate.ShouldProcess).Returns(true);
			A.CallTo(() => gate.FencingToken).Returns(Tenure);
		}

		await using var processor = new OutboxProcessor(
			options,
			store,
			new DispatchJsonSerializer(),
			A.Fake<IServiceProvider>(),
			NullLogger<OutboxProcessor>.Instance,
			leaderGate: gate);

		processor.Init("fenced-completion-routing-test");

		using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
		_ = await processor.DispatchPendingMessagesAsync(cts.Token);

		return store;
	}

	/// <summary>
	/// Offers BOTH completion surfaces and records which one the drain chose.
	/// </summary>
	/// <remarks>
	/// Implements every contract directly rather than through a first-party base, so the assertions bind the
	/// interfaces' own requirements.
	/// <para>
	/// <b>It must implement the fenced CLAIM as well, and that is the product being stricter than I
	/// assumed rather than fixture noise.</b> A first version omitted it, on the reasoning that the property
	/// under test is which COMPLETION member is chosen. The startup invariant refused to construct the
	/// processor at all: under a registered leader election a store that cannot record a high-water mark is
	/// rejected outright rather than run unfenced. So a fenced completion is only ever probed on a store
	/// that also carries the fenced claim, and a fixture pairing them is the realistic one.
	/// </para>
	/// </remarks>
	private sealed class FencedObservingStore
		: IOutboxStore, IClaimScopedOutboxStore, IFencedClaimScopedOutboxStore, IDeadLetterableOutboxStore,
			IFencedDeadLetterableOutboxStore, IFencedOutboxStore
	{
		private int _served;

		public int FencedFailureMarks { get; private set; }

		public int ClaimScopedFailureMarks { get; private set; }

		public int UnfencedFailureMarks { get; private set; }

		public OutboxWriteAuthority PresentedAuthority { get; private set; }

		public int FencedDeadLetters { get; private set; }

		public int UnfencedDeadLetters { get; private set; }

		public long PresentedDeadLetterToken { get; private set; }

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, long fencingToken, CancellationToken cancellationToken)
			=> GetUnsentMessagesAsync(batchSize, cancellationToken);

		public ValueTask MarkSentAsync(string messageId, long fencingToken, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask<IEnumerable<OutboundMessage>> GetUnsentMessagesAsync(
			int batchSize, CancellationToken cancellationToken)
		{
			if (Interlocked.Increment(ref _served) > 1)
			{
				return new ValueTask<IEnumerable<OutboundMessage>>([]);
			}

			// Decodes cleanly and cannot be dispatched: the service provider supplies no handler, so the
			// dispatch throws and the row takes the retryable failure path rather than the decode-failure one.
			var message = new OutboundMessage
			{
				Id = "msg-" + Guid.NewGuid().ToString("N"),
				MessageType = typeof(UndispatchableProbe).FullName!,
				Destination = "test-destination",
				Payload = System.Text.Encoding.UTF8.GetBytes("{}"),
				CreatedAt = DateTimeOffset.UtcNow,
				Status = OutboxStatus.Staged,
				RetryCount = 0,
				DispatcherId = StampedClaim,
			};

			return new ValueTask<IEnumerable<OutboundMessage>>([message]);
		}

		public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, DateTimeOffset? nextAttemptAt,
			OutboxWriteAuthority authority, CancellationToken cancellationToken)
		{
			FencedFailureMarks++;
			PresentedAuthority = authority;
			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}

		public ValueTask<OutboxCompletionOutcome> MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, string claimIdentity,
			CancellationToken cancellationToken)
		{
			ClaimScopedFailureMarks++;
			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}

		public ValueTask<OutboxCompletionOutcome> MarkFailedWithBackoffAsync(
			string messageId, string errorMessage, int retryCount, DateTimeOffset nextAttemptAt,
			string claimIdentity, CancellationToken cancellationToken)
		{
			ClaimScopedFailureMarks++;
			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}

		public ValueTask MarkFailedAsync(
			string messageId, string errorMessage, int retryCount, CancellationToken cancellationToken)
		{
			UnfencedFailureMarks++;
			return ValueTask.CompletedTask;
		}

		public ValueTask MarkSentAsync(string messageId, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask StageMessageAsync(OutboundMessage message, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;

		public ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
			string messageId, string reason, long fencingToken, CancellationToken cancellationToken)
		{
			FencedDeadLetters++;
			PresentedDeadLetterToken = fencingToken;
			return new ValueTask<OutboxCompletionOutcome>(OutboxCompletionOutcome.Applied);
		}

		public ValueTask MarkDeadLetteredAsync(
			string messageId, string reason, CancellationToken cancellationToken)
		{
			UnfencedDeadLetters++;
			return ValueTask.CompletedTask;
		}

		public ValueTask EnqueueAsync(
			IDispatchMessage message, IMessageContext context, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}
}
