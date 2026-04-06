// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Compliance;

/// <summary>
/// Validates <see cref="Soc2Options"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class Soc2OptionsValidator : IValidateOptions<Soc2Options>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, Soc2Options options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.DefaultTestSampleSize < 1)
		{
			failures.Add($"{nameof(Soc2Options.DefaultTestSampleSize)} must be >= 1 (was {options.DefaultTestSampleSize}).");
		}

		if (options.MinimumTypeIIPeriodDays < 1)
		{
			failures.Add($"{nameof(Soc2Options.MinimumTypeIIPeriodDays)} must be >= 1 (was {options.MinimumTypeIIPeriodDays}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
