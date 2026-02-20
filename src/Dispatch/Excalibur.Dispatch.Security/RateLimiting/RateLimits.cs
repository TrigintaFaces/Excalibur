// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Security;

/// <summary>
/// Defines rate limit parameters.
/// </summary>
public sealed class RateLimits
{
	/// <summary>
	/// Gets or sets the maximum number of tokens in the token bucket.
	/// </summary>
	/// <value>
	/// The maximum number of tokens in the token bucket. The default is 100.
	/// </value>
	public int TokenLimit { get; set; } = 100;

	/// <summary>
	/// Gets or sets the number of tokens to add to the bucket per replenishment period.
	/// </summary>
	/// <value>
	/// The number of tokens to add to the bucket per replenishment period. The default is 20.
	/// </value>
	public int TokensPerPeriod { get; set; } = 20;

	/// <summary>
	/// Gets or sets the replenishment period in seconds for token bucket rate limiting.
	/// </summary>
	/// <value>
	/// The replenishment period in seconds for token bucket rate limiting. The default is 1 second.
	/// </value>
	public int ReplenishmentPeriodSeconds { get; set; } = 1;

	/// <summary>
	/// Gets or sets the maximum number of permits allowed within the time window.
	/// </summary>
	/// <value>
	/// The maximum number of permits allowed within the time window. The default is 100.
	/// </value>
	public int PermitLimit { get; set; } = 100;

	/// <summary>
	/// Gets or sets the time window duration in seconds for window-based rate limiting.
	/// </summary>
	/// <value>
	/// The time window duration in seconds for window-based rate limiting. The default is 60 seconds.
	/// </value>
	public int WindowSeconds { get; set; } = 60;

	/// <summary>
	/// Gets or sets the number of segments to divide the time window into.
	/// </summary>
	/// <value>
	/// The number of segments to divide the time window into. The default is 4.
	/// </value>
	public int SegmentsPerWindow { get; set; } = 4;

	/// <summary>
	/// Gets or sets the maximum number of concurrent requests allowed.
	/// </summary>
	/// <value>
	/// The maximum number of concurrent requests allowed. The default is 10.
	/// </value>
	public int ConcurrencyLimit { get; set; } = 10;

	/// <summary>
	/// Gets or sets the maximum number of requests that can be queued when rate limits are exceeded.
	/// </summary>
	/// <value>
	/// The maximum number of requests that can be queued when rate limits are exceeded. The default is 10.
	/// </value>
	public int QueueLimit { get; set; } = 10;
}
