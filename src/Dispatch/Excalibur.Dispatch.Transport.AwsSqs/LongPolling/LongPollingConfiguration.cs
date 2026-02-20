// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration for long polling operations.
/// </summary>
public sealed class LongPollingConfiguration
{
	/// <summary>
	/// Gets or sets maximum wait time for long polling in seconds.
	/// </summary>
	/// <value>
	/// Maximum wait time for long polling in seconds
	/// </value>
	public int MaxWaitTimeSeconds { get; set; } = 20;

	/// <summary>
	/// Gets or sets minimum wait time for long polling in seconds.
	/// </summary>
	/// <value>
	/// Minimum wait time for long polling in seconds
	/// </value>
	public int MinWaitTimeSeconds { get; set; } = 1;

	/// <summary>
	/// Gets or sets maximum number of messages to retrieve.
	/// </summary>
	/// <value>
	/// Maximum number of messages to retrieve
	/// </value>
	public int MaxNumberOfMessages { get; set; } = 10;

	/// <summary>
	/// Gets or sets visibility timeout in seconds.
	/// </summary>
	/// <value>
	/// Visibility timeout in seconds
	/// </value>
	public int VisibilityTimeoutSeconds { get; set; } = 30;

	/// <summary>
	/// Gets or sets a value indicating whether to enable adaptive polling.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable adaptive polling
	/// </value>
	public bool EnableAdaptivePolling { get; set; } = true;

	/// <summary>
	/// Gets or sets polling interval in milliseconds.
	/// </summary>
	/// <value>
	/// Polling interval in milliseconds
	/// </value>
	public int PollingIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets maximum retry attempts.
	/// </summary>
	/// <value>
	/// Maximum retry attempts
	/// </value>
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets retry delay in milliseconds.
	/// </summary>
	/// <value>
	/// Retry delay in milliseconds
	/// </value>
	public int RetryDelayMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets queue URL for polling.
	/// </summary>
	/// <value>
	/// Queue URL for polling
	/// </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to delete messages after processing.
	/// </summary>
	/// <value>
	/// A value indicating whether to delete messages after processing
	/// </value>
	public bool DeleteAfterProcessing { get; set; } = true;

	/// <summary>
	/// Gets or sets batch size for processing.
	/// </summary>
	/// <value>
	/// Batch size for processing
	/// </value>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets timeout for processing a single message.
	/// </summary>
	/// <value>
	/// Timeout for processing a single message
	/// </value>
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets a value indicating whether to use FIFO queue.
	/// </summary>
	/// <value>
	/// A value indicating whether to use FIFO queue
	/// </value>
	public bool IsFifoQueue { get; set; }

	/// <summary>
	/// Gets or sets receive message group ID for FIFO queues.
	/// </summary>
	/// <value>
	/// Receive message group ID for FIFO queues
	/// </value>
	public string? ReceiveRequestAttemptId { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to enable request coalescing for optimization.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable request coalescing for optimization
	/// </value>
	public bool EnableRequestCoalescing { get; set; } = true;

	/// <summary>
	/// Gets or sets time window for coalescing requests in milliseconds.
	/// </summary>
	/// <value>
	/// Time window for coalescing requests in milliseconds
	/// </value>
	public int CoalescingWindow { get; set; } = 100;

	/// <summary>
	/// Gets or sets message attributes to retrieve.
	/// </summary>
	/// <value>
	/// Message attributes to retrieve
	/// </value>
	public string[] MessageAttributeNames { get; set; } = ["All"];

	/// <summary>
	/// Gets or sets system message attributes to retrieve.
	/// </summary>
	/// <value>
	/// System message attributes to retrieve
	/// </value>
	public string[] SystemAttributeNames { get; set; } = ["All"];

	/// <summary>
	/// Gets maximum wait time for long polling (TimeSpan wrapper).
	/// </summary>
	/// <value>
	/// Maximum wait time for long polling (TimeSpan wrapper)
	/// </value>
	public TimeSpan MaxWaitTime => TimeSpan.FromSeconds(MaxWaitTimeSeconds);

	/// <summary>
	/// Gets or sets maximum number of messages to receive in a single polling operation.
	/// </summary>
	/// <value>
	/// Maximum number of messages to receive in a single polling operation
	/// </value>
	public int MaxMessagesPerReceive { get; set; } = 10;

	/// <summary>
	/// Gets or sets time window for adaptation calculations in seconds.
	/// </summary>
	/// <value>
	/// Time window for adaptation calculations in seconds
	/// </value>
	public TimeSpan AdaptationWindow { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets smoothing factor for adaptive calculations (0.0 to 1.0).
	/// </summary>
	/// <value>
	/// Smoothing factor for adaptive calculations (0.0 to 1.0)
	/// </value>
	public double SmoothingFactor { get; set; } = 0.3;

	/// <summary>
	/// Gets or sets high load threshold for adaptive polling.
	/// </summary>
	/// <value>
	/// High load threshold for adaptive polling
	/// </value>
	public double HighLoadThreshold { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets low load threshold for adaptive polling.
	/// </summary>
	/// <value>
	/// Low load threshold for adaptive polling
	/// </value>
	public double LowLoadThreshold { get; set; } = 0.2;

	/// <summary>
	/// Gets minimum wait time for adaptive polling.
	/// </summary>
	/// <value>
	/// Minimum wait time for adaptive polling
	/// </value>
	public TimeSpan MinWaitTime => TimeSpan.FromSeconds(MinWaitTimeSeconds);

	/// <summary>
	/// Gets or sets a value indicating whether to enable visibility timeout optimization.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable visibility timeout optimization
	/// </value>
	public bool EnableVisibilityTimeoutOptimization { get; set; } = true;

	/// <summary>
	/// Gets or sets buffer factor for visibility timeout calculations (0.0 to 1.0).
	/// </summary>
	/// <value>
	/// Buffer factor for visibility timeout calculations (0.0 to 1.0)
	/// </value>
	public double VisibilityTimeoutBufferFactor { get; set; } = 0.1;

	/// <summary>
	/// Validates the configuration.
	/// </summary>
	/// <exception cref="ArgumentOutOfRangeException"></exception>
	/// <exception cref="ArgumentException"></exception>
	public void Validate()
	{
		if (MaxWaitTimeSeconds is < 0 or > 20)
		{
			throw new ArgumentOutOfRangeException(nameof(MaxWaitTimeSeconds), "Must be between 0 and 20 seconds");
		}

		if (MinWaitTimeSeconds < 0 || MinWaitTimeSeconds > MaxWaitTimeSeconds)
		{
			throw new ArgumentOutOfRangeException(nameof(MinWaitTimeSeconds), "Must be between 0 and MaxWaitTimeSeconds");
		}

		if (MaxNumberOfMessages is < 1 or > 10)
		{
			throw new ArgumentOutOfRangeException(nameof(MaxNumberOfMessages), "Must be between 1 and 10");
		}

		if (VisibilityTimeoutSeconds is < 0 or > 43200)
		{
			throw new ArgumentOutOfRangeException(nameof(VisibilityTimeoutSeconds), "Must be between 0 and 43200 seconds (12 hours)");
		}

		if (QueueUrl == null)
		{
			throw new ArgumentException("Queue URL cannot be empty", nameof(QueueUrl));
		}
	}
}
