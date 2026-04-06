// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance.Azure;

/// <summary>
/// Validates <see cref="AzureKeyVaultOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class AzureKeyVaultOptionsValidator : IValidateOptions<AzureKeyVaultOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, AzureKeyVaultOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.VaultUri is null)
		{
			return ValidateOptionsResult.Fail($"{nameof(AzureKeyVaultOptions.VaultUri)} is required.");
		}

		return ValidateOptionsResult.Success;
	}
}
