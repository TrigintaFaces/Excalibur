// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Defines the contract for batch processing strategies.
/// </summary>
public interface IBatchingStrategy
{
	/// <summary>
	/// Determines the next batch size based on current conditions.
	/// </summary>
	/// <param name="context"> The batching context. </param>
	/// <returns> The recommended batch size. </returns>
	int DetermineNextBatchSize(BatchingContext context);

	/// <summary>
	/// Records the result of a batch operation for strategy adjustment.
	/// </summary>
	/// <param name="result"> The batch result. </param>
	void RecordBatchResult(BatchResult result);

	/// <summary>
	/// Resets the strategy to its initial state.
	/// </summary>
	void Reset();
}
