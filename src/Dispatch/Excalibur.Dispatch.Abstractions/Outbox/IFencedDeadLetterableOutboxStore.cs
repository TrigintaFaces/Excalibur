// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch;

/// <summary>
/// An outbox store that can evaluate the leadership fence in the same atomic action that performs the
/// terminal dead-letter transition.
/// </summary>
/// <remarks>
/// The authority overload compares both tenure and acquisition in the same atomic action as the terminal
/// transition. The legacy token-only overload protects tenure only and cannot establish same-tenure claim
/// ownership. Framework workers use the authority overload; no default implementation may discard it.
/// </remarks>
public interface IFencedDeadLetterableOutboxStore
{
	/// <summary>Dead-letters a message only while the caller holds both accepted tenure and current claim.</summary>
	/// <param name="messageId">The persisted message identifier captured at acquisition.</param>
	/// <param name="reason">The reason for the terminal disposition.</param>
	/// <param name="attemptCount">Nonnegative observed logical attempt count; applied writes retain its maximum.</param>
	/// <param name="authority">The positive tenure and nonblank acquisition identity captured by the worker.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>The outcome decided atomically with the guarded terminal mutation.</returns>
	/// <remarks>
	/// Validate arguments before mutation. For valid authority evaluate the fence before row outcomes;
	/// absent/different/released claims return ClaimLost, retained terminal rows AlreadyTerminal and
	/// absent rows MessageNotFound. Do not reconstruct outcomes from a later unguarded read.
	/// </remarks>
	ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
		string messageId,
		string reason,
		int attemptCount,
		OutboxWriteAuthority authority,
		CancellationToken cancellationToken);

	/// <summary>
	/// Moves a message to the dead-letter destination, applying it only if the caller's tenure is still
	/// current.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message to dead-letter.</param>
	/// <param name="reason">The reason recorded against the dead-lettered message.</param>
	/// <param name="attemptCount">Observed logical delivery attempts. Nonnegative; an applied transition atomically records the maximum of this value and the stored count. Missing, terminal, and refused transitions do not change the count.</param>
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
	/// <exception cref="ArgumentOutOfRangeException">Thrown before mutation when <paramref name="attemptCount"/> is negative.</exception>
	ValueTask<OutboxCompletionOutcome> MarkDeadLetteredAsync(
		string messageId,
		string reason,
		int attemptCount,
		long fencingToken,
		CancellationToken cancellationToken);
}
