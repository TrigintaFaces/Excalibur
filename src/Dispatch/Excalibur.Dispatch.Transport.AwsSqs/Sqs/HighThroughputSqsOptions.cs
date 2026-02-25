// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for high-throughput SQS processing.
/// </summary>
public sealed class HighThroughputSqsOptions
{
	/// <summary>
	/// Gets or sets the SQS queue URL.
	/// </summary>
	/// <value>
	/// The SQS queue URL.
	/// </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the maximum concurrency for message processing.
	/// </summary>
	/// <value>
	/// The maximum concurrency for message processing.
	/// </value>
	public int MaxConcurrency { get; set; } = 10;

	/// <summary>
	/// Gets or sets the number of concurrent pollers.
	/// </summary>
	/// <value>
	/// The number of concurrent pollers.
	/// </value>
	public int ConcurrentPollers { get; set; } = 5;

	/// <summary>
	/// Gets or sets the maximum number of concurrent pollers.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent pollers.
	/// </value>
	public int MaxConcurrentPollers { get; set; } = 10;

	/// <summary>
	/// Gets or sets the channel capacity for buffering messages.
	/// </summary>
	/// <value>
	/// The channel capacity for buffering messages.
	/// </value>
	public int ChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum number of concurrent messages to process.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent messages to process.
	/// </value>
	public int MaxConcurrentMessages { get; set; } = 100;

	/// <summary>
	/// Gets or sets the batch delete interval in milliseconds.
	/// </summary>
	/// <value>
	/// The batch delete interval in milliseconds.
	/// </value>
	public int BatchDeleteIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the batch size for receiving messages.
	/// </summary>
	/// <value>
	/// The batch size for receiving messages.
	/// </value>
	public int BatchSize { get; set; } = 10;

	/// <summary>
	/// Gets or sets the visibility timeout in seconds.
	/// </summary>
	/// <value>
	/// The visibility timeout in seconds.
	/// </value>
	public int VisibilityTimeout { get; set; } = 30;

	/// <summary>
	/// Gets or sets the wait time for long polling in seconds.
	/// </summary>
	/// <value>
	/// The wait time for long polling in seconds.
	/// </value>
	public int WaitTimeSeconds { get; set; } = 20;

	/// <summary>
	/// Gets or sets a value indicating whether batching is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether batching is enabled.
	/// </value>
	public bool EnableBatching { get; set; } = true;
}
