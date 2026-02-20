// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.DynamoDb.Cdc;

/// <summary>
/// Defines the contract for processing DynamoDB Streams CDC events.
/// </summary>
public interface IDynamoDbCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts continuous CDC processing until cancellation.
	/// </summary>
	/// <param name="eventHandler">The handler for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task that completes when processing is stopped.</returns>
	/// <remarks>
	/// <para>
	/// This method processes changes from all active shards concurrently.
	/// New shards from resharding are automatically discovered when enabled.
	/// </para>
	/// <para>
	/// Position is confirmed after each successfully processed event.
	/// Use <see cref="ProcessBatchAsync"/> for manual batch control.
	/// </para>
	/// </remarks>
	Task StartAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of changes and returns.
	/// </summary>
	/// <param name="eventHandler">The handler for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The number of events processed in this batch.</returns>
	/// <remarks>
	/// Use this method for serverless scenarios where you want explicit
	/// control over batch processing. Position must be confirmed manually
	/// via <see cref="ConfirmPositionAsync"/>.
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<DynamoDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current (uncommitted) position across all shards.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current position.</returns>
	Task<DynamoDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms that all events up to the given position have been processed.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// This persists the position to the state store. On restart,
	/// processing will resume from this confirmed position.
	/// </remarks>
	Task ConfirmPositionAsync(
		DynamoDbCdcPosition position,
		CancellationToken cancellationToken);
}
