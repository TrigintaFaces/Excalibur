// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch;

/// <summary>
/// An outbox store that can evaluate the leadership fence in the same atomic action that performs the
/// terminal dead-letter transition.
/// </summary>
/// <remarks>
/// <para>
/// <b>This carries the fencing token and NOT a claim identity, and the asymmetry with the failure-report
/// contract is deliberate.</b> The harm this closes is CROSS-TENURE: a superseded leader that reaches its
/// attempt ceiling moves a message to the dead-letter table and destroys the outbox row, while a live
/// successor still holds that message and has not delivered it. A fencing token is exactly what refuses
/// that. A claim identity would additionally refuse a stale cycle of the SAME process — a real but
/// different failure, and not the one that makes dead-lettering lossy.
/// </para>
/// <para>
/// <b>Requiring both terms would have narrowed the fix from six candidate stores to one.</b> Every store
/// that participates in fencing can honour this member; only one also records a per-claim identity. Since
/// the loss is cross-tenure, demanding the claim term would have bought a residual and cost five stores
/// the primary protection. The claim-bearing refinement stays available as a later addition where a store
/// supports it, without breaking this contract.
/// </para>
/// <para>
/// <b>Declares no base interfaces, and that is a confidentiality requirement.</b> Deriving from the
/// fencing contract would pull in the payload-bearing store surface transitively, which a decorator
/// enforcing a confidentiality boundary cannot forward unmediated — and a denied capability is silently
/// absent for exactly the consumers who enabled encryption. Nothing here moves a payload: an identifier, a
/// reason string and an opaque token.
/// </para>
/// <para>
/// Discovered through <see cref="IServiceProvider.GetService(System.Type)"/>, never by casting the store,
/// because a cast sees only the outermost type and is lossy through any decorator.
/// </para>
/// </remarks>
public interface IFencedDeadLetterableOutboxStore
{
	/// <summary>
	/// Moves a message to the dead-letter destination, applying it only if the caller's tenure is still
	/// current.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">The reason recorded against the dead-lettered message.</param>
	/// <param name="fencingToken">The monotonic token identifying the caller's leadership tenure.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>What the store did, decided by the same atomic action that performed the transition.</returns>
	/// <remarks>
	/// <para>
	/// <b>Implementations MUST evaluate the fence and the transition in ONE atomic action.</b> On a store
	/// whose terminal idiom destroys the row, the transition is irreversible: there is no later read that
	/// can establish whether the caller was entitled to make it, because the evidence is what was deleted.
	/// A fence judged in a separate round trip leaves a window in which a successor advances the mark
	/// between the check and the delete.
	/// </para>
	/// <para>
	/// <b>The refusal is REPORTED, never thrown.</b> The drains reach this member from inside their own
	/// exception handlers, where a sibling handler on the same block cannot run, so an exception raised
	/// here escapes the cycle and abandons every message the caller still holds.
	/// <see cref="OutboxCompletionOutcome.ClaimLost"/> is not reachable from this member — it carries no
	/// claim term — and an implementation returning it would be reporting a decision it did not make.
	/// </para>
	/// </remarks>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> is null or empty.</exception>
	ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
		string messageId,
		string reason,
		long fencingToken,
		CancellationToken cancellationToken);
}
