// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch.Projections;

/// <summary>
/// Configuration for consistency monitoring alerts.
/// </summary>
public sealed class ConsistencyAlertConfiguration
{
	/// <summary>
	/// Gets the maximum acceptable lag before alerting.
	/// </summary>
	/// <value>
	/// The maximum acceptable lag before alerting.
	/// </value>
	public required TimeSpan MaxAcceptableLag { get; init; }

	/// <summary>
	/// Gets the percentage of events that must meet SLA.
	/// </summary>
	/// <value>
	/// The percentage of events that must meet SLA.
	/// </value>
	public double RequiredSLAPercentage { get; init; } = 99.0;

	/// <summary>
	/// Gets the time window for calculating metrics.
	/// </summary>
	/// <value>
	/// The time window for calculating metrics.
	/// </value>
	public TimeSpan MetricsWindow { get; init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets projection-specific thresholds.
	/// </summary>
	/// <value>
	/// Projection-specific thresholds.
	/// </value>
	public IDictionary<string, TimeSpan>? ProjectionSpecificThresholds { get; init; }

	/// <summary>
	/// Gets a value indicating whether to alert on individual lagging events.
	/// </summary>
	/// <value>
	/// A value indicating whether to alert on individual lagging events.
	/// </value>
	public bool AlertOnIndividualEvents { get; init; }

	/// <summary>
	/// Gets the cooldown period between alerts.
	/// </summary>
	/// <value>
	/// The cooldown period between alerts.
	/// </value>
	public TimeSpan AlertCooldownPeriod { get; init; } = TimeSpan.FromMinutes(15);

	/// <summary>
	/// Gets the alert severity levels based on lag duration.
	/// </summary>
	/// <value>
	/// The alert severity levels based on lag duration.
	/// </value>
	public IList<AlertSeverityThreshold>? SeverityThresholds { get; init; }
}
