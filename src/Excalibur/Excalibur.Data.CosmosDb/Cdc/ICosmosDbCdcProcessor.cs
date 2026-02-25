// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.CosmosDb.Cdc;

/// <summary>
/// Defines the contract for processing CosmosDb Change Feed events.
/// </summary>
/// <remarks>
/// <para>
/// Supports two processing modes:
/// </para>
/// <list type="bullet">
/// <item>
/// <term>Continuous</term>
/// <description>Use <see cref="StartAsync"/> for long-running background processing.</description>
/// </item>
/// <item>
/// <term>Batch</term>
/// <description>Use <see cref="ProcessBatchAsync"/> for serverless or on-demand scenarios.</description>
/// </item>
/// </list>
/// </remarks>
public interface ICosmosDbCdcProcessor : IAsyncDisposable, IDisposable
{
	/// <summary>
	/// Starts continuous Change Feed processing.
	/// </summary>
	/// <param name="eventHandler">The handler invoked for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task that completes when processing stops due to cancellation.</returns>
	/// <remarks>
	/// This method runs continuously until cancelled. Position is automatically
	/// confirmed after successful handler execution.
	/// </remarks>
	Task StartAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes a single batch of changes from the Change Feed.
	/// </summary>
	/// <param name="eventHandler">The handler invoked for each change event.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The number of events processed in this batch.</returns>
	/// <remarks>
	/// Use this method for serverless scenarios (Azure Functions) or when you need
	/// fine-grained control over processing. Position is automatically confirmed
	/// after the batch completes successfully.
	/// </remarks>
	Task<int> ProcessBatchAsync(
		Func<CosmosDbDataChangeEvent, CancellationToken, Task> eventHandler,
		CancellationToken cancellationToken);

	/// <summary>
	/// Gets the current position in the Change Feed.
	/// </summary>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>The current position, or a beginning position if none exists.</returns>
	Task<CosmosDbCdcPosition> GetCurrentPositionAsync(CancellationToken cancellationToken);

	/// <summary>
	/// Confirms a position, persisting it to the state store.
	/// </summary>
	/// <param name="position">The position to confirm.</param>
	/// <param name="cancellationToken">A token to observe for cancellation requests.</param>
	/// <returns>A task representing the asynchronous operation.</returns>
	/// <remarks>
	/// Normally position confirmation is automatic. Use this method when you need
	/// manual control over position persistence, such as after processing a batch
	/// of events atomically.
	/// </remarks>
	Task ConfirmPositionAsync(
		CosmosDbCdcPosition position,
		CancellationToken cancellationToken);
}
