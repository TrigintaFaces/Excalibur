// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Vault;

/// <summary>
/// Validates <see cref="VaultOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class VaultOptionsValidator : IValidateOptions<VaultOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, VaultOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.VaultUri is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(VaultOptions.VaultUri)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
