// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for long polling operations. Replaces LongPollingConfiguration
/// with sub-option composition following the Microsoft Options pattern.
/// </summary>
public sealed class LongPollingOptions
{
	/// <summary>Gets or sets the queue URL for polling.</summary>
	[Required]
	public Uri? QueueUrl { get; set; }

	/// <summary>Gets or sets a value indicating whether to use FIFO queue.</summary>
	public bool IsFifoQueue { get; set; }

	/// <summary>Gets or sets receive message group ID for FIFO queues.</summary>
	public string? ReceiveRequestAttemptId { get; set; }

	/// <summary>Gets or sets a value indicating whether to delete messages after processing.</summary>
	public bool DeleteAfterProcessing { get; set; } = true;

	/// <summary>Gets the polling configuration.</summary>
	public LongPollingTimingOptions Polling { get; } = new();

	/// <summary>Gets the visibility timeout configuration.</summary>
	public LongPollingVisibilityOptions Visibility { get; } = new();

	/// <summary>Gets the processing configuration.</summary>
	public LongPollingProcessingOptions Processing { get; } = new();

	/// <summary>Gets the retry configuration.</summary>
	public LongPollingRetryOptions Retry { get; } = new();

	/// <summary>Gets the adaptive polling configuration.</summary>
	public LongPollingAdaptiveOptions Adaptive { get; } = new();

	/// <summary>Gets the message attribute configuration.</summary>
	public LongPollingAttributeOptions Attributes { get; } = new();
}

/// <summary>Polling timing configuration.</summary>
public sealed class LongPollingTimingOptions
{
	/// <summary>Gets or sets maximum wait time for long polling in seconds (0-20).</summary>
	[Range(0, 20)]
	public int MaxWaitTimeSeconds { get; set; } = 20;

	/// <summary>Gets or sets minimum wait time for long polling in seconds.</summary>
	public int MinWaitTimeSeconds { get; set; } = 1;

	/// <summary>Gets or sets maximum number of messages to retrieve (1-10).</summary>
	[Range(1, 10)]
	public int MaxNumberOfMessages { get; set; } = 10;

	/// <summary>Gets or sets polling interval in milliseconds.</summary>
	public int PollingIntervalMs { get; set; } = 1000;

	/// <summary>Gets or sets maximum messages per receive operation.</summary>
	public int MaxMessagesPerReceive { get; set; } = 10;

	/// <summary>Gets maximum wait time as TimeSpan.</summary>
	public TimeSpan MaxWaitTime => TimeSpan.FromSeconds(MaxWaitTimeSeconds);

	/// <summary>Gets minimum wait time as TimeSpan.</summary>
	public TimeSpan MinWaitTime => TimeSpan.FromSeconds(MinWaitTimeSeconds);
}

/// <summary>Visibility timeout configuration.</summary>
public sealed class LongPollingVisibilityOptions
{
	/// <summary>Gets or sets visibility timeout in seconds (0-43200).</summary>
	[Range(0, 43200)]
	public int VisibilityTimeoutSeconds { get; set; } = 30;

	/// <summary>Gets or sets a value indicating whether to enable visibility timeout optimization.</summary>
	public bool EnableOptimization { get; set; } = true;

	/// <summary>Gets or sets buffer factor for visibility timeout calculations (0.0-1.0).</summary>
	[Range(0.0, 1.0)]
	public double BufferFactor { get; set; } = 0.1;
}

/// <summary>Processing configuration.</summary>
public sealed class LongPollingProcessingOptions
{
	/// <summary>Gets or sets batch size for processing.</summary>
	public int BatchSize { get; set; } = 10;

	/// <summary>Gets or sets timeout for processing a single message.</summary>
	public TimeSpan ProcessingTimeout { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>Gets or sets a value indicating whether to enable request coalescing.</summary>
	public bool EnableRequestCoalescing { get; set; } = true;

	/// <summary>Gets or sets time window for coalescing requests in milliseconds.</summary>
	public int CoalescingWindowMs { get; set; } = 100;
}

/// <summary>Retry configuration.</summary>
public sealed class LongPollingRetryOptions
{
	/// <summary>Gets or sets maximum retry attempts.</summary>
	public int MaxAttempts { get; set; } = 3;

	/// <summary>Gets or sets retry delay in milliseconds.</summary>
	public int DelayMs { get; set; } = 1000;
}

/// <summary>Adaptive polling configuration.</summary>
public sealed class LongPollingAdaptiveOptions
{
	/// <summary>Gets or sets a value indicating whether to enable adaptive polling.</summary>
	public bool Enabled { get; set; } = true;

	/// <summary>Gets or sets time window for adaptation calculations.</summary>
	public TimeSpan AdaptationWindow { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>Gets or sets smoothing factor for adaptive calculations (0.0-1.0).</summary>
	[Range(0.0, 1.0)]
	public double SmoothingFactor { get; set; } = 0.3;

	/// <summary>Gets or sets high load threshold for adaptive polling.</summary>
	public double HighLoadThreshold { get; set; } = 0.8;

	/// <summary>Gets or sets low load threshold for adaptive polling.</summary>
	public double LowLoadThreshold { get; set; } = 0.2;
}

/// <summary>Message attribute configuration.</summary>
public sealed class LongPollingAttributeOptions
{
	/// <summary>Gets or sets message attributes to retrieve.</summary>
	public string[] MessageAttributeNames { get; set; } = ["All"];

	/// <summary>Gets or sets system message attributes to retrieve.</summary>
	public string[] SystemAttributeNames { get; set; } = ["All"];
}
