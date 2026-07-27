// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Abstractions;

/// <summary>
/// Provides persistent storage for saga timeout requests. Implementations enable
/// sagas to schedule timeout messages that are delivered after a specified delay.
/// </summary>
/// <remarks>
/// <para>
/// Timeout stores must be reliable across process restarts. When a process restarts,
/// previously scheduled timeouts must still be delivered. This is achieved by persisting
/// timeout metadata to durable storage (e.g., SQL Server, Redis, MongoDB).
/// </para>
/// <para>
/// The <see cref="ClaimDueTimeoutsAsync"/> method is called periodically by a background
/// service to poll for timeouts ready for delivery. It atomically leases due timeouts to the
/// calling processor so that, under a multi-instance deployment, each due timeout is claimed
/// and delivered by exactly one processor at a time.
/// </para>
/// <para>
/// <see cref="GetDueTimeoutsAsync"/> and <see cref="ClaimDueTimeoutsAsync"/> are deliberately
/// distinct operations. <see cref="GetDueTimeoutsAsync"/> is a read-only diagnostic query used
/// by monitoring and dashboard tooling to observe due timeouts without side effects; it never
/// leases or mutates state. <see cref="ClaimDueTimeoutsAsync"/> is the delivery-path operation
/// that atomically leases timeouts so exactly one processor delivers each one. Callers driving
/// delivery must use <see cref="ClaimDueTimeoutsAsync"/>, never <see cref="GetDueTimeoutsAsync"/>.
/// A future <c>ISagaTimeoutStoreAdmin</c> split could separate the diagnostic surface from the
/// delivery surface if the interface grows further.
/// </para>
/// </remarks>
public interface ISagaTimeoutStore
{
	/// <summary>
	/// Schedules a timeout for delivery at the specified due time.
	/// </summary>
	/// <param name="timeout">The timeout to schedule.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous scheduling operation.</returns>
	Task ScheduleTimeoutAsync(SagaTimeout timeout, CancellationToken cancellationToken);

	/// <summary>
	/// Cancels a specific timeout by its identifier.
	/// </summary>
	/// <param name="sagaId">The saga identifier that owns the timeout.</param>
	/// <param name="timeoutId">The unique timeout identifier to cancel.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous cancellation operation.</returns>
	/// <remarks>
	/// Cancellation is idempotent. Cancelling a non-existent or already-delivered
	/// timeout completes without error.
	/// </remarks>
	Task CancelTimeoutAsync(string sagaId, string timeoutId, CancellationToken cancellationToken);

	/// <summary>
	/// Cancels all pending timeouts for a saga instance.
	/// </summary>
	/// <param name="sagaId">The saga identifier whose timeouts should be cancelled.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous cancellation operation.</returns>
	/// <remarks>
	/// This method is called when a saga completes or is terminated to clean up
	/// any pending timeouts that are no longer needed.
	/// </remarks>
	Task CancelAllTimeoutsAsync(string sagaId, CancellationToken cancellationToken);

	/// <summary>
	/// Atomically claims up to <paramref name="batchSize"/> timeouts that are due for delivery
	/// as of the specified time, leasing them to this processor so that no other processor can
	/// claim the same timeout while the lease is held.
	/// </summary>
	/// <param name="asOf">The reference time for determining which timeouts are due.</param>
	/// <param name="batchSize">The maximum number of timeouts to claim in this call.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of newly claimed timeouts where <see cref="SagaTimeout.DueAt"/> is less
	/// than or equal to <paramref name="asOf"/>, ordered by <see cref="SagaTimeout.DueAt"/>
	/// ascending. Only timeouts that were unclaimed, or whose previous lease has expired, are
	/// returned.
	/// </returns>
	/// <remarks>
	/// <para>
	/// A claimed timeout is leased to the calling processor for an implementation-defined lease
	/// duration. Two concurrent calls to this method, from the same or different processor
	/// instances, never return the same timeout while its lease is active -- this is what makes
	/// delivery exactly-once-at-a-time under a multi-instance deployment.
	/// </para>
	/// <para>
	/// If the processor crashes (or otherwise fails to call <see cref="MarkDeliveredAsync"/>)
	/// after claiming a timeout, the lease expires and another processor may re-claim and
	/// re-deliver it. This preserves the store's documented at-least-once delivery guarantee --
	/// consumers of delivered timeouts must remain idempotent.
	/// </para>
	/// </remarks>
	Task<IReadOnlyList<SagaTimeout>> ClaimDueTimeoutsAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken);

	/// <summary>
	/// Returns the timeouts that are due for delivery as of the specified time, without leasing
	/// or claiming them.
	/// </summary>
	/// <param name="asOf">The reference time for determining which timeouts are due.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of timeouts where <see cref="SagaTimeout.DueAt"/> is less than or equal
	/// to <paramref name="asOf"/>, ordered by <see cref="SagaTimeout.DueAt"/> ascending.
	/// </returns>
	/// <remarks>
	/// This is a read-only diagnostic query intended for monitoring and dashboard tooling to
	/// observe stuck or overdue timeouts. It does not lease or mutate any state, and repeated
	/// calls may return the same timeouts. Callers driving delivery must use
	/// <see cref="ClaimDueTimeoutsAsync"/> instead.
	/// </remarks>
	Task<IReadOnlyList<SagaTimeout>> GetDueTimeoutsAsync(DateTimeOffset asOf, CancellationToken cancellationToken);

	/// <summary>
	/// Marks a timeout as delivered, removing it from the pending timeout list.
	/// </summary>
	/// <param name="timeoutId">The unique timeout identifier that was delivered.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This method is called after a timeout message has been successfully dispatched
	/// to the saga handler. Marking delivery is idempotent.
	/// </remarks>
	Task MarkDeliveredAsync(string timeoutId, CancellationToken cancellationToken);
}
