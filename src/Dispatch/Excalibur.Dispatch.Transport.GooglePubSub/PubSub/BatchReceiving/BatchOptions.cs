// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for batch receiving operations.
/// </summary>
public sealed class BatchOptions
{
	/// <summary>
	/// Gets or sets the maximum number of messages per batch.
	/// Default: 1000 (Pub/Sub API limit).
	/// </summary>
	/// <value>
	/// The maximum number of messages per batch.
	/// Default: 1000 (Pub/Sub API limit).
	/// </value>
	[Range(1, 1000)]
	public int MaxMessagesPerBatch { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the minimum number of messages to wait for before returning a batch.
	/// Default: 1.
	/// </summary>
	/// <value>
	/// The minimum number of messages to wait for before returning a batch.
	/// Default: 1.
	/// </value>
	[Range(1, 1000)]
	public int MinMessagesPerBatch { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum wait time for accumulating a batch.
	/// Default: 100ms.
	/// </summary>
	/// <value>
	/// The maximum wait time for accumulating a batch.
	/// Default: 100ms.
	/// </value>
	public TimeSpan MaxBatchWaitTime { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the maximum total size of messages in a batch (in bytes).
	/// Default: 10MB.
	/// </summary>
	/// <value>
	/// The maximum total size of messages in a batch (in bytes).
	/// Default: 10MB.
	/// </value>
	[Range(1L, long.MaxValue)]
	public long MaxBatchSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

	/// <summary>
	/// Gets or sets a value indicating whether to enable adaptive batch sizing.
	/// When enabled, batch size adjusts based on message throughput.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable adaptive batch sizing; otherwise, <see langword="false"/>.
	/// </value>
	public bool EnableAdaptiveBatching { get; set; } = true;

	/// <summary>
	/// Gets or sets the target processing time per batch. Used by adaptive batching to optimize batch sizes.
	/// </summary>
	/// <value>
	/// The target processing time per batch. Used by adaptive batching to optimize batch sizes.
	/// </value>
	public TimeSpan TargetBatchProcessingTime { get; set; } = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Gets or sets the number of concurrent batch processors.
	/// Default: Environment.ProcessorCount.
	/// </summary>
	/// <value>
	/// The number of concurrent batch processors.
	/// Default: Environment.ProcessorCount.
	/// </value>
	[Range(1, int.MaxValue)]
	public int ConcurrentBatchProcessors { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets a value indicating whether to maintain message ordering within batches.
	/// When true, messages are processed in order within each batch.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to maintain message ordering within batches; otherwise, <see langword="false"/>.
	/// </value>
	public bool PreserveMessageOrder { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable batch compression for metrics.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to enable batch compression for metrics; otherwise, <see langword="false"/>.
	/// </value>
	public bool EnableMetricsCompression { get; set; } = true;

	/// <summary>
	/// Gets or sets the acknowledgment configuration for batch receiving.
	/// </summary>
	/// <value>The batch acknowledgment options.</value>
	public BatchAcknowledgmentOptions Acknowledgment { get; set; } = new();

	/// <summary>
	/// Creates a copy of the configuration.
	/// </summary>
	/// <returns>A new <see cref="BatchOptions"/> instance with the same configuration.</returns>
	public BatchOptions Clone() =>
		new()
		{
			MaxMessagesPerBatch = MaxMessagesPerBatch,
			MinMessagesPerBatch = MinMessagesPerBatch,
			MaxBatchWaitTime = MaxBatchWaitTime,
			MaxBatchSizeBytes = MaxBatchSizeBytes,
			EnableAdaptiveBatching = EnableAdaptiveBatching,
			TargetBatchProcessingTime = TargetBatchProcessingTime,
			ConcurrentBatchProcessors = ConcurrentBatchProcessors,
			PreserveMessageOrder = PreserveMessageOrder,
			EnableMetricsCompression = EnableMetricsCompression,
			Acknowledgment = new BatchAcknowledgmentOptions
			{
				AckDeadlineSeconds = Acknowledgment.AckDeadlineSeconds,
				AckStrategy = Acknowledgment.AckStrategy,
			},
		};
}

/// <summary>
/// Configuration options for batch message acknowledgment behavior.
/// </summary>
public sealed class BatchAcknowledgmentOptions
{
	/// <summary>
	/// Gets or sets the acknowledgment deadline extension in seconds.
	/// Messages not processed within this time will be redelivered.
	/// </summary>
	/// <value>
	/// The acknowledgment deadline extension in seconds.
	/// Default: 600 (10 minutes).
	/// </value>
	[Range(10, 600)]
	public int AckDeadlineSeconds { get; set; } = 600; // 10 minutes

	/// <summary>
	/// Gets or sets the batch acknowledgment strategy.
	/// </summary>
	/// <value>
	/// The batch acknowledgment strategy.
	/// Default: <see cref="BatchAckStrategy.OnSuccess"/>.
	/// </value>
	public BatchAckStrategy AckStrategy { get; set; } = BatchAckStrategy.OnSuccess;
}
