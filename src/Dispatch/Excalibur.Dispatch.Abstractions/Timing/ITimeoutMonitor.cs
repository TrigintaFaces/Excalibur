// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions;

/// <summary>
/// Monitors timeout events and provides timeout statistics for adaptive timeout calculations. R7.4: Timeout monitoring and adaptive timeout management.
/// </summary>
public interface ITimeoutMonitor
{
	/// <summary>
	/// Records the start of an operation for timeout monitoring.
	/// </summary>
	/// <param name="operationType"> The type of operation being monitored. </param>
	/// <param name="context"> Additional context for the operation. </param>
	/// <returns> A monitoring token to track the operation. </returns>
	ITimeoutOperationToken StartOperation(TimeoutOperationType operationType, TimeoutContext? context = null);

	/// <summary>
	/// Records the completion of an operation.
	/// </summary>
	/// <param name="token"> The monitoring token from StartOperation. </param>
	/// <param name="success"> Whether the operation completed successfully. </param>
	/// <param name="timedOut"> Whether the operation timed out. </param>
	void CompleteOperation(ITimeoutOperationToken token, bool success, bool timedOut);

	/// <summary>
	/// Gets timeout statistics for a specific operation type.
	/// </summary>
	/// <param name="operationType"> The operation type to get statistics for. </param>
	/// <returns> Timeout statistics for the operation type. </returns>
	TimeoutStatistics GetStatistics(TimeoutOperationType operationType);

	/// <summary>
	/// Gets the recommended timeout for an operation based on historical data.
	/// </summary>
	/// <param name="operationType"> The operation type to get a recommendation for. </param>
	/// <param name="percentile"> The percentile to use for the recommendation (e.g., 95 for 95th percentile). </param>
	/// <param name="context"> Additional context for the recommendation. </param>
	/// <returns> The recommended timeout duration. </returns>
	TimeSpan GetRecommendedTimeout(TimeoutOperationType operationType, int percentile = 95, TimeoutContext? context = null);

	/// <summary>
	/// Clears timeout statistics for a specific operation type or all operations.
	/// </summary>
	/// <param name="operationType"> The operation type to clear statistics for, or null for all operations. </param>
	void ClearStatistics(TimeoutOperationType? operationType = null);

	/// <summary>
	/// Gets the number of samples collected for an operation type.
	/// </summary>
	/// <param name="operationType"> The operation type to get sample count for. </param>
	/// <returns> The number of samples collected. </returns>
	int GetSampleCount(TimeoutOperationType operationType);

	/// <summary>
	/// Determines if enough samples have been collected for reliable adaptive timeout calculations.
	/// </summary>
	/// <param name="operationType"> The operation type to check. </param>
	/// <param name="minimumSamples"> The minimum number of samples required. </param>
	/// <returns> True if enough samples have been collected; otherwise, false. </returns>
	bool HasSufficientSamples(TimeoutOperationType operationType, int minimumSamples = 100);
}
