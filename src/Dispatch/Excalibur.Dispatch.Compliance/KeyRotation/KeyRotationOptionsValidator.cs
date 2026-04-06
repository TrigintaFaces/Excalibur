// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Validates <see cref="KeyRotationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class KeyRotationOptionsValidator : IValidateOptions<KeyRotationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, KeyRotationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.MaxConcurrentRotations < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(KeyRotationOptions.MaxConcurrentRotations)} must be >= 1 (was {options.MaxConcurrentRotations}).");
		}

		return ValidateOptionsResult.Success;
	}
}
