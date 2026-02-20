// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.Postgres.Cdc;

/// <summary>
/// Defines the contract for a Postgres Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// The processor uses Postgres logical replication with the pgoutput protocol
/// to capture row-level changes from the database.
/// </para>
/// <para>
/// Server requirements:
/// <list type="bullet">
/// <item><description>wal_level = logical</description></item>
/// <item><description>A publication for the tables to capture</description></item>
/// <item><description>A replication slot (created automatically if AutoCreateSlot is true)</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IPostgresCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts processing CDC changes and invokes the event handler for each change.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each <see cref="PostgresDataChangeEvent"/>.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the processing loop. Completes when cancelled.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="eventHandler"/> is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when processing is cancelled.</exception>
	/// <remarks>
	/// <para>
	/// This method runs continuously until cancelled. It automatically handles:
	/// <list type="bullet">
	/// <item><description>Connection establishment and reconnection</description></item>
	/// <item><description>Position tracking and checkpointing</description></item>
	/// <item><description>Transaction boundaries</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// The event handler is invoked for each change within a transaction.
	/// If the handler throws, processing stops and the exception is propagated.
	/// </para>
	/// </remarks>
	Task StartAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of CDC changes and returns.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each <see cref="PostgresDataChangeEvent"/>.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The number of events processed in this batch.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="eventHandler"/> is null.</exception>
	/// <remarks>
	/// <para>
	/// Use this method for manual/scheduled processing (e.g., from Azure Functions).
	/// For continuous processing, use <see cref="StartAsync"/> instead.
	/// </para>
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<PostgresDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current processing position.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current LSN position being processed.</returns>
	Task<PostgresCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that changes up to the specified position have been processed.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the confirmation operation.</returns>
	/// <remarks>
	/// This advances the replication slot's confirmed position, allowing Postgres
	/// to reclaim WAL space. Call this after successfully processing a batch.
	/// </remarks>
	Task ConfirmPositionAsync(
		PostgresCdcPosition position,
		CancellationToken cancellationToken);
}
