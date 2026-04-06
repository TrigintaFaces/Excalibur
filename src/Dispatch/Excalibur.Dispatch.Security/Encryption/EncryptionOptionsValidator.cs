// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// AOT-safe validator for <see cref="EncryptionOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class EncryptionOptionsValidator : IValidateOptions<EncryptionOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, EncryptionOptions options)
	{
		if (options.KeyRotationIntervalDays < 1)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.KeyRotationIntervalDays)} must be at least 1.");
		}

		return ValidateOptionsResult.Success;
	}
}
