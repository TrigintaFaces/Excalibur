// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Data.IdentityMap.Diagnostics;

/// <summary>
/// Validates <see cref="IdentityMapCacheOptions"/> at startup so a misconfigured cache fails fast
/// instead of surfacing as a deep runtime error on first cache access.
/// </summary>
internal sealed class IdentityMapCacheOptionsValidator : IValidateOptions<IdentityMapCacheOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, IdentityMapCacheOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.AbsoluteExpiration <= TimeSpan.Zero)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(IdentityMapCacheOptions.AbsoluteExpiration)} must be greater than zero.");
		}

		if (options.SlidingExpiration is { } sliding)
		{
			if (sliding <= TimeSpan.Zero)
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(IdentityMapCacheOptions.SlidingExpiration)}, when set, must be greater than zero.");
			}

			if (sliding > options.AbsoluteExpiration)
			{
				return ValidateOptionsResult.Fail(
					$"{nameof(IdentityMapCacheOptions.SlidingExpiration)} must not exceed " +
					$"{nameof(IdentityMapCacheOptions.AbsoluteExpiration)}.");
			}
		}

		return ValidateOptionsResult.Success;
	}
}
