// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// What an administrative inbox mark-failed actually did, as decided by the store's own atomic statement.
/// </summary>
/// <remarks>
/// <para>
/// <b>A mutation that can decline must say so in its return, and this enumeration is that return.</b> The
/// members below are not shades of success: exactly one of them means the entry was mutated, and a caller
/// that cannot tell them apart cannot tell a refusal from a write. Returning nothing makes "it declined"
/// and "it acted" the same observation, and no log line closes that gap — a log is readable by an operator
/// afterwards, never by the caller deciding what to do next.
/// </para>
/// <para>
/// <b>The refusals are reported, never thrown.</b> The administrative mark is issued from inside a drain's
/// own failure handling, where a sibling <c>catch</c> cannot run; an exception raised there escapes the
/// whole cycle and abandons every entry the caller still legitimately holds. A refusal on one entry must
/// cost that entry and nothing else. Before this type existed, half the shipped stores threw on an absent
/// entry and half returned silently, so a caller could write code correct against neither.
/// </para>
/// <para>
/// <b>It is deliberately not the outbox's completion outcome.</b> That enumeration reports a lost claim and
/// a superseded fencing tenure, neither of which this operation takes or can refuse on, so importing it
/// would hand callers two members no store can ever produce. It has, conversely, no member for the refusal
/// this operation actually performs — a present entry in the terminal processed state — and reporting that
/// as a missing entry would state an absence about a row that is there.
/// </para>
/// </remarks>
public enum InboxMarkFailedOutcome
{
	/// <summary>
	/// No store decided anything. The write must be treated as NOT applied.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Zero is deliberately not a success state, and this member exists to keep it that way.</b> An
	/// unassigned field, a <c>default</c> value, a task that completed without a result being set — every
	/// one of these produces the zero value, and a caller has no way to distinguish it from a value a store
	/// chose. If zero meant success, all of them would assert a durable write that never happened, which is
	/// the exact failure this enumeration was introduced to end.
	/// </para>
	/// <para>
	/// <b>This is not an unreachable member.</b> It is produced by the language rather than by any store —
	/// which is precisely why it must be named: an undeclared zero would be just as reachable and harder to
	/// read about at the point where someone is deciding what to do with it.
	/// </para>
	/// </remarks>
	Unknown = 0,

	/// <summary>
	/// The store mutated the entry: it existed in the addressed tenant partition, it was not terminal, and
	/// the failure is recorded with the retry count set exactly as asked.
	/// </summary>
	/// <remarks>
	/// <b>The only success value, and callers must test for it by name.</b> Deciding success by excluding
	/// the refusals means every member added later starts life as a success, which is the way this contract
	/// fails open.
	/// </remarks>
	Applied = 1,

	/// <summary>
	/// No entry with that message identifier and handler type exists in the addressed tenant partition —
	/// it was never staged, retention removed it, or the caller addressed the wrong tenant.
	/// </summary>
	/// <remarks>
	/// <b>This is the outcome that used to be silent, and the reason the operation could mislead.</b> An
	/// operator who addressed the wrong partition received a completed task indistinguishable from a
	/// successful mark and walked away believing production state had changed. Because the tenant is now a
	/// parameter rather than ambient, a caller receiving this knows exactly which partition was searched.
	/// </remarks>
	EntryNotFound = 2,

	/// <summary>
	/// The entry is present in the addressed tenant partition and is already in the terminal
	/// <see cref="InboxStatus.Processed"/> state, so the transition to failed was refused and nothing was
	/// written.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Refusing is the correct behaviour, not a failure to handle.</b> Processed is absorbing: demoting a
	/// finalized entry to failed would re-admit it to the retry drain and run its handler a second time over
	/// side effects already committed.
	/// </para>
	/// <para>
	/// <b>Distinct from <see cref="EntryNotFound"/> because the two have opposite diagnoses and every store
	/// can tell them apart for free.</b> A missing entry means the caller is addressing something that is not
	/// there — a wrong identifier or a wrong tenant, and the next step is to look. A present terminal entry
	/// means the work is done and nothing is owed. Collapsing them reports a completed message as a lost one.
	/// </para>
	/// </remarks>
	AlreadyProcessed = 3,

	/// <summary>
	/// The store was asked and could not reach a decision: nothing was written by this call, and the entry's
	/// state is not known to it.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Characteristically reported when an optimistic-concurrency retry loop is exhausted because a competing
	/// writer won every attempt. Each losing attempt wrote nothing, so the entry is left exactly as it was
	/// found and the operation is safe to re-drive. Stores that perform the transition in a single
	/// compare-and-set statement have no loop to exhaust and so never reach this state; it belongs to the
	/// document stores, which need a precondition loop.
	/// </para>
	/// <para>
	/// <b>This is a decided "I do not know", and it is deliberately distinct from <see cref="Unknown"/>.</b>
	/// <see cref="Unknown"/> is the zero-valued poison default meaning no store decided anything at all,
	/// which is what catches an implementation that forgets to set an outcome. This member is a value a store
	/// returns on purpose. Collapsing the two would make a forgetful store indistinguishable from a contended
	/// one.
	/// </para>
	/// <para>
	/// <b>Consumer obligation: treat this as NOT applied.</b> It is reported rather than thrown because the
	/// caller is typically already handling a failure — the message it was recording is the thing that went
	/// wrong — and an exception raised here would replace that original error with one about the store.
	/// </para>
	/// </remarks>
	Undecided = 4,
}
