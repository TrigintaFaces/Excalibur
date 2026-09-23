// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// The value a completion outcome takes when nobody assigned one must not be the value that means the
/// write succeeded.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is worth an arm at all.</b> The outcome was introduced so a store could REPORT a refusal
/// instead of throwing. With the successful state sitting on zero, every uninitialised field, every
/// <c>default(OutboxCompletionOutcome)</c>, and every path that forgets to assign reports a durable write
/// that never happened - which is the exact class the type exists to remove, reappearing in the type
/// itself.
/// </para>
/// <para>
/// <b>Why a TEST and not a review note.</b> This was found by three people reading the diff at the last
/// gate before integration, while the suite was 1832/0 green. A green suite was compatible with it,
/// because no arm asked the question. Reading caught it once; nothing would catch it the second time, and
/// the second time is a new enum written by someone who never read this thread.
/// </para>
/// <para>
/// <b>Scope, stated so a green here is not over-read.</b> This binds ONE property of ONE type: the
/// default is not the success state. It says nothing about the consuming switch, whose unrecognised-value
/// branch is a separate defect with the same effect - fixing either alone leaves the framework reporting
/// a success it did not perform. No arm here covers that branch, because the remedy for it is a contract
/// question that was still open when this was written, and an arm written to a guess locks the guess.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
[Trait("Priority", "1")]
public sealed class OutboxCompletionOutcomeDefaultShould
{
	/// <summary>
	/// SAFETY. An unassigned outcome does not claim the write was applied.
	/// </summary>
	[Fact]
	public void Not_report_success_when_nobody_assigned_a_value()
	{
		default(OutboxCompletionOutcome).ShouldNotBe(
			OutboxCompletionOutcome.Applied,
			"a caller that forgets to assign, a field that is never initialised, and a store that returns "
			+ "an unset value all produce this. If it means 'applied', the framework reports a durable "
			+ "write it never performed - which is the failure this outcome type was introduced to end");
	}

	/// <summary>
	/// LIVENESS. The success state still exists and is still distinguishable.
	/// </summary>
	/// <remarks>
	/// The pair to the arm above, and not a formality: deleting <c>Applied</c> outright, or collapsing it
	/// onto a refusal, satisfies that arm completely and leaves no way to report a write that did succeed.
	/// A store that can only ever say "refused" is as useless as one that can only ever say "applied".
	/// </remarks>
	[Fact]
	public void Still_offer_a_distinct_way_to_report_a_write_that_did_apply()
	{
		OutboxCompletionOutcome.Applied.ShouldNotBe(
			OutboxCompletionOutcome.ClaimLost,
			"success and a lost claim must stay distinguishable, or the caller cannot tell a completed "
			+ "report from a refused one");

		OutboxCompletionOutcome.Applied.ShouldNotBe(
			OutboxCompletionOutcome.MessageNotFound,
			"success and a missing message must stay distinguishable - they have opposite diagnoses");
	}
}
