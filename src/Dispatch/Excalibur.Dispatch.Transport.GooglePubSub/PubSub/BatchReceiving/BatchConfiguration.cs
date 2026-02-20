// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Configuration options for batch receiving operations.
/// </summary>
public sealed class BatchConfiguration
{
	/// <summary>
	/// Gets or sets the maximum number of messages per batch.
	/// Default: 1000 (Pub/Sub API limit).
	/// </summary>
	/// <value>
	/// The maximum number of messages per batch.
	/// Default: 1000 (Pub/Sub API limit).
	/// </value>
	public int MaxMessagesPerBatch { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the minimum number of messages to wait for before returning a batch.
	/// Default: 1.
	/// </summary>
	/// <value>
	/// The minimum number of messages to wait for before returning a batch.
	/// Default: 1.
	/// </value>
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
	public long MaxBatchSizeBytes { get; set; } = 10 * 1024 * 1024; // 10MB

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable adaptive batch sizing. When enabled, batch size adjusts based on message throughput.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable adaptive batch sizing. When enabled, batch size adjusts based on message throughput.
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
	public int ConcurrentBatchProcessors { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to maintain message ordering within batches. When true, messages are processed in order within each batch.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to maintain message ordering within batches. When true, messages are processed in order within each batch.
	/// </value>
	public bool PreserveMessageOrder { get; set; }

	/// <summary>
	/// Gets or sets the acknowledgment deadline extension in seconds. Messages not processed within this time will be redelivered.
	/// </summary>
	/// <value>
	/// The acknowledgment deadline extension in seconds. Messages not processed within this time will be redelivered.
	/// </value>
	public int AckDeadlineSeconds { get; set; } = 600; // 10 minutes

	/// <summary>
	/// Gets or sets the batch acknowledgment strategy.
	/// </summary>
	/// <value>
	/// The batch acknowledgment strategy.
	/// </value>
	public BatchAckStrategy AckStrategy { get; set; } = BatchAckStrategy.OnSuccess;

	/// <summary>
	/// Gets or sets a value indicating whether gets or sets whether to enable batch compression for metrics.
	/// </summary>
	/// <value>
	/// A value indicating whether gets or sets whether to enable batch compression for metrics.
	/// </value>
	public bool EnableMetricsCompression { get; set; } = true;

	/// <summary>
	/// Validates the configuration settings.
	/// </summary>
	/// <exception cref="InvalidOperationException"> Thrown when configuration is invalid. </exception>
	public void Validate()
	{
		if (MaxMessagesPerBatch is <= 0 or > 1000)
		{
			throw new InvalidOperationException(
				"MaxMessagesPerBatch must be between 1 and 1000 (Pub/Sub API limit).");
		}

		if (MinMessagesPerBatch < 1 || MinMessagesPerBatch > MaxMessagesPerBatch)
		{
			throw new InvalidOperationException(
				"MinMessagesPerBatch must be between 1 and MaxMessagesPerBatch.");
		}

		if (MaxBatchWaitTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				"MaxBatchWaitTime must be greater than zero.");
		}

		if (MaxBatchSizeBytes <= 0)
		{
			throw new InvalidOperationException(
				"MaxBatchSizeBytes must be greater than zero.");
		}

		if (TargetBatchProcessingTime <= TimeSpan.Zero)
		{
			throw new InvalidOperationException(
				"TargetBatchProcessingTime must be greater than zero.");
		}

		if (ConcurrentBatchProcessors <= 0)
		{
			throw new InvalidOperationException(
				"ConcurrentBatchProcessors must be greater than zero.");
		}

		if (AckDeadlineSeconds is < 10 or > 600)
		{
			throw new InvalidOperationException(
				"AckDeadlineSeconds must be between 10 and 600.");
		}
	}

	/// <summary>
	/// Creates a copy of the configuration.
	/// </summary>
	public BatchConfiguration Clone() =>
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
			AckDeadlineSeconds = AckDeadlineSeconds,
			AckStrategy = AckStrategy,
			EnableMetricsCompression = EnableMetricsCompression,
		};
}
