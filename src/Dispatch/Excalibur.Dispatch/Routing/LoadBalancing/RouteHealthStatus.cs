// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Represents the health status of a route.
/// </summary>
public sealed class RouteHealthStatus
{
	/// <summary>
	/// Gets or sets the route ID.
	/// </summary>
	/// <value>
	/// The route ID.
	/// </value>
	public string RouteId { get; set; } = string.Empty;

	/// <summary>
	/// Gets or sets a value indicating whether the route is healthy.
	/// </summary>
	/// <value>
	/// A value indicating whether the route is healthy.
	/// </value>
	public bool IsHealthy { get; set; }

	/// <summary>
	/// Gets or sets the last health check timestamp.
	/// </summary>
	/// <value>
	/// The last health check timestamp.
	/// </value>
	public DateTimeOffset LastCheck { get; set; } = DateTimeOffset.UtcNow;

	/// <summary>
	/// Gets or sets the consecutive failure count.
	/// </summary>
	/// <value>
	/// The consecutive failure count.
	/// </value>
	public int ConsecutiveFailures { get; set; }

	/// <summary>
	/// Gets or sets the average latency.
	/// </summary>
	/// <value>
	/// The average latency.
	/// </value>
	public TimeSpan AverageLatency { get; set; }

	/// <summary>
	/// Gets or sets the success rate (0-1).
	/// </summary>
	/// <value>
	/// The success rate (0-1).
	/// </value>
	public double SuccessRate { get; set; }

	/// <summary>
	/// Gets additional health metadata.
	/// </summary>
	/// <value>
	/// Additional health metadata.
	/// </value>
	public Dictionary<string, object> Metadata { get; init; } = [];
}
