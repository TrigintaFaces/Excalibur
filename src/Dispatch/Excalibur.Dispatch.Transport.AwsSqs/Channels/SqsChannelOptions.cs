// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Configuration options for SQS channel adapter.
/// </summary>
public sealed class SqsChannelOptions
{
	/// <summary>
	/// Gets or sets the SQS queue URL.
	/// </summary>
	/// <value>
	/// The SQS queue URL.
	/// </value>
	public Uri? QueueUrl { get; set; }

	/// <summary>
	/// Gets or sets the number of concurrent polling tasks.
	/// </summary>
	/// <value>
	/// The number of concurrent polling tasks.
	/// </value>
	public int ConcurrentPollers { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum concurrent pollers allowed.
	/// </summary>
	/// <value>
	/// The maximum concurrent pollers allowed.
	/// </value>
	public int MaxConcurrentPollers { get; set; } = 20;

	/// <summary>
	/// Gets or sets the receive channel capacity for backpressure.
	/// </summary>
	/// <value>
	/// The receive channel capacity for backpressure.
	/// </value>
	public int ReceiveChannelCapacity { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the visibility timeout in seconds.
	/// </summary>
	/// <value>
	/// The visibility timeout in seconds.
	/// </value>
	public int VisibilityTimeout { get; set; } = 300;

	/// <summary>
	/// Gets or sets the batch send interval in milliseconds.
	/// </summary>
	/// <value>
	/// The batch send interval in milliseconds.
	/// </value>
	public int BatchIntervalMs { get; set; } = 100;
}
