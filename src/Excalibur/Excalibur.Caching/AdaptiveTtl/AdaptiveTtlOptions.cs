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
	/// Gets or sets the target hit rate.
	/// </summary>
	/// <value> The target hit rate as a value between 0.0 and 1.0. </value>
	[Range(0.0, 1.0)]
	public double TargetHitRate { get; set; } = 0.8;

	/// <summary>
	/// Gets or sets the target response time.
	/// </summary>
	/// <value> The target response time for cache operations. </value>
	public TimeSpan TargetResponseTime { get; set; } = TimeSpan.FromMilliseconds(50);

	/// <summary>
	/// Gets or sets the learning rate for reinforcement learning.
	/// </summary>
	/// <value> The learning rate for the reinforcement learning algorithm. </value>
	[Range(0.001, 1.0)]
	public double LearningRate { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets the discount factor for future rewards.
	/// </summary>
	/// <value> The discount factor applied to future rewards. </value>
	[Range(0.0, 1.0)]
	public double DiscountFactor { get; set; } = 0.9;

	/// <summary>
	/// Gets or sets the factor weight configuration for TTL calculations.
	/// </summary>
	/// <value> The factor weight options. </value>
	public AdaptiveTtlWeightOptions Weights { get; set; } = new();

	/// <summary>
	/// Gets or sets the threshold configuration for system load and capacity.
	/// </summary>
	/// <value> The threshold options. </value>
	public AdaptiveTtlThresholdOptions Thresholds { get; set; } = new();

}

/// <summary>
/// Factor weight configuration for adaptive TTL calculations.
/// </summary>
public sealed class AdaptiveTtlWeightOptions
{
	/// <summary>
	/// Gets or sets the weight for hit rate factor.
	/// </summary>
	/// <value> The weight applied to the hit rate factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double HitRateWeight { get; set; } = 0.3;

	/// <summary>
	/// Gets or sets the weight for access frequency factor.
	/// </summary>
	/// <value> The weight applied to the access frequency factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double AccessFrequencyWeight { get; set; } = 0.25;

	/// <summary>
	/// Gets or sets the weight for temporal factor.
	/// </summary>
	/// <value> The weight applied to the temporal factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double TemporalWeight { get; set; } = 0.15;

	/// <summary>
	/// Gets or sets the weight for cost factor.
	/// </summary>
	/// <value> The weight applied to the cost factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double CostWeight { get; set; } = 0.15;

	/// <summary>
	/// Gets or sets the weight for load factor.
	/// </summary>
	/// <value> The weight applied to the load factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double LoadWeight { get; set; } = 0.1;

	/// <summary>
	/// Gets or sets the weight for volatility factor.
	/// </summary>
	/// <value> The weight applied to the volatility factor in TTL calculations. </value>
	[Range(0.0, 1.0)]
	public double VolatilityWeight { get; set; } = 0.05;
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

	/// <summary>
	/// Gets or sets the maximum expected access frequency (requests per minute).
	/// </summary>
	/// <value> The maximum expected access frequency in requests per minute. </value>
	[Range(0.1, double.MaxValue)]
	public double MaxExpectedFrequency { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the maximum expected miss cost in milliseconds.
	/// </summary>
	/// <value> The maximum expected cache miss cost in milliseconds. </value>
	[Range(0.1, double.MaxValue)]
	public double MaxExpectedMissCostMs { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the large content threshold in MB.
	/// </summary>
	/// <value> The size threshold in megabytes for determining large content. </value>
	[Range(0.001, double.MaxValue)]
	public double LargeContentThresholdMb { get; set; } = 10;
}
