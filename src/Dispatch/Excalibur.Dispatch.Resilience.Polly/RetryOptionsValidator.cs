// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="RetryOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class RetryOptionsValidator : IValidateOptions<RetryOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, RetryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.MaxRetries is < 0 or > 100)
		{
			failures.Add($"{nameof(RetryOptions.MaxRetries)} must be between 0 and 100 (was {options.MaxRetries}).");
		}

		if (options.JitterFactor is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(RetryOptions.JitterFactor)} must be between 0.0 and 1.0 (was {options.JitterFactor}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
