// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// AOT-safe validator for <see cref="RateLimitingOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class RateLimitingOptionsValidator : IValidateOptions<RateLimitingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RateLimitingOptions options)
	{
		var failures = new List<string>();

		if (options.DefaultRetryAfterMilliseconds < 1)
		{
			failures.Add($"{nameof(options.DefaultRetryAfterMilliseconds)} must be at least 1.");
		}

		if (options.CleanupIntervalMinutes < 1)
		{
			failures.Add($"{nameof(options.CleanupIntervalMinutes)} must be at least 1.");
		}

		if (options.InactivityTimeoutMinutes < 1)
		{
			failures.Add($"{nameof(options.InactivityTimeoutMinutes)} must be at least 1.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
