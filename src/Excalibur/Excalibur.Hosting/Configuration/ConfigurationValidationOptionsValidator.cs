// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Hosting.Configuration;

/// <summary>Validates <see cref="ConfigurationValidationOptions"/> at startup. Reflection-free (AOT-safe).</summary>
internal sealed class ConfigurationValidationOptionsValidator : IValidateOptions<ConfigurationValidationOptions>
{
	/// <inheritdoc/>
	public ValidateOptionsResult Validate(string? name, ConfigurationValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return options.ValidationTimeout <= TimeSpan.Zero
			? ValidateOptionsResult.Fail($"{nameof(ConfigurationValidationOptions.ValidationTimeout)} must be greater than zero.")
			: ValidateOptionsResult.Success;
	}
}
