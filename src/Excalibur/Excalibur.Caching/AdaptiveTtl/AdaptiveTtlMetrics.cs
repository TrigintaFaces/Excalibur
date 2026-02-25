// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Metrics for adaptive TTL strategies.
/// </summary>
public sealed class AdaptiveTtlMetrics
{
	/// <summary>
	/// Gets or sets the average TTL adjustment factor.
	/// </summary>
	/// <value> The average factor by which TTLs are adjusted. </value>
	public double AverageAdjustmentFactor { get; set; }

	/// <summary>
	/// Gets or sets the total number of TTL calculations.
	/// </summary>
	/// <value> The total count of TTL calculations performed. </value>
	public long TotalCalculations { get; set; }

	/// <summary>
	/// Gets or sets the number of TTL increases.
	/// </summary>
	/// <value> The count of times TTL was increased. </value>
	public long TtlIncreases { get; set; }

	/// <summary>
	/// Gets or sets the number of TTL decreases.
	/// </summary>
	/// <value> The count of times TTL was decreased. </value>
	public long TtlDecreases { get; set; }

	/// <summary>
	/// Gets or sets the average hit rate.
	/// </summary>
	/// <value> The average cache hit rate as a percentage. </value>
	public double AverageHitRate { get; set; }

	/// <summary>
	/// Gets custom strategy-specific metrics.
	/// </summary>
	/// <value> A dictionary of custom metric names and their values. </value>
	public IDictionary<string, double> CustomMetrics { get; } = new Dictionary<string, double>(StringComparer.Ordinal);
}
