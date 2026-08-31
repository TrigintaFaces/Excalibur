// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Configuration options for adaptive TTL strategies.
/// </summary>
public class AdaptiveTtlOptions
{
	/// <summary>
	/// Gets or sets the minimum TTL.
	/// </summary>
	/// <value> The minimum time-to-live value. </value>
	public TimeSpan MinTtl { get; set; } = TimeSpan.FromSeconds(5);

	/// <summary>
	/// Gets or sets the maximum TTL.
	/// </summary>
	/// <value> The maximum time-to-live value. </value>
	public TimeSpan MaxTtl { get; set; } = TimeSpan.FromHours(24);

	/// <summary>
	/// Gets or sets the threshold configuration for system load and capacity.
	/// </summary>
	/// <value> The threshold options. </value>
	public AdaptiveTtlThresholdOptions Thresholds { get; set; } = new();

}

/// <summary>
/// Threshold configuration for system load, frequency, and content size.
/// </summary>
public sealed class AdaptiveTtlThresholdOptions
{
	/// <summary>
	/// Gets or sets the high load threshold.
	/// </summary>
	/// <value> The threshold value for determining high system load. </value>
	[Range(0.0, 1.0)]
	public double HighLoadThreshold { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets the low load threshold.
	/// </summary>
	/// <value> The threshold value for determining low system load. </value>
	[Range(0.0, 1.0)]
	public double LowLoadThreshold { get; set; } = 0.3;
}
