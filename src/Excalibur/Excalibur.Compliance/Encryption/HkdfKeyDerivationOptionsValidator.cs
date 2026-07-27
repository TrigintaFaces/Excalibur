// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Encryption;

/// <summary>Validates <see cref="HkdfKeyDerivationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class HkdfKeyDerivationOptionsValidator : IValidateOptions<HkdfKeyDerivationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, HkdfKeyDerivationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.DefaultOutputLength < 1
			? ValidateOptionsResult.Fail($"{nameof(HkdfKeyDerivationOptions.DefaultOutputLength)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
