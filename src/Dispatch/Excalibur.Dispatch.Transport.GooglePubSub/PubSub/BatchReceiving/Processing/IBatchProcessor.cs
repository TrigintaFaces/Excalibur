// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the contract for processing message batches.
/// </summary>
public interface IBatchProcessor
{
	/// <summary>
	/// Processes a batch of messages.
	/// </summary>
	/// <param name="batch"> The message batch to process. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> The processing result. </returns>
	Task<BatchProcessingResult> ProcessAsync(
		MessageBatch batch,
		CancellationToken cancellationToken);

	/// <summary>
	/// Processes multiple batches with a specified concurrency level.
	/// </summary>
	/// <param name="batches"> The batches to process. </param>
	/// <param name="maxConcurrency"> Maximum concurrent batch processing. </param>
	/// <param name="cancellationToken"> Cancellation token. </param>
	/// <returns> An async enumerable of processing results. </returns>
	IAsyncEnumerable<BatchProcessingResult> ProcessMultipleAsync(
		IAsyncEnumerable<MessageBatch> batches,
		int maxConcurrency,
		CancellationToken cancellationToken);
}
