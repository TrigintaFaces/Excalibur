// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Configuration options for consistency monitoring alerts.
/// </summary>
/// <remarks>
/// Renamed from <c>ConsistencyAlertConfiguration</c> to follow the Options naming convention (Sprint 743).
/// </remarks>
public sealed class ConsistencyAlertOptions
{
	/// <summary>
	/// Gets the maximum acceptable lag before alerting.
	/// </summary>
	/// <value>
	/// The maximum acceptable lag duration.
	/// </value>
	public TimeSpan MaxAcceptableLag { get; init; } = TimeSpan.FromSeconds(30);

	/// <summary>
	/// Gets the cooldown period between alerts.
	/// </summary>
	/// <value>
	/// The alert cooldown period.
	/// </value>
	public TimeSpan AlertCooldownPeriod { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets the time window for metrics calculation.
	/// </summary>
	/// <value>
	/// The metrics window duration.
	/// </value>
	public TimeSpan MetricsWindow { get; init; } = TimeSpan.FromMinutes(15);

	/// <summary>
	/// Gets the required SLA compliance percentage.
	/// </summary>
	/// <value>
	/// The required SLA compliance percentage (0-100).
	/// </value>
	public double RequiredSLAPercentage { get; init; } = 99.9;

	/// <summary>
	/// Gets a value indicating whether to alert on individual events.
	/// </summary>
	/// <value>
	/// <see langword="true"/> to alert on individual events; otherwise, <see langword="false"/>.
	/// </value>
	public bool AlertOnIndividualEvents { get; init; }

	/// <summary>
	/// Gets the projection-specific lag thresholds.
	/// </summary>
	/// <value>
	/// A dictionary mapping projection types to their specific lag thresholds.
	/// </value>
	public IDictionary<string, TimeSpan>? ProjectionSpecificThresholds { get; init; }

	/// <summary>
	/// Gets the severity thresholds for escalating alerts.
	/// </summary>
	/// <value>
	/// A list of alert severity thresholds.
	/// </value>
	public IList<AlertSeverityThreshold>? SeverityThresholds { get; init; }
}
