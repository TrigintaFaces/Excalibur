// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// AOT-safe validator for <see cref="SigningOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class SigningOptionsValidator : IValidateOptions<SigningOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, SigningOptions options)
	{
		var failures = new List<string>();

		if (options.MaxSignatureAgeMinutes < 1)
		{
			failures.Add($"{nameof(options.MaxSignatureAgeMinutes)} must be at least 1.");
		}

		if (options.KeyRotationIntervalDays < 1)
		{
			failures.Add($"{nameof(options.KeyRotationIntervalDays)} must be at least 1.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
