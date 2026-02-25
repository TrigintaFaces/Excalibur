// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Metrics;

/// <summary>
/// Represents metrics for a messaging channel.
/// </summary>
public sealed class ChannelMetrics
{
	/// <summary>
	/// Gets or sets the number of messages processed per second.
	/// </summary>
	/// <value>The current <see cref="MessagesPerSecond"/> value.</value>
	public double MessagesPerSecond { get; set; }

	/// <summary>
	/// Gets or sets the average latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageLatencyMs"/> value.</value>
	public double AverageLatencyMs { get; set; }

	/// <summary>
	/// Gets or sets the 99th percentile latency in milliseconds.
	/// </summary>
	/// <value>The current <see cref="P99LatencyMs"/> value.</value>
	public double P99LatencyMs { get; set; }
}
