// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Aws;

/// <summary>
/// Polling configuration for high-throughput SQS processing.
/// </summary>
public sealed class HighThroughputSqsPollingOptions
{
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
	/// Gets or sets the wait time for long polling in seconds.
	/// </summary>
	/// <value>
	/// The wait time for long polling in seconds.
	/// </value>
	public int WaitTimeSeconds { get; set; } = 20;
}
