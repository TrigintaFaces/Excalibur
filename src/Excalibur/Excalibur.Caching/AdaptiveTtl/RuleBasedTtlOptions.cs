// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.ComponentModel.DataAnnotations;

namespace Excalibur.Caching.AdaptiveTtl;

/// <summary>
/// Configuration options for rule-based adaptive TTL.
/// </summary>
public sealed class RuleBasedTtlOptions : AdaptiveTtlOptions
{
	/// <summary>
	/// Gets or sets the hit rate rule configuration.
	/// </summary>
	/// <value> The hit rate rule options. </value>
	public RuleBasedHitRateOptions HitRate { get; set; } = new();

	/// <summary>
	/// Gets or sets the frequency rule configuration.
	/// </summary>
	/// <value> The frequency rule options. </value>
	public RuleBasedFrequencyOptions Frequency { get; set; } = new();

	/// <summary>
	/// Gets or sets the load and cost rule configuration.
	/// </summary>
	/// <value> The load rule options. </value>
	public RuleBasedLoadOptions Load { get; set; } = new();

	/// <summary>
	/// Gets or sets the time-of-day rule configuration.
	/// </summary>
	/// <value> The time-of-day rule options. </value>
	public RuleBasedTimeOfDayOptions TimeOfDay { get; set; } = new();

	/// <summary>
	/// Gets or sets the content size rule configuration.
	/// </summary>
	/// <value> The content size rule options. </value>
	public RuleBasedContentOptions Content { get; set; } = new();

}

/// <summary>
/// Hit rate rule configuration for rule-based TTL strategies.
/// </summary>
public sealed class RuleBasedHitRateOptions
{
	/// <summary>
	/// Gets or sets the threshold for considering a cache hit rate as high (0.0-1.0).
	/// </summary>
	/// <value> The threshold value for a high cache hit rate. </value>
	[Range(0.0, 1.0)]
	public double HighHitRateThreshold { get; set; } = 0.9;

	/// <summary>
	/// Gets or sets the threshold for considering a cache hit rate as low (0.0-1.0).
	/// </summary>
	/// <value> The threshold value for a low cache hit rate. </value>
	[Range(0.0, 1.0)]
	public double LowHitRateThreshold { get; set; } = 0.5;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when hit rate is high.
	/// </summary>
	/// <value> The TTL multiplier applied when the hit rate is high. </value>
	[Range(0.1, 10.0)]
	public double HighHitRateMultiplier { get; set; } = 1.5;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when hit rate is low.
	/// </summary>
	/// <value> The TTL multiplier applied when the hit rate is low. </value>
	[Range(0.1, 10.0)]
	public double LowHitRateMultiplier { get; set; } = 0.7;
}

/// <summary>
/// Frequency rule configuration for rule-based TTL strategies.
/// </summary>
public sealed class RuleBasedFrequencyOptions
{
	/// <summary>
	/// Gets or sets the threshold for considering access frequency as high (requests per minute).
	/// </summary>
	/// <value> The threshold for high access frequency in requests per minute. </value>
	[Range(0.1, double.MaxValue)]
	public double HighFrequencyThreshold { get; set; } = 100;

	/// <summary>
	/// Gets or sets the threshold for considering access frequency as low (requests per minute).
	/// </summary>
	/// <value> The threshold for low access frequency in requests per minute. </value>
	[Range(0.0, double.MaxValue)]
	public double LowFrequencyThreshold { get; set; } = 1;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when access frequency is high.
	/// </summary>
	/// <value> The TTL multiplier applied when access frequency is high. </value>
	[Range(0.1, 10.0)]
	public double HighFrequencyMultiplier { get; set; } = 1.4;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when access frequency is low.
	/// </summary>
	/// <value> The TTL multiplier applied when access frequency is low. </value>
	[Range(0.1, 10.0)]
	public double LowFrequencyMultiplier { get; set; } = 0.8;
}

/// <summary>
/// Load and cost rule configuration for rule-based TTL strategies.
/// </summary>
public sealed class RuleBasedLoadOptions
{
	/// <summary>
	/// Gets or sets the threshold for considering a cache miss as expensive.
	/// </summary>
	/// <value> The time threshold for determining an expensive cache miss. </value>
	public TimeSpan ExpensiveMissThreshold { get; set; } = TimeSpan.FromMilliseconds(100);

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when cache misses are expensive.
	/// </summary>
	/// <value> The TTL multiplier applied when cache misses are expensive. </value>
	[Range(0.1, 10.0)]
	public double ExpensiveMissMultiplier { get; set; } = 1.3;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when system load is high.
	/// </summary>
	/// <value> The TTL multiplier applied when system load is high. </value>
	[Range(0.1, 10.0)]
	public double HighLoadMultiplier { get; set; } = 0.7;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL when system load is low.
	/// </summary>
	/// <value> The TTL multiplier applied when system load is low. </value>
	[Range(0.1, 10.0)]
	public double LowLoadMultiplier { get; set; } = 1.2;
}

/// <summary>
/// Time-of-day rule configuration for rule-based TTL strategies.
/// </summary>
public sealed class RuleBasedTimeOfDayOptions
{
	/// <summary>
	/// Gets or sets the start hour of peak usage time (24-hour format).
	/// </summary>
	/// <value> The hour when peak usage begins (0-23). </value>
	[Range(0, 23)]
	public int PeakHoursStart { get; set; } = 9;

	/// <summary>
	/// Gets or sets the end hour of peak usage time (24-hour format).
	/// </summary>
	/// <value> The hour when peak usage ends (0-23). </value>
	[Range(0, 23)]
	public int PeakHoursEnd { get; set; } = 17;

	/// <summary>
	/// Gets or sets the start hour of off-peak usage time (24-hour format).
	/// </summary>
	/// <value> The hour when off-peak usage begins (0-23). </value>
	[Range(0, 23)]
	public int OffHoursStart { get; set; } = 22;

	/// <summary>
	/// Gets or sets the end hour of off-peak usage time (24-hour format).
	/// </summary>
	/// <value> The hour when off-peak usage ends (0-23). </value>
	[Range(0, 23)]
	public int OffHoursEnd { get; set; } = 6;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL during peak hours.
	/// </summary>
	/// <value> The TTL multiplier applied during peak hours. </value>
	[Range(0.1, 10.0)]
	public double PeakHoursMultiplier { get; set; } = 1.2;

	/// <summary>
	/// Gets or sets the multiplier applied to TTL during off-peak hours.
	/// </summary>
	/// <value> The TTL multiplier applied during off-peak hours. </value>
	[Range(0.1, 10.0)]
	public double OffHoursMultiplier { get; set; } = 0.8;
}

/// <summary>
/// Content size rule configuration for rule-based TTL strategies.
/// </summary>
public sealed class RuleBasedContentOptions
{
	/// <summary>
	/// Gets or sets the size threshold for considering content as large (in bytes).
	/// </summary>
	/// <value> The size threshold in bytes for determining large content. </value>
	[Range(1, long.MaxValue)]
	public long LargeContentThreshold { get; set; } = 1024 * 1024; // 1MB

	/// <summary>
	/// Gets or sets the multiplier applied to TTL for large content.
	/// </summary>
	/// <value> The TTL multiplier applied for large content. </value>
	[Range(0.1, 10.0)]
	public double LargeContentMultiplier { get; set; } = 0.9;
}
