// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Validation;

/// <summary>
/// Validates <see cref="ContextValidationOptions"/> at startup.
/// </summary>
internal sealed class ContextValidationOptionsValidator : IValidateOptions<ContextValidationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, ContextValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxMessageAge is { } maxAge && maxAge <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(ContextValidationOptions.MaxMessageAge)} must be greater than zero when set.");
		}

		if (options.Checks.ValidateRequiredFields && options.RequiredFields.Count == 0)
		{
			failures.Add(
				$"{nameof(ContextValidationOptions.RequiredFields)} must contain at least one field when " +
				$"{nameof(ContextValidationOptions.Checks)}.{nameof(ContextValidationChecksOptions.ValidateRequiredFields)} is enabled.");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
