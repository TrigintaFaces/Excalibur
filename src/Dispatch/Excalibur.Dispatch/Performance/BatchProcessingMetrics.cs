// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Batch processing performance metrics.
/// </summary>
public sealed record BatchProcessingMetrics
{
	/// <summary>
	/// Gets the total number of batches processed.
	/// </summary>
	/// <value> The count of batches observed during the sampling window. </value>
	public required int TotalBatches { get; init; }

	/// <summary>
	/// Gets the total number of items processed across all batches.
	/// </summary>
	/// <value> The aggregate number of items handled across all batches. </value>
	public required int TotalItemsProcessed { get; init; }

	/// <summary>
	/// Gets the total processing time for all batches.
	/// </summary>
	/// <value> The cumulative processing duration. </value>
	public required TimeSpan TotalProcessingTime { get; init; }

	/// <summary>
	/// Gets the average batch size.
	/// </summary>
	/// <value> The mean number of items per batch. </value>
	public required double AverageBatchSize { get; init; }

	/// <summary>
	/// Gets the average processing time per batch.
	/// </summary>
	/// <value> The mean duration required to process a batch. </value>
	public required TimeSpan AverageProcessingTimePerBatch { get; init; }

	/// <summary>
	/// Gets the average processing time per item.
	/// </summary>
	/// <value> The average latency per processed item. </value>
	public required TimeSpan AverageProcessingTimePerItem { get; init; }

	/// <summary>
	/// Gets the average degree of parallelism used.
	/// </summary>
	/// <value> The average parallel worker count engaged per batch. </value>
	public required double AverageParallelDegree { get; init; }

	/// <summary>
	/// Gets the overall success rate across all batches.
	/// </summary>
	/// <value> The ratio of successful items to total processed items. </value>
	public required double OverallSuccessRate { get; init; }

	/// <summary>
	/// Gets the throughput in items per second.
	/// </summary>
	/// <value> The processing throughput expressed as items per second. </value>
	public required double ThroughputItemsPerSecond { get; init; }
}
