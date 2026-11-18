// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.ErrorHandling;

/// <summary>
/// What marking a dead letter message as replayed actually did, as decided by the store's own atomic action.
/// </summary>
/// <remarks>
/// <para>
/// <b>The mark is a CLAIM on a replay, not a RECORD of one, and this return is what makes the difference
/// observable.</b> While it returned nothing, the caller could not tell "I marked it" from "somebody else
/// already had", so the only safe ordering was to dispatch first and mark afterwards — which left a window
/// in which two callers both read an unmarked message, both dispatched it, and the second mark merely
/// rewrote a flag that was already set. Winning this claim is what grants the right to replay.
/// </para>
/// <para>
/// <b>Test for <see cref="Claimed"/> by name.</b> Deciding success by excluding the refusals means every
/// member added later starts life as a success, which is the way this contract fails open.
/// </para>
/// </remarks>
public enum DeadLetterReplayOutcome
{
	/// <summary>
	/// No store decided anything. The claim must be treated as NOT held and the message must not be replayed.
	/// </summary>
	/// <remarks>
	/// <b>Zero is deliberately not a success state, and this member exists to keep it that way.</b> An
	/// unassigned field, a <see langword="default"/> value, or a task that completed without a result being
	/// set all produce the zero value, and a caller cannot distinguish any of them from a value a store
	/// chose. If zero meant success, each of them would assert a claim nobody holds.
	/// </remarks>
	Unknown = 0,

	/// <summary>
	/// This caller moved the message from not-replayed to replayed. It holds the claim and may replay.
	/// </summary>
	/// <remarks>
	/// <b>The only success value.</b> Exactly one concurrent caller can receive it for a given message,
	/// because the store's statement carries the not-replayed condition and its row count is the decision.
	/// </remarks>
	Claimed = 1,

	/// <summary>
	/// The message is present and was already marked replayed, so this caller did not claim it and must not
	/// replay it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Reached by a sequential second call as readily as by a concurrent one — an operator retrying a replay
	/// that appeared to hang is the ordinary way to arrive here, and it needs no race at all.
	/// </para>
	/// <para>
	/// <b>This is not an error and must not be reported as one.</b> It is the mechanism working: the second
	/// caller is being told the work is already done or already owned.
	/// </para>
	/// </remarks>
	AlreadyReplayed = 2,

	/// <summary>
	/// No message with that identifier was present in the caller's tenant when the statement ran.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="AlreadyReplayed"/> because the two have opposite diagnoses and the store can
	/// tell them apart for free: a missing message means nothing is owed, whereas a present and already
	/// replayed one means the work was taken. Collapsing them would report a completed replay as an absence.
	/// </remarks>
	MessageNotFound = 3,
}
