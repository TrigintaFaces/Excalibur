// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Data.ElasticSearch;

/// <summary>
/// Configures consistency tracking between write and read models.
/// </summary>
public sealed class ConsistencyTrackingOptions
{
	/// <summary>
	/// Gets a value indicating whether consistency tracking is enabled.
	/// </summary>
	/// <value>
	/// A value indicating whether consistency tracking is enabled.
	/// </value>
	public bool Enabled { get; init; } = true;

	/// <summary>
	/// Gets the expected maximum lag for projections.
	/// </summary>
	/// <value>
	/// The expected maximum lag for projections.
	/// </value>
	public TimeSpan ExpectedMaxLag { get; init; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets the SLA percentage for consistency.
	/// </summary>
	/// <value>
	/// The SLA percentage for consistency.
	/// </value>
	public double SLAPercentage { get; init; } = 99.0;

	/// <summary>
	/// Gets the metrics collection interval.
	/// </summary>
	/// <value>
	/// The metrics collection interval.
	/// </value>
	public TimeSpan MetricsInterval { get; init; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets a value indicating whether to enable alerting on consistency violations.
	/// </summary>
	/// <value>
	/// A value indicating whether to enable alerting on consistency violations.
	/// </value>
	public bool EnableAlerting { get; init; } = true;
}
