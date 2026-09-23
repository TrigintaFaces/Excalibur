// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// What a claim-scoped outbox completion actually did, as decided by the store's own atomic action.
/// </summary>
/// <remarks>
/// <para>
/// <b>A completion that can decline must say so in its return, and this enumeration is that return.</b> The
/// members below are not shades of success: exactly one of them means the row was mutated, and a caller that
/// cannot tell them apart cannot tell a refusal from a write. A completion returning nothing makes
/// "it declined" and "it acted" the same observation, and no log line closes that gap — a log is readable by
/// an operator afterwards, never by the caller deciding what to do next.
/// </para>
/// <para>
/// <b>The refusals are reported, never thrown, and that is a liveness requirement rather than a taste.</b> The
/// drain reports a failure from inside its own exception handler, where a sibling <c>catch</c> cannot run; an
/// exception raised there escapes the whole cycle and abandons every message the caller still legitimately
/// holds. A refusal on one message must cost that message and nothing else.
/// </para>
/// </remarks>
public enum OutboxCompletionOutcome
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
	/// the exact failure this enumeration was introduced to end. It would have reappeared inside the remedy.
	/// </para>
	/// <para>
	/// <b>This is not an unreachable member.</b> It is produced by the language rather than by any store —
	/// which is precisely why it must be named: an undeclared zero would be just as reachable and harder to
	/// read about at the point where someone is deciding what to do with it.
	/// </para>
	/// </remarks>
	Unknown = 0,

	/// <summary>
	/// The store mutated the row: the caller held the claim it reported against, and the failure is recorded.
	/// </summary>
	/// <remarks>
	/// <b>The only success value, and callers must test for it by name.</b> Deciding success by excluding
	/// the refusals means every member added later starts life as a success, which is the way this contract
	/// fails open.
	/// </remarks>
	Applied = 1,

	/// <summary>
	/// The row is present and the caller no longer holds the claim it reported against — the message was
	/// re-claimed, by a later cycle of this process or by another.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>A lost claim requires NO leadership change at all, and that is why it is not a milder fence
	/// refusal.</b> One process can claim a message, fail, reclaim it and fail again with no handover
	/// occurring anywhere — the fencing token is identical across those cycles, so a fence cannot separate
	/// them and a claim identity is the only thing that can. The two conditions are ORTHOGONAL rather than
	/// points on a severity scale: either can arise without the other. Reading them as an ordering is what
	/// makes collapsing them into a single outcome look safe, and it is not, because the caller's correct
	/// response differs in each case.
	/// </para>
	/// <para>
	/// <b>This is ROW-scoped, and confusing it with a tenure-scoped refusal costs messages.</b> Only this one
	/// message moved on; the caller's other claims are intact, so it reports this message's outcome and
	/// carries on with the rest of its batch. A caller that abandons its drain cycle here strands messages it
	/// still owns and still has to complete.
	/// </para>
	/// <para>
	/// <b>The concrete way it arises, and the harm it does when reported as success.</b> A processor claims a
	/// message, its dispatch hangs past the reservation window, a later cycle re-claims the row, and the first
	/// cycle then returns to report a result about a claim that is gone. If the store answered that report
	/// with silence, the caller could not distinguish "declined" from "applied" and would proceed as though
	/// its decision took effect — so a message can be DEAD-LETTERED TWICE, by two cycles that each believe
	/// they exhausted its attempts, with both dead-letter rows recording a reason that is false. Reporting the
	/// refusal is what makes that unrepresentable.
	/// </para>
	/// </remarks>
	ClaimLost = 2,

	/// <summary>
	/// No row with that identifier was present when the statement ran — a terminal transition removed it, or
	/// it was never staged.
	/// </summary>
	/// <remarks>
	/// Distinct from <see cref="ClaimLost"/> because the two have opposite diagnoses and the store can tell
	/// them apart for free: a missing row means nothing is owed, whereas a present row the caller could not
	/// touch means someone else owns it. Collapsing them would report an ownership failure as an absence.
	/// </remarks>
	MessageNotFound = 3,

	/// <summary>
	/// A NEWER TENURE exists: the caller presented a fencing token below the scope high-water mark, and
	/// nothing was written.
	/// </summary>
	/// <remarks>
	/// <b>TENURE-scoped, and that is what separates it from <see cref="ClaimLost"/>.</b> A lost claim costs
	/// one message and the caller carries on with the rest of its batch. A fence refusal means every
	/// remaining write this caller would make is unfenced, so it must stop draining altogether and stand
	/// down. Treating one as the other either strands messages a live tenure owns, or lets a superseded
	/// tenure keep writing.
	/// </remarks>
	FenceRefused = 4,

	/// <summary>
	/// The row is present and already TERMINAL — delivered or dead-lettered — so the transition the caller
	/// asked for was refused because the decision it would reverse has already been taken and recorded.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>This member exists because without it every store had to answer a common situation with a value
	/// that was wrong in a different way.</b> A late completion arriving after a message has been delivered
	/// or buried is ordinary, not exceptional, and none of the four values above described it.
	/// <see cref="MessageNotFound"/> is a lie on a store whose terminal idiom KEEPS the row — the row is
	/// right there — though it is honest-by-parity on one whose terminal idiom DELETES it, which is why
	/// different stores reasonably reached different answers. <see cref="ClaimLost"/> says someone else owns
	/// the message, which is not what happened: nobody owns a terminal message.
	/// </para>
	/// <para>
	/// <b><see cref="Applied"/> is the dangerous answer, and it is the one a caller reaches for when the
	/// end state looks close enough.</b> On the dead-letter path the drain writes the external dead-letter
	/// entry BEFORE this mark and withdraws it on any outcome other than <see cref="Applied"/>. Answering
	/// <see cref="Applied"/> for a message that was SENT therefore leaves that message simultaneously
	/// delivered and sitting unreplayed in the dead-letter queue, where an operator draining the queue
	/// re-executes work that already succeeded. The distinction this member draws is exactly the one that
	/// prevents that.
	/// </para>
	/// <para>
	/// <b>Adding it is safe for every existing caller BY CONSTRUCTION</b>, and that is a property of the
	/// contract rather than luck: callers are required to test for <see cref="Applied"/> BY NAME, so any
	/// value they do not recognise — including this one — already means the write did not apply. A caller
	/// that decided success by excluding the known refusals would have treated every future member as a
	/// success, which is the way this contract fails open and the reason that rule exists.
	/// </para>
	/// <para>
	/// <b>A store that genuinely cannot distinguish this case must not guess.</b> Where the terminal idiom
	/// removes the row, the row really is absent and <see cref="MessageNotFound"/> is the accurate answer;
	/// reporting this value from a statement that never observed a terminal row would be describing a
	/// decision the store did not make.
	/// </para>
	/// </remarks>
	AlreadyTerminal = 5,
}
