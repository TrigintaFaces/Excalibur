// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Metrics for SQS channel processing.
/// </summary>
public sealed class SqsChannelMetrics
{
	/// <summary>
	/// Gets or sets the total messages processed.
	/// </summary>
	/// <value>
	/// The total messages processed.
	/// </value>
	public long TotalMessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the successful message count.
	/// </summary>
	/// <value>
	/// The successful message count.
	/// </value>
	public long SuccessfulMessages { get; set; }

	/// <summary>
	/// Gets or sets the failed message count.
	/// </summary>
	/// <value>
	/// The failed message count.
	/// </value>
	public long FailedMessages { get; set; }

	/// <summary>
	/// Gets or sets the average processing time in milliseconds.
	/// </summary>
	/// <value>
	/// The average processing time in milliseconds.
	/// </value>
	public double AverageProcessingTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the current throughput (messages per second).
	/// </summary>
	/// <value>
	/// The current throughput (messages per second).
	/// </value>
	public double CurrentThroughput { get; set; }

	/// <summary>
	/// Gets or sets the peak throughput.
	/// </summary>
	/// <value>
	/// The peak throughput.
	/// </value>
	public double PeakThroughput { get; set; }

	/// <summary>
	/// Gets or sets the current batch size.
	/// </summary>
	/// <value>
	/// The current batch size.
	/// </value>
	public int CurrentBatchSize { get; set; }

	/// <summary>
	/// Gets or sets the number of active workers.
	/// </summary>
	/// <value>
	/// The number of active workers.
	/// </value>
	public int ActiveWorkers { get; set; }

	/// <summary>
	/// Gets or sets the queue depth.
	/// </summary>
	/// <value>
	/// The queue depth.
	/// </value>
	public int QueueDepth { get; set; }

	/// <summary>
	/// Gets or sets when the metrics were last updated.
	/// </summary>
	/// <value>
	/// When the metrics were last updated.
	/// </value>
	public DateTimeOffset LastUpdated { get; set; } = DateTimeOffset.UtcNow;
}
