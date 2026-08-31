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
	/// Polling configuration.
	/// </summary>
	public HighThroughputSqsPollingOptions Polling { get; set; } = new();

	/// <summary>
	/// Gets or sets the channel capacity for buffering messages.
	/// </summary>
	/// <value>
	/// The channel capacity for buffering messages.
	/// </value>
	public int ChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the batch delete interval in milliseconds.
	/// </summary>
	/// <value>
	/// The batch delete interval in milliseconds.
	/// </value>
	public int BatchDeleteIntervalMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the visibility timeout in seconds.
	/// </summary>
	/// <value>
	/// The visibility timeout in seconds.
	/// </value>
	public int VisibilityTimeout { get; set; } = 30;

}
