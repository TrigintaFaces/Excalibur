// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Options.Middleware;

/// <summary>
/// Configuration options for rate limiting middleware.
/// </summary>
public sealed class RateLimitingOptions
{
	/// <summary>
	/// Gets or sets a value indicating whether rate limiting is enabled.
	/// </summary>
	/// <value> Default is true. </value>
	public bool Enabled { get; set; } = true;

	/// <summary>
	/// Gets or sets a value indicating whether to enable per-tenant rate limiting.
	/// </summary>
	/// <value> Default is true. </value>
	public bool EnablePerTenantLimiting { get; set; } = true;

	/// <summary>
	/// Gets or sets the default rate limit configuration.
	/// </summary>
	/// <value>
	/// The default rate limit configuration.
	/// </value>
	public RateLimitConfiguration DefaultLimit { get; set; } = new()
	{
		Algorithm = RateLimitAlgorithm.TokenBucket,
		TokenLimit = 100,
		ReplenishmentPeriod = TimeSpan.FromSeconds(1),
		TokensPerPeriod = 100,
	};

	/// <summary>
	/// Gets or sets the global rate limit configuration.
	/// </summary>
	/// <value>
	/// The global rate limit configuration.
	/// </value>
	public RateLimitConfiguration GlobalLimit { get; set; } = new()
	{
		Algorithm = RateLimitAlgorithm.TokenBucket,
		TokenLimit = 1000,
		ReplenishmentPeriod = TimeSpan.FromSeconds(1),
		TokensPerPeriod = 1000,
	};

	/// <summary>
	/// Gets message type-specific rate limits.
	/// </summary>
	/// <value>The current <see cref="MessageTypeLimits"/> value.</value>
	public Dictionary<string, RateLimitConfiguration> MessageTypeLimits { get; init; } = [];

	/// <summary>
	/// Gets or sets message types that bypass rate limiting.
	/// </summary>
	/// <value>The current <see cref="BypassRateLimitingForTypes"/> value.</value>
	public string[]? BypassRateLimitingForTypes { get; set; }
}
