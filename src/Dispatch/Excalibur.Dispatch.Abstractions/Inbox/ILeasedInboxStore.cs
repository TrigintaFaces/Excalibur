// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch;

/// <summary>
/// An optional capability for <see cref="IInboxStore"/> implementations that support a self-expiring
/// <b>lease</b> idempotency protocol: acquire a lease that expires on its own, hold it across handler
/// execution, then finalize under the term the acquisition returned.
/// </summary>
/// <remarks>
/// <para>
/// This is the self-governing counterpart to <see cref="IClaimableInboxStore"/>, whose claim never
/// auto-expires and whose TTL the caller governs. The two protocols are separate interfaces because a
/// store supports one, the other, both, or neither — and a caller must be able to tell which it holds
/// before it calls. Admission and expired-lease reclaim collapse into a <b>single</b> atomic
/// compare-and-set, making both the double-admission race and the permanently-stuck claim
/// inexpressible.
/// </para>
/// <para>
/// <b>A claim is a term.</b> Acquisition returns a <see cref="LeaseToken"/>, and every later write
/// carries it as a predicate the store enforces inside its own atomic step. A caller whose lease has
/// lapsed therefore cannot alter or finalize the record of the caller that replaced it: it presents the
/// term of the lease it lost, which matches no row. This is why the protocol does not rely on a status
/// predicate — at the instant of the bad write the entry is legitimately
/// <see cref="InboxStatus.Processing"/>, its successor's, so status protects the terminal <i>state</i>
/// and never the <i>term</i>.
/// </para>
/// <para>
/// <b>Every fenced operation returns a <see cref="bool"/>, never <see langword="void"/>.</b> A write
/// rejected for a stale term must be distinguishable from one that took effect; a silent no-op would
/// leave a caller believing it finalized a message it had already lost.
/// </para>
/// <para>
/// The lease-expiry comparison MUST be evaluated against the <b>store's own server clock</b> inside the
/// atomic operation — never an app-side clock — because the invariant is distributed across competing
/// processors and only the store's single clock is skew-free. The supplied duration governs the
/// app-side lease length only.
/// </para>
/// <para>
/// <b>Why the term is unique, and the one assumption that carries it.</b> Reclaim admits an entry only
/// when its recorded expiry is <i>strictly</i> earlier than the store clock, and the replacement expiry
/// is that same clock plus a non-negative duration — so a newly written term is always strictly greater
/// than the one it displaced, decided inside one atomic step. That argument holds because each store
/// call resolves the clock independently. A future change that batches an acquisition and its
/// finalization into <b>one shared transaction</b> would, on any store whose clock function is fixed for
/// the transaction's lifetime rather than evaluated per operation, freeze that clock, make two terms
/// compare equal, and silently stop the fence discriminating. Keep each operation on its own
/// transaction, or re-establish uniqueness by another means before batching them.
/// </para>
/// <para>
/// Only <see cref="InboxStatus.Processed"/> is terminal. A handler failure leaves the entry
/// <see cref="InboxStatus.Failed"/> and clears the lease term — a failed entry has no holder — so a
/// redelivery re-admits it for retry and a failure never silently drops the message.
/// </para>
/// </remarks>
public interface ILeasedInboxStore
{
	/// <summary>
	/// Atomically acquires a self-expiring lease on a message for a specific handler, returning the
	/// ownership term to present on finalization.
	/// </summary>
	/// <remarks>
	/// Succeeds when no entry exists, the entry is <see cref="InboxStatus.Received"/>, the entry is
	/// <see cref="InboxStatus.Failed"/> (re-admitted for retry), or the entry is
	/// <see cref="InboxStatus.Processing"/> with an expired lease (reclaiming a dead processor's stuck
	/// claim). A terminal <see cref="InboxStatus.Processed"/> entry is never reclaimed.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="leaseDuration">How long the lease is held before another processor may reclaim it.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// The <see cref="LeaseToken"/> identifying this holder when the caller acquired the lease, reclaimed
	/// an expired one, or re-admitted a previously <see cref="InboxStatus.Failed"/> entry for retry (it is
	/// now the sole processor); <see langword="null"/> if a live lease is held elsewhere, or the entry is
	/// terminal <see cref="InboxStatus.Processed"/>.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<LeaseToken?> TryAcquireLeaseAsync(
		string messageId,
		string handlerType,
		TimeSpan leaseDuration,
		CancellationToken cancellationToken);

	/// <summary>
	/// Finalizes a leased message as terminal <see cref="InboxStatus.Processed"/>, but only while
	/// <paramref name="lease"/> is still the entry's current term.
	/// </summary>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="lease">The term returned by <see cref="TryAcquireLeaseAsync"/>.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller still held the lease and the entry is now terminal;
	/// <see langword="false"/> if the lease had lapsed and been reclaimed, or the entry no longer exists —
	/// in which case the caller has lost its term and MUST NOT treat the message as finalized by it.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> CompleteAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		CancellationToken cancellationToken);

	/// <summary>
	/// Records a handler failure against a leased message, leaving it <see cref="InboxStatus.Failed"/> and
	/// re-admittable, but only while <paramref name="lease"/> is still the entry's current term.
	/// </summary>
	/// <remarks>
	/// Clears the entry's lease term on success: a failed entry has no holder, so it must not continue to
	/// carry one that a later comparison could match.
	/// </remarks>
	/// <param name="messageId">The unique identifier of the message.</param>
	/// <param name="handlerType">The fully qualified type name of the handler processing the message.</param>
	/// <param name="lease">The term returned by <see cref="TryAcquireLeaseAsync"/>.</param>
	/// <param name="errorMessage">A description of the handler failure.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// <see langword="true"/> if the caller still held the lease and the failure was recorded;
	/// <see langword="false"/> if the lease had lapsed and been reclaimed, or the entry no longer exists.
	/// </returns>
	/// <exception cref="ArgumentException">Thrown when <paramref name="messageId"/> or <paramref name="handlerType"/> is null or empty.</exception>
	ValueTask<bool> FailAsync(
		string messageId,
		string handlerType,
		LeaseToken lease,
		string errorMessage,
		CancellationToken cancellationToken);
}
