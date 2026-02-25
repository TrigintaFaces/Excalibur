// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures health monitoring for Elasticsearch cluster and nodes.
/// </summary>
public sealed class HealthMonitoringOptions
{
	/// <summary>
	/// Gets a value indicating whether health monitoring is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether health checks are active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the interval for health check polling.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the health check interval. Defaults to 30 seconds. </value>
	public TimeSpan HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets a value indicating whether to monitor individual node health.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to check individual node status. Defaults to <c> false </c>. </value>
	public bool MonitorNodeHealth { get; init; }

	/// <summary>
	/// Gets a value indicating whether to monitor cluster statistics.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether to collect cluster-level statistics. Defaults to <c> false </c>. </value>
	public bool MonitorClusterStats { get; init; }

	/// <summary>
	/// Gets the timeout for health check operations.
	/// </summary>
	/// <value> A <see cref="TimeSpan" /> representing the health check timeout. Defaults to 10 seconds. </value>
	public TimeSpan HealthCheckTimeout { get; init; } = TimeSpan.FromSeconds(10);
}
