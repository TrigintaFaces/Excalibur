// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Channels.Diagnostics;

/// <summary>
/// Channel performance metrics.
/// </summary>
public sealed class ChannelMetrics
{
	/// <summary>
	/// Gets the throughput of the channel in messages per second.
	/// </summary>
	/// <value>The current <see cref="MessagesPerSecond"/> value.</value>
	public double MessagesPerSecond { get; init; }

	/// <summary>
	/// Gets the average latency of the channel in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageLatencyMs"/> value.</value>
	public double AverageLatencyMs { get; init; }

	/// <summary>
	/// Gets the 99th percentile latency of the channel in milliseconds.
	/// </summary>
	/// <value>The current <see cref="P99LatencyMs"/> value.</value>
	public double P99LatencyMs { get; init; }
}
