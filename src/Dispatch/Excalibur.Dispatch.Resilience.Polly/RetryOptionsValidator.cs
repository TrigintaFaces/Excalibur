// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="PollyRetryOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class RetryOptionsValidator : IValidateOptions<PollyRetryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, PollyRetryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxRetryAttempts is < 0 or > 100)
		{
			failures.Add($"{nameof(PollyRetryOptions.MaxRetryAttempts)} must be between 0 and 100 (was {options.MaxRetryAttempts}).");
		}

		// The JitterFactor range check was removed with the property. It validated a value nothing read,
		// which is the most misleading shape a validator can have: startup validation is the signal a
		// consumer trusts to mean "this option is honoured", and here it certified one that was inert.

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
