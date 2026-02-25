// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.ComponentModel.DataAnnotations;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Configuration options for rate limiting.
/// </summary>
public sealed class RateLimitingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether rate limiting is enabled.
	/// </summary>
	/// <value>
	/// <see langword="true"/> if rate limiting is enabled; otherwise, <see langword="false"/>. The default is <see langword="true"/>.
	/// </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets the rate limiting algorithm to use.
	/// </summary>
	/// <value>
	/// The rate limiting algorithm to use. The default is <see cref="RateLimitAlgorithm.TokenBucket"/>.
	/// </value>
	public RateLimitAlgorithm Algorithm { get; set; } = RateLimitAlgorithm.TokenBucket;

	/// <summary>
	/// Gets or sets the default rate limits.
	/// </summary>
	/// <value>
	/// The default rate limits to apply when no tenant or tier specific limits are configured.
	/// </value>
	public RateLimits DefaultLimits { get; set; } = new();

	/// <summary>
	/// Gets or initializes tenant-specific rate limits.
	/// </summary>
	/// <value>
	/// A dictionary mapping tenant identifiers to their specific rate limits, or an empty dictionary if no tenant-specific limits are configured.
	/// </value>
	public IDictionary<string, RateLimits> TenantLimits { get; init; } = new Dictionary<string, RateLimits>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or initializes tier-based rate limits (e.g., "free", "premium", "enterprise").
	/// </summary>
	/// <value>
	/// A dictionary mapping tier names to their rate limits, or an empty dictionary if no tier-based limits are configured.
	/// </value>
	public IDictionary<string, RateLimits> TierLimits { get; init; } = new Dictionary<string, RateLimits>(StringComparer.Ordinal);

	/// <summary>
	/// Gets or sets the default retry-after duration in milliseconds.
	/// </summary>
	/// <value>
	/// The default retry-after duration in milliseconds. The default is 1000 (1 second).
	/// </value>
	[Range(1, int.MaxValue)]
	public int DefaultRetryAfterMilliseconds { get; set; } = 1000;

	/// <summary>
	/// Gets or sets the cleanup interval in minutes for removing inactive limiters.
	/// </summary>
	/// <value>
	/// The cleanup interval in minutes for removing inactive limiters. The default is 5 minutes.
	/// </value>
	[Range(1, int.MaxValue)]
	public int CleanupIntervalMinutes { get; set; } = 5;

	/// <summary>
	/// Gets or sets the inactivity timeout in minutes before a limiter is removed.
	/// </summary>
	/// <value>
	/// The inactivity timeout in minutes before a limiter is removed. The default is 30 minutes.
	/// </value>
	[Range(1, int.MaxValue)]
	public int InactivityTimeoutMinutes { get; set; } = 30;
}
