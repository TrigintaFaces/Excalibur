// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Saga.Tests.Orchestration;

/// <summary>
/// An event is recorded as processed IF AND ONLY IF a handler acted on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect these arms bind.</b> The coordinator marked an event processed BEFORE invoking the
/// handler, and persisted that mark afterwards regardless of what the handler did. A handler guarded
/// by a condition returns without acting when the condition is false — so a message that arrived
/// before its guard was satisfiable was recorded as processed and permanently retired. It was never
/// redelivered, no error was raised, and nothing downstream could detect it.
/// </para>
/// <para>
/// <b>Why the guarantee document did not catch it.</b> The dedup guarantee states the bound on the
/// remembered set falsifiably, and says nothing about what "processed" MEANS. It even names this
/// failure direction for a different mechanism — a key collision causing "zero execution, which no
/// consumer obligation on this page covers and which emits the same log line a correct dedup emits" —
/// and the consumer obligation it does state, that steps be idempotent, does not help: idempotence
/// protects against re-execution, and this is the opposite failure.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> These arms exercise <see cref="SagaState"/>'s guard directly, which is the seam
/// the coordinator now depends on: asking must not record. Make <c>HasProcessedEvent</c> mark the id —
/// the conflation the old code had, where the CHECK was the RECORD — and
/// <see cref="Leave_an_unacted_event_deliverable"/> goes RED while
/// <see cref="Refuse_an_event_that_was_genuinely_processed"/> stays GREEN.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Saga")]
public sealed class DeclinedEventStaysDeliverableShould
{
	/// <summary>Asking whether an event was processed must not record that it was.</summary>
	/// <remarks>
	/// This is the whole fix in one assertion. The coordinator has to ask the question BEFORE it can
	/// know whether a handler will act, and the old code asked it with the mutating call — so merely
	/// looking retired the message.
	/// </remarks>
	[Fact]
	public void Leave_an_unacted_event_deliverable()
	{
		var state = new TrackedSaga();

		var alreadySeen = state.HasProcessedEvent("evt-1");

		alreadySeen.ShouldBeFalse("nothing has processed this event yet");
		state.HasProcessedEvent("evt-1").ShouldBeFalse(
			"asking must not record. The coordinator asks before it knows whether a handler will act, "
			+ "so a question that records is what permanently retired a declined event.");
		state.TryMarkEventProcessed("evt-1").ShouldBeTrue(
			"and the event must still be markable afterwards -- if the question consumed it, the "
			+ "handler that DID act could no longer record that it had");
	}

	/// <summary>SAFETY: an event that was genuinely processed is not processed twice.</summary>
	/// <remarks>
	/// The liveness partner. Without it, the arm above is satisfied by a guard that never remembers
	/// anything — which would stop retiring declined events by also failing to deduplicate real ones.
	/// </remarks>
	[Fact]
	public void Refuse_an_event_that_was_genuinely_processed()
	{
		var state = new TrackedSaga();

		state.TryMarkEventProcessed("evt-1").ShouldBeTrue();

		state.HasProcessedEvent("evt-1").ShouldBeTrue("it was recorded, so the guard must report it");
		state.TryMarkEventProcessed("evt-1").ShouldBeFalse("a second mark of the same id is a duplicate");
	}

	/// <summary>The outcome type can express declining, which is what makes the gate possible.</summary>
	/// <remarks>
	/// <c>Declined</c> is deliberately the default value. A handler that somehow reports nothing is
	/// treated as not having acted, which is the safe direction: the cost is a redelivery, not a lost
	/// message.
	/// </remarks>
	[Fact]
	public void Default_to_declined_so_an_unreported_outcome_is_not_mistaken_for_work()
	{
		default(SagaEventOutcome).ShouldBe(
			SagaEventOutcome.Declined,
			"an outcome nobody set must not read as Handled -- that is the silent retirement this "
			+ "change removes, reintroduced through the enum's default");
	}

	private sealed class TrackedSaga : SagaState
	{
	}
}
