// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="TimeoutManagerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class TimeoutManagerOptionsValidator : IValidateOptions<TimeoutManagerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, TimeoutManagerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.SlowOperationThreshold is < 0.0 or > 1.0)
		{
			return ValidateOptionsResult.Fail(
				$"{nameof(TimeoutManagerOptions.SlowOperationThreshold)} must be between 0.0 and 1.0 (was {options.SlowOperationThreshold}).");
		}

		return ValidateOptionsResult.Success;
	}
}
