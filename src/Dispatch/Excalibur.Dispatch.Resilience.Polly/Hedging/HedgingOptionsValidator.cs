// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// AOT-safe validator for <see cref="HedgingOptions"/>.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class HedgingOptionsValidator : IValidateOptions<HedgingOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, HedgingOptions options)
	{
		if (options.MaxHedgedAttempts is < 1 or > 10)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(options.MaxHedgedAttempts)} must be between 1 and 10.");
		}

		return ValidateOptionsResult.Success;
	}
}
