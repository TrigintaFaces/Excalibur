// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Security;

/// <summary>
/// Validates <see cref="InputValidationOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class InputValidationOptionsValidator : IValidateOptions<InputValidationOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, InputValidationOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxStringLength < 1)
		{
			failures.Add($"{nameof(InputValidationOptions.MaxStringLength)} must be >= 1 (was {options.MaxStringLength}).");
		}

		if (options.MaxMessageSizeBytes < 1)
		{
			failures.Add($"{nameof(InputValidationOptions.MaxMessageSizeBytes)} must be >= 1 (was {options.MaxMessageSizeBytes}).");
		}

		if (options.MaxObjectDepth < 1)
		{
			failures.Add($"{nameof(InputValidationOptions.MaxObjectDepth)} must be >= 1 (was {options.MaxObjectDepth}).");
		}

		if (options.MaxMessageAgeDays < 1)
		{
			failures.Add($"{nameof(InputValidationOptions.MaxMessageAgeDays)} must be >= 1 (was {options.MaxMessageAgeDays}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
