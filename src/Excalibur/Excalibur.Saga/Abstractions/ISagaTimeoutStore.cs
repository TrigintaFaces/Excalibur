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
/// The <see cref="GetDueTimeoutsAsync"/> method is called periodically by a background
/// service to poll for timeouts ready for delivery.
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
	/// Retrieves all timeouts that are due for delivery as of the specified time.
	/// </summary>
	/// <param name="asOf">The reference time for determining which timeouts are due.</param>
	/// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
	/// <returns>
	/// A read-only list of timeouts where <see cref="SagaTimeout.DueAt"/> is less than
	/// or equal to <paramref name="asOf"/>, ordered by <see cref="SagaTimeout.DueAt"/> ascending.
	/// </returns>
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
