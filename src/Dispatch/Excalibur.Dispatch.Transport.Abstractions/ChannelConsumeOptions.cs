// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;
using System.Threading.Channels;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Transport;

/// <summary>
/// Options for configuring channel-based message consumption.
/// </summary>
/// <remarks>
/// Uses sub-option objects for buffer, acknowledgment, and retry settings.
/// Follows <c>System.Threading.Channels.BoundedChannelOptions</c> minimal pattern.
/// </remarks>
public sealed class ChannelConsumeOptions
{
	/// <summary>
	/// Gets creates default channel consume options.
	/// </summary>
	/// <value>Creates default channel consume options.</value>
	public static ChannelConsumeOptions Default => new();

	/// <summary>
	/// Gets creates options optimized for high throughput.
	/// </summary>
	/// <value>Creates options optimized for high throughput.</value>
	public static ChannelConsumeOptions HighThroughput => new()
	{
		Buffer = { MaxConcurrency = Environment.ProcessorCount * 2, PrefetchCount = 100 },
		Acknowledgment = { UseBatchAcknowledgment = true, AcknowledgmentBatchSize = 50, AutoAcknowledge = true },
		Retry = { EnableAutoRetry = false },
	};

	/// <summary>
	/// Gets creates options optimized for ordered processing.
	/// </summary>
	/// <value>Creates options optimized for ordered processing.</value>
	public static ChannelConsumeOptions Ordered => new()
	{
		Buffer = { MaxConcurrency = 1, PrefetchCount = 1 },
		PreserveOrdering = true,
		Acknowledgment = { AutoAcknowledge = true, UseBatchAcknowledgment = false },
	};

	/// <summary>
	/// Gets creates options optimized for reliable processing.
	/// </summary>
	/// <value>Creates options optimized for reliable processing.</value>
	public static ChannelConsumeOptions Reliable => new()
	{
		Buffer = { MaxConcurrency = Environment.ProcessorCount / 2, PrefetchCount = 5 },
		Acknowledgment = { AutoAcknowledge = false },
		Retry = { EnableAutoRetry = true, MaxRetryAttempts = 5, UseExponentialBackoff = true, DeadLetterStrategy = DeadLetterStrategy.MoveToDeadLetterQueue },
	};

	/// <summary>
	/// Gets the buffer configuration options.
	/// </summary>
	/// <value>The buffer options including channel capacity, concurrency, prefetch, and batching.</value>
	public ChannelBufferOptions Buffer { get; } = new();

	/// <summary>
	/// Gets the acknowledgment configuration options.
	/// </summary>
	/// <value>The acknowledgment options including auto-ack, batch ack, and visibility timeout.</value>
	public ChannelAcknowledgmentOptions Acknowledgment { get; } = new();

	/// <summary>
	/// Gets the retry configuration options.
	/// </summary>
	/// <value>The retry options including auto-retry, backoff, and dead letter strategy.</value>
	public ChannelRetryOptions Retry { get; } = new();

	/// <summary>
	/// Gets or sets a value indicating whether to complete the channel when stopping.
	/// </summary>
	/// <value>The current <see cref="CompleteChannelOnStop"/> value.</value>
	public bool CompleteChannelOnStop { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable message ordering preservation.
	/// </summary>
	/// <value>The current <see cref="PreserveOrdering"/> value.</value>
	public bool PreserveOrdering { get; set; }

	/// <summary>
	/// Gets or sets the ordering key selector function.
	/// </summary>
	/// <value>The current <see cref="OrderingKeySelector"/> value.</value>
	public Func<MessageEnvelope, string?>? OrderingKeySelector { get; set; }
}

/// <summary>
/// Buffer and channel configuration options for message consumption.
/// </summary>
/// <remarks>
/// Groups all buffer-related settings: channel capacity, concurrency,
/// prefetch count, and batching.
/// </remarks>
public sealed class ChannelBufferOptions
{
	/// <summary>
	/// Gets or sets the channel options for the internal message channel.
	/// </summary>
	/// <value>The unbounded channel options, or null to use bounded channel.</value>
	public UnboundedChannelOptions? ChannelOptions { get; set; }

	/// <summary>
	/// Gets or sets the capacity of the bounded channel.
	/// </summary>
	/// <value>The channel capacity. Default is 1000.</value>
	[Range(1, int.MaxValue)]
	public int ChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the behavior when the channel is full.
	/// </summary>
	/// <value>The full mode behavior. Default is <see cref="BoundedChannelFullMode.Wait"/>.</value>
	public BoundedChannelFullMode FullMode { get; set; } = BoundedChannelFullMode.Wait;

	/// <summary>
	/// Gets or sets a value indicating whether synchronous continuations are allowed.
	/// </summary>
	/// <value><see langword="true"/> if synchronous continuations are allowed; otherwise, <see langword="false"/>.</value>
	public bool AllowSynchronousContinuations { get; set; }

	/// <summary>
	/// Gets or sets the maximum number of concurrent message processors.
	/// </summary>
	/// <value>The max concurrency. Default is <see cref="Environment.ProcessorCount"/>.</value>
	[Range(1, int.MaxValue)]
	public int MaxConcurrency { get; set; } = Environment.ProcessorCount;

	/// <summary>
	/// Gets or sets the prefetch count for message retrieval.
	/// </summary>
	/// <value>The prefetch count. Default is 10.</value>
	[Range(1, int.MaxValue)]
	public int PrefetchCount { get; set; } = 10;

	/// <summary>
	/// Gets or sets the batch size for processing messages.
	/// </summary>
	/// <value>The batch size. Default is 10.</value>
	[Range(1, int.MaxValue)]
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum wait time for batching messages.
	/// </summary>
	/// <value>The maximum wait time. Default is 1 second.</value>
	public TimeSpan MaxWaitTime { get; set; } = TimeSpan.FromSeconds(1);
}

/// <summary>
/// Acknowledgment configuration options for message consumption.
/// </summary>
/// <remarks>
/// Groups all acknowledgment-related settings: auto-ack, batch acknowledgment,
/// and visibility timeout.
/// </remarks>
public sealed class ChannelAcknowledgmentOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to automatically acknowledge messages after processing.
	/// </summary>
	/// <value><see langword="true"/> for auto-acknowledge; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool AutoAcknowledge { get; set; } = true;

	/// <summary>
	/// Gets or sets the visibility timeout for messages during processing.
	/// </summary>
	/// <value>The visibility timeout, or null for transport default.</value>
	public TimeSpan? VisibilityTimeout { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether to use batch acknowledgment when available.
	/// </summary>
	/// <value><see langword="true"/> to use batch acknowledgment; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool UseBatchAcknowledgment { get; set; } = true;

	/// <summary>
	/// Gets or sets the batch size for acknowledgments.
	/// </summary>
	/// <value>The acknowledgment batch size. Default is 10.</value>
	[Range(1, int.MaxValue)]
	public int AcknowledgmentBatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the timeout for batch acknowledgment operations.
	/// </summary>
	/// <value>The acknowledgment batch timeout. Default is 5 seconds.</value>
	public TimeSpan AcknowledgmentBatchTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

/// <summary>
/// Retry configuration options for message consumption.
/// </summary>
/// <remarks>
/// Groups all retry-related settings: auto-retry, backoff strategy,
/// and dead letter handling.
/// </remarks>
public sealed class ChannelRetryOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether to enable automatic retry on transient failures.
	/// </summary>
	/// <value><see langword="true"/> to enable auto-retry; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool EnableAutoRetry { get; set; } = true;

	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>The max retry attempts. Default is 3.</value>
	[Range(0, int.MaxValue)]
	public int MaxRetryAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the delay between retry attempts.
	/// </summary>
	/// <value>The retry delay. Default is 1 second.</value>
	public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets a value indicating whether to use exponential backoff for retries.
	/// </summary>
	/// <value><see langword="true"/> to use exponential backoff; otherwise, <see langword="false"/>. Default is <see langword="true"/>.</value>
	public bool UseExponentialBackoff { get; set; } = true;

	/// <summary>
	/// Gets or sets the dead letter handling strategy.
	/// </summary>
	/// <value>The dead letter strategy. Default is <see cref="DeadLetterStrategy.MoveToDeadLetterQueue"/>.</value>
	public DeadLetterStrategy DeadLetterStrategy { get; set; } = DeadLetterStrategy.MoveToDeadLetterQueue;
}
