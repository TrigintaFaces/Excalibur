// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


namespace Excalibur.Data.DataProcessing;

/// <summary>
/// Defines a contract for processing data tasks.
/// </summary>
public interface IDataProcessor : IAsyncDisposable
{
	/// <summary>
	/// Executes the data processing pipeline.
	/// </summary>
	/// <param name="completedCount"> The count of records that have already been processed, used to resume from a specific point. </param>
	/// <param name="processedCursor">
	/// The opaque cursor identifying the last durably processed page boundary,
	/// or <see langword="null"/> when starting from the beginning. On crash recovery
	/// the fetch cursor resets to this value. Records within an incomplete page can be replayed;
	/// handlers must tolerate duplicate effects.
	/// </param>
	/// <param name="updateCompletedCount"> A delegate for updating the count of completed records and cursor in the data task. </param>
	/// <param name="cancellationToken"> A token to signal the cancellation of the processing operation. </param>
	/// <returns>The completed count after normal source exhaustion and successful processing.</returns>
	/// <remarks>Failures and cancellation must propagate; returning successfully permits the orchestrator to delete the task.</remarks>
	Task<long> RunAsync(long completedCount, string? processedCursor, UpdateCompletedCount updateCompletedCount, CancellationToken cancellationToken);
}
