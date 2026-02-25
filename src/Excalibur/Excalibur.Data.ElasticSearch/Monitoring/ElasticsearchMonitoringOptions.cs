// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Configures monitoring and diagnostics for Elasticsearch operations.
/// </summary>
public sealed class ElasticsearchMonitoringOptions
{
	/// <summary>
	/// Gets a value indicating whether monitoring is enabled.
	/// </summary>
	/// <value> A <see cref="bool" /> indicating whether monitoring features are active. Defaults to <c> true </c>. </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the monitoring verbosity level.
	/// </summary>
	/// <value> The level of monitoring detail. Defaults to <see cref="MonitoringLevel.Standard" />. </value>
	public MonitoringLevel Level { get; init; } = MonitoringLevel.Standard;

	/// <summary>
	/// Gets the metrics collection settings.
	/// </summary>
	/// <value> Configuration for metrics collection and reporting. </value>
	public MetricsOptions Metrics { get; init; } = new();

	/// <summary>
	/// Gets the request/response logging settings.
	/// </summary>
	/// <value> Configuration for request and response logging. </value>
	public RequestLoggingOptions RequestLogging { get; init; } = new();

	/// <summary>
	/// Gets the performance diagnostics settings.
	/// </summary>
	/// <value> Configuration for performance monitoring and slow operation detection. </value>
	public PerformanceDiagnosticsOptions Performance { get; init; } = new();

	/// <summary>
	/// Gets the health monitoring settings.
	/// </summary>
	/// <value> Configuration for health checks and cluster monitoring. </value>
	public HealthMonitoringOptions Health { get; init; } = new();

	/// <summary>
	/// Gets the tracing settings.
	/// </summary>
	/// <value> Configuration for distributed tracing integration. </value>
	public TracingOptions Tracing { get; init; } = new();
}
