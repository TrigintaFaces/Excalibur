// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Health metrics for system monitoring.
/// </summary>
public sealed class HealthMetrics
{
	/// <summary>
	/// Gets the CPU usage percentage (0-100).
	/// </summary>
	/// <value>The instantaneous CPU utilization percentage for the monitored node.</value>
	public double CpuUsagePercent { get; init; }

	/// <summary>
	/// Gets the memory usage percentage (0-100).
	/// </summary>
	/// <value>The percentage of memory currently consumed by the workload.</value>
	public double MemoryUsagePercent { get; init; }

	/// <summary>
	/// Gets the error rate (0.0-1.0).
	/// </summary>
	/// <value>The ratio of failed operations to total operations in the sampling window.</value>
	public double ErrorRate { get; init; }

	/// <summary>
	/// Gets the average response time in milliseconds.
	/// </summary>
	/// <value>The mean latency, in milliseconds, observed during the sampling window.</value>
	public double ResponseTimeMs { get; init; }

	/// <summary>
	/// Gets the number of active connections.
	/// </summary>
	/// <value>The active connection count reported by the monitored service.</value>
	public int ActiveConnections { get; init; }

	/// <summary>
	/// Gets the timestamp when these metrics were collected.
	/// </summary>
	/// <value>The UTC timestamp representing when the metrics snapshot was captured.</value>
	public DateTimeOffset Timestamp { get; init; }
}
