// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing;

/// <summary>
/// Statistics for message routing operations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="RouteStatistics" /> class. </remarks>
/// <param name="activeRoutes"> The number of active routes. </param>
/// <param name="totalDecisions"> The total number of routing decisions made. </param>
/// <param name="cacheHitRate"> The cache hit rate as a percentage. </param>
/// <param name="averageLatencyUs"> The average routing latency in microseconds. </param>
public sealed class RouteStatistics(int activeRoutes, long totalDecisions, double cacheHitRate, double averageLatencyUs)
{
	/// <summary>
	/// Gets the number of active routes.
	/// </summary>
	/// <value>The current <see cref="ActiveRoutes"/> value.</value>
	public int ActiveRoutes { get; } = activeRoutes;

	/// <summary>
	/// Gets the total number of routing decisions made.
	/// </summary>
	/// <value>The current <see cref="TotalDecisions"/> value.</value>
	public long TotalDecisions { get; } = totalDecisions;

	/// <summary>
	/// Gets the cache hit rate as a percentage.
	/// </summary>
	/// <value>The current <see cref="CacheHitRate"/> value.</value>
	public double CacheHitRate { get; } = cacheHitRate;

	/// <summary>
	/// Gets the average routing latency in microseconds.
	/// </summary>
	/// <value>The current <see cref="AverageLatencyUs"/> value.</value>
	public double AverageLatencyUs { get; } = averageLatencyUs;
}
