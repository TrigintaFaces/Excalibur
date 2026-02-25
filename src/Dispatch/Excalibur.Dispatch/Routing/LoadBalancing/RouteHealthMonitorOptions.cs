// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Routing.LoadBalancing;

/// <summary>
/// Options for route health monitoring.
/// </summary>
public sealed class RouteHealthMonitorOptions
{
	/// <summary>
	/// Gets or sets the interval between health checks.
	/// </summary>
	/// <value>
	/// The interval between health checks.
	/// </value>
	public TimeSpan CheckInterval { get; set; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets or sets the initial delay before starting health checks.
	/// </summary>
	/// <value>
	/// The initial delay before starting health checks.
	/// </value>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(10);

	/// <summary>
	/// Gets or sets the maximum number of concurrent health checks.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent health checks.
	/// </value>
	public int MaxConcurrentHealthChecks { get; set; } = 10;

	/// <summary>
	/// Gets or sets the HTTP timeout for health checks.
	/// </summary>
	/// <value>
	/// The HTTP timeout for health checks.
	/// </value>
	public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the number of consecutive failures before marking unhealthy.
	/// </summary>
	/// <value>
	/// The number of consecutive failures before marking unhealthy.
	/// </value>
	public int UnhealthyThreshold { get; set; } = 3;

	/// <summary>
	/// Gets or sets the number of consecutive successes before marking healthy.
	/// </summary>
	/// <value>
	/// The number of consecutive successes before marking healthy.
	/// </value>
	public int HealthyThreshold { get; set; } = 2;
}
