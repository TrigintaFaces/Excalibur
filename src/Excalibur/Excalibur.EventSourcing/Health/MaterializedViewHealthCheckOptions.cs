// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.EventSourcing.Health;

/// <summary>
/// Configuration options for <see cref="MaterializedViewHealthCheck"/>.
/// </summary>
/// <remarks>
/// <para>
/// Controls health check behavior including staleness thresholds and failure rate monitoring.
/// </para>
/// </remarks>
public sealed class MaterializedViewHealthCheckOptions
{
	/// <summary>
	/// Gets or sets the maximum allowed staleness duration before a view is considered unhealthy.
	/// </summary>
	/// <value>The staleness threshold. Defaults to 5 minutes.</value>
	/// <remarks>
	/// <para>
	/// If the time since the last successful refresh exceeds this threshold,
	/// the health check will report a degraded or unhealthy status.
	/// </para>
	/// </remarks>
	public TimeSpan StalenessThreshold { get; set; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets or sets the threshold for failure rate percentage before reporting degraded status.
	/// </summary>
	/// <value>The failure rate threshold as a percentage (0-100). Defaults to 10%.</value>
	/// <remarks>
	/// <para>
	/// When the rolling failure rate exceeds this threshold, the health check
	/// reports a degraded status. Values are clamped to the 0-100 range.
	/// </para>
	/// </remarks>
	[Range(0.0, 100.0)]
	public double FailureRateThresholdPercent { get; set; } = 10.0;

	/// <summary>
	/// Gets or sets a value indicating whether to include detailed view information in the health report.
	/// </summary>
	/// <value><see langword="true"/> to include details; otherwise, <see langword="false"/>. Defaults to <see langword="true"/>.</value>
	public bool IncludeDetails { get; set; } = true;

	/// <summary>
	/// Gets or sets the tags to apply to this health check.
	/// </summary>
	/// <value>A collection of tags. Defaults to "ready" and "event-sourcing".</value>
	public List<string> Tags { get; set; } = ["ready", "event-sourcing"];

	/// <summary>
	/// Gets or sets the name of the health check.
	/// </summary>
	/// <value>The health check name. Defaults to "materialized-views".</value>
	[Required]
	public string Name { get; set; } = "materialized-views";
}
