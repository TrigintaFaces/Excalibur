// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.MongoDB.Cdc;

/// <summary>
/// Defines the contract for a MongoDB Change Data Capture (CDC) processor.
/// </summary>
/// <remarks>
/// <para>
/// The processor uses MongoDB Change Streams to capture document changes
/// from a replica set or sharded cluster.
/// </para>
/// <para>
/// Server requirements:
/// <list type="bullet">
/// <item><description>MongoDB 3.6+ for collection-level change streams</description></item>
/// <item><description>MongoDB 4.0+ for database-level or cluster-level change streams</description></item>
/// <item><description>MongoDB 6.0+ for pre-image support (FullDocumentBeforeChange)</description></item>
/// <item><description>Must be a replica set or sharded cluster (not standalone)</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IMongoDbCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts processing CDC changes and invokes the event handler for each change.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each <see cref="MongoDbDataChangeEvent"/>.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the processing loop. Completes when cancelled.</returns>
	/// <exception cref="ArgumentNullException">Thrown if <paramref name="eventHandler"/> is null.</exception>
	/// <exception cref="OperationCanceledException">Thrown when processing is cancelled.</exception>
	/// <remarks>
	/// <para>
	/// This method runs continuously until cancelled. It automatically handles:
	/// <list type="bullet">
	/// <item><description>Connection establishment and reconnection</description></item>
	/// <item><description>Resume token tracking and checkpointing</description></item>
	/// <item><description>Invalidation recovery (when possible)</description></item>
	/// </list>
	/// </para>
	/// <para>
	/// The event handler is invoked for each change event.
	/// If the handler throws, processing stops and the exception is propagated.
	/// </para>
	/// </remarks>
	Task StartAsync(
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of CDC changes and returns.
	/// </summary>
	/// <param name="eventHandler">A delegate that handles each <see cref="MongoDbDataChangeEvent"/>.</param>
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
		Func<MongoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current processing position.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current resume token position being processed.</returns>
	Task<MongoDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that changes up to the specified position have been processed.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the confirmation operation.</returns>
	/// <remarks>
	/// This persists the resume token, allowing the change stream to resume
	/// from this point after a restart.
	/// </remarks>
	Task ConfirmPositionAsync(
		MongoDbCdcPosition position,
		CancellationToken cancellationToken);
}
