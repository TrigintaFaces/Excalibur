// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Validates <see cref="CacheOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks including:
/// <list type="bullet">
///   <item><description>DefaultExpiration must be positive</description></item>
///   <item><description>CacheTimeout must be positive and less than DefaultExpiration</description></item>
///   <item><description>JitterRatio must be between 0.0 and 1.0</description></item>
/// </list>
/// </remarks>
public sealed class CacheOptionsValidator : IValidateOptions<CacheOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CacheOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.Behavior.DefaultExpiration <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CacheBehaviorOptions.DefaultExpiration)} must be positive (was {options.Behavior.DefaultExpiration}).");
		}

		if (options.Behavior.CacheTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CacheBehaviorOptions.CacheTimeout)} must be positive (was {options.Behavior.CacheTimeout}).");
		}

		if (options.Behavior.JitterRatio is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(CacheBehaviorOptions.JitterRatio)} must be between 0.0 and 1.0 (was {options.Behavior.JitterRatio}).");
		}

		// Cross-property: CacheTimeout should be less than DefaultExpiration
		if (options.Behavior.CacheTimeout > TimeSpan.Zero && options.Behavior.DefaultExpiration > TimeSpan.Zero &&
			options.Behavior.CacheTimeout >= options.Behavior.DefaultExpiration)
		{
			failures.Add(
				$"{nameof(CacheBehaviorOptions.CacheTimeout)} ({options.Behavior.CacheTimeout}) " +
				$"must be less than {nameof(CacheBehaviorOptions.DefaultExpiration)} ({options.Behavior.DefaultExpiration}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
