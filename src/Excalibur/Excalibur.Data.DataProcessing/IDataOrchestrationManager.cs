// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Defines methods for managing and processing data tasks for different record types.
/// </summary>
public interface IDataOrchestrationManager
{
	/// <summary>
	/// Adds a new data task for a specific record type.
	/// </summary>
	/// <param name="recordType"> The type of record for which the data task is created. </param>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A <see cref="Guid" /> representing the unique identifier of the created data task. </returns>
	/// <remarks>
	/// <para>
	/// THE QUEUE TABLE IS TRANSIENT, SO AN ABSENT ROW IS NOT EVIDENCE THE ENQUEUE FAILED. A task that
	/// completes is DELETED from the table; a task that fails keeps its row with its attempt count
	/// raised; a task that exhausts its attempts keeps its row and stops being selected, so a failed
	/// backfill stays visible for diagnosis. When a drain is running — a background service, or a
	/// scheduled job — a task whose processor succeeds can therefore be gone within one polling
	/// interval of being enqueued. Query by the returned identifier to tell an enqueue that never
	/// happened from one that has already been processed.
	/// </para>
	/// <para>
	/// The row is written to the configured schema and table, which default to
	/// <c>[DataProcessor].[DataTaskRequests]</c> rather than the connection's default schema, on the
	/// connection supplied to the registration — not necessarily the database the rest of the
	/// application uses.
	/// </para>
	/// <para>
	/// THE ENQUEUE PARTICIPATES IN AN AMBIENT TRANSACTION, deliberately, so that queueing work can be
	/// part of the business transaction that decided to queue it. A scope that is disposed without
	/// being completed therefore rolls the row back, and the enqueue itself reports no error. The
	/// drain refuses an ambient transaction instead, because processing must not be undone by one.
	/// </para>
	/// </remarks>
	Task<Guid> AddDataTaskForRecordTypeAsync(string recordType, CancellationToken cancellationToken);

	/// <summary>
	/// Processes pending data tasks, continuing independent tasks after individual failures.
	/// Successful tasks may complete before the operation reports a failure.
	/// </summary>
	/// <param name="cancellationToken"> A token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous operation. </returns>
	/// <exception cref="AggregateException">One or more tasks failed processing or cleanup.</exception>
	/// <exception cref="OperationCanceledException">The caller requested cancellation.</exception>
	ValueTask ProcessDataTasksAsync(CancellationToken cancellationToken);
}
