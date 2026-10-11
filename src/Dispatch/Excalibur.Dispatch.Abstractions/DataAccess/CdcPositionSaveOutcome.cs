// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// What a CDC checkpoint save actually did, as decided by the store's own write.
/// </summary>
/// <remarks>
/// <para>
/// <b>A save that can decline must say so in its return, and this enumeration is that return.</b> A store
/// that refuses to regress a checkpoint and reports nothing makes "it declined" and "it advanced" the same
/// observation, and no log line closes that gap: a log is readable by an operator afterwards, never by the
/// caller deciding whether its progress is durable. Before this type existed, three stores implemented three
/// mutually incompatible postconditions behind one <c>Task</c>-returning member — unconditional
/// last-write-wins, a silent monotonic refusal, and a thrown monotonic refusal — and a consumer could not
/// tell which one it had without reading the implementation.
/// </para>
/// <para>
/// <b>The refusals are reported, never thrown, and that is a liveness requirement rather than a taste.</b> A
/// checkpoint save is the last step of a processing cycle. An exception raised there abandons the cycle over
/// a condition that resolves itself — the next batch saves a later position, which supersedes the one that
/// was refused — so throwing converts a self-healing situation into lost processing. A refusal must cost the
/// checkpoint and nothing else.
/// </para>
/// </remarks>
public enum CdcPositionSaveOutcome
{
	/// <summary>
	/// No store decided anything. The checkpoint must be treated as NOT saved.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Zero is deliberately not a success state, and this member exists to keep it that way.</b> An
	/// unassigned field, a <c>default</c> value, a task that completed without a result being set — every one
	/// of these produces the zero value, and a caller has no way to distinguish it from a value a store
	/// chose. If zero meant success, all of them would assert a durable checkpoint that was never written.
	/// </para>
	/// <para>
	/// <b>This is not an unreachable member.</b> It is produced by the language rather than by any store,
	/// which is precisely why it must be named.
	/// </para>
	/// </remarks>
	Unknown = 0,

	/// <summary>
	/// The store wrote the checkpoint: the stored position now is the position that was passed in.
	/// </summary>
	/// <remarks>
	/// <b>The only success value, and callers must test for it by name.</b> Deciding success by excluding the
	/// refusals means every member added later starts life as a success, which is the way this contract fails
	/// open.
	/// </remarks>
	Advanced = 1,

	/// <summary>
	/// The store declined to regress its checkpoint: the stored position is at or beyond the one offered, so
	/// nothing was written and the stored position is the later of the two.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This is a correct outcome, not a failure.</b> A store with a monotonic guard is choosing the safer
	/// of two wrong-looking results: a checkpoint moved backwards is re-delivery, and one moved forwards past
	/// what was actually processed is silent data loss. Declining the write preserves the stronger property.
	/// </para>
	/// <para>
	/// <b>A caller that reads this has learned something it cannot learn any other way: another writer is
	/// ahead of it on this consumer id.</b> That is the signal of a second processor on the same stream, and
	/// the caller's own in-memory position is behind the durable one. Treating it as success hides that;
	/// treating it as an error abandons a cycle that did real work.
	/// </para>
	/// <para>
	/// <b>Only a store that actually compared the two positions may report this.</b> A store with no guard
	/// performs an unconditional write and must report <see cref="Advanced"/>, because that is what it did.
	/// </para>
	/// </remarks>
	RefusedAsStale = 2,

	/// <summary>
	/// The store attempted the write and could not complete it because of concurrent activity on the same
	/// checkpoint. The write must be treated as NOT applied.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Distinct from <see cref="RefusedAsStale"/> because the two have opposite diagnoses.</b> A stale
	/// refusal is a decision the store made after reading both positions: it knows the stored one is later. A
	/// contended save is the absence of a decision — the store never established which position should win,
	/// so it cannot tell the caller that anything is ahead of it. Reporting contention as staleness would
	/// claim knowledge the store does not have, and would tell a lone processor that a second one exists.
	/// </para>
	/// <para>
	/// <b>It is reported rather than thrown because it is transient and self-correcting.</b> The next cycle
	/// offers a later position and supersedes this one, so the correct caller response is to carry on. This
	/// is also what keeps a provider's transport exception from crossing the abstraction: a consumer must be
	/// able to handle a contended checkpoint without referencing the driver that produced it.
	/// </para>
	/// </remarks>
	Contended = 3,
}
