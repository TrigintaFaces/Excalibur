// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Microsoft.Extensions.Options;

namespace Excalibur.A3.Authorization;

/// <summary>
/// Refuses an authorization cache bound that would not bound anything.
/// </summary>
internal sealed class AuthorizationCacheOptionsValidator : IValidateOptions<AuthorizationCacheOptions>
{
	public ValidateOptionsResult Validate(string? name, AuthorizationCacheOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.AbsoluteExpirationRelativeToNow > TimeSpan.Zero
			? ValidateOptionsResult.Success
			: ValidateOptionsResult.Fail(
				$"{nameof(AuthorizationCacheOptions.AbsoluteExpirationRelativeToNow)} must be a positive duration. It is "
				+ "the longest a revoked grant can still authorize through the cache, so a zero or negative value "
				+ "cannot express that bound.");
	}
}
