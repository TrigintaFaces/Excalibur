// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Middleware;

/// <summary>
/// Configuration for a specific rate limit.
/// </summary>
public sealed class RateLimitConfiguration
{
	/// <summary>
	/// Gets or sets the rate limiting algorithm to use.
	/// </summary>
	/// <value>The current <see cref="Algorithm"/> value.</value>
	public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;

	/// <summary>
	/// Gets or sets the maximum number of tokens in the bucket for token bucket algorithm.
	/// </summary>
	/// <value>The current <see cref="TokenLimit"/> value.</value>
	public int TokenLimit { get; set; } = 100;

	/// <summary>
	/// Gets or sets the period for replenishing tokens in the bucket.
	/// </summary>
	/// <value>
	/// The period for replenishing tokens in the bucket.
	/// </value>
	public TimeSpan ReplenishmentPeriod { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the number of tokens to add per replenishment period.
	/// </summary>
	/// <value>The current <see cref="TokensPerPeriod"/> value.</value>
	public int TokensPerPeriod { get; set; } = 100;

	/// <summary>
	/// Gets or sets the maximum number of permits allowed for sliding/fixed window algorithms.
	/// </summary>
	/// <value>The current <see cref="PermitLimit"/> value.</value>
	public int PermitLimit { get; set; } = 100;

	/// <summary>
	/// Gets or sets the time window duration for sliding/fixed window algorithms.
	/// </summary>
	/// <value>
	/// The time window duration for sliding/fixed window algorithms.
	/// </value>
	public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the number of segments per window for sliding window algorithm.
	/// </summary>
	/// <value>The current <see cref="SegmentsPerWindow"/> value.</value>
	public int SegmentsPerWindow { get; set; } = 4;
}
