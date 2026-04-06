// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Validates <see cref="DistributedCircuitBreakerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// Replaces DataAnnotations validation with explicit checks.
/// </summary>
internal sealed class DistributedCircuitBreakerOptionsValidator : IValidateOptions<DistributedCircuitBreakerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, DistributedCircuitBreakerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		if (options.FailureRatio is < 0.0 or > 1.0)
		{
			failures.Add($"{nameof(DistributedCircuitBreakerOptions.FailureRatio)} must be between 0.0 and 1.0 (was {options.FailureRatio}).");
		}

		if (options.MinimumThroughput < 1)
		{
			failures.Add($"{nameof(DistributedCircuitBreakerOptions.MinimumThroughput)} must be >= 1 (was {options.MinimumThroughput}).");
		}

		if (options.ConsecutiveFailureThreshold < 1)
		{
			failures.Add($"{nameof(DistributedCircuitBreakerOptions.ConsecutiveFailureThreshold)} must be >= 1 (was {options.ConsecutiveFailureThreshold}).");
		}

		if (options.SuccessThresholdToClose < 1)
		{
			failures.Add($"{nameof(DistributedCircuitBreakerOptions.SuccessThresholdToClose)} must be >= 1 (was {options.SuccessThresholdToClose}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
