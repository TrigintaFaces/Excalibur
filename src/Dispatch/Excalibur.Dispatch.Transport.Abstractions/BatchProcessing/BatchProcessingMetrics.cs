// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Metrics for batch processing.
/// </summary>
public sealed class BatchProcessingMetrics
{
	/// <summary>
	/// Gets or sets the total number of batches processed.
	/// </summary>
	/// <value>The current <see cref="TotalBatchesProcessed"/> value.</value>
	public long TotalBatchesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the total number of messages processed.
	/// </summary>
	/// <value>The current <see cref="TotalMessagesProcessed"/> value.</value>
	public long TotalMessagesProcessed { get; set; }

	/// <summary>
	/// Gets or sets the total number of successful messages.
	/// </summary>
	/// <value>The current <see cref="TotalSuccessfulMessages"/> value.</value>
	public long TotalSuccessfulMessages { get; set; }

	/// <summary>
	/// Gets or sets the total number of failed messages.
	/// </summary>
	/// <value>The current <see cref="TotalFailedMessages"/> value.</value>
	public long TotalFailedMessages { get; set; }

	/// <summary>
	/// Gets or sets the average batch size.
	/// </summary>
	/// <value>The current <see cref="AverageBatchSize"/> value.</value>
	public double AverageBatchSize { get; set; }

	/// <summary>
	/// Gets or sets the average processing time per batch.
	/// </summary>
	/// <value>The current <see cref="AverageProcessingTime"/> value.</value>
	public TimeSpan AverageProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the average processing time per message.
	/// </summary>
	/// <value>The current <see cref="AverageMessageProcessingTime"/> value.</value>
	public TimeSpan AverageMessageProcessingTime { get; set; }

	/// <summary>
	/// Gets or sets the current throughput (messages per second).
	/// </summary>
	/// <value>The current <see cref="CurrentThroughput"/> value.</value>
	public double CurrentThroughput { get; set; }

	/// <summary>
	/// Gets or sets the peak throughput.
	/// </summary>
	/// <value>The current <see cref="PeakThroughput"/> value.</value>
	public double PeakThroughput { get; set; }

	/// <summary>
	/// Gets or sets the success rate.
	/// </summary>
	/// <value>The current <see cref="SuccessRate"/> value.</value>
	public double SuccessRate { get; set; }

	/// <summary>
	/// Gets or sets the number of active batches.
	/// </summary>
	/// <value>The current <see cref="ActiveBatches"/> value.</value>
	public int ActiveBatches { get; set; }

	/// <summary>
	/// Gets or sets the queue depth (messages waiting to be batched).
	/// </summary>
	/// <value>The current <see cref="QueueDepth"/> value.</value>
	public int QueueDepth { get; set; }

	/// <summary>
	/// Gets or sets when metrics collection started.
	/// </summary>
	/// <value>The current <see cref="StartedAt"/> value.</value>
	public DateTimeOffset StartedAt { get; set; }

	/// <summary>
	/// Gets or sets when metrics were last updated.
	/// </summary>
	/// <value>The current <see cref="LastUpdatedAt"/> value.</value>
	public DateTimeOffset LastUpdatedAt { get; set; }

	/// <summary>
	/// Gets the uptime.
	/// </summary>
	/// <value>The current <see cref="Uptime"/> value.</value>
	public TimeSpan Uptime => DateTimeOffset.UtcNow - StartedAt;
}
