// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Validates <see cref="CircuitBreakerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks for circuit breaker configuration.
/// Sprint 561 S561.45.
/// </remarks>
public sealed class CircuitBreakerOptionsValidator : IValidateOptions<CircuitBreakerOptions>
{
	/// <inheritdoc />
	public ValidateOptionsResult Validate(string? name, CircuitBreakerOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		var failures = new List<string>();

		// FailureThreshold must be positive
		if (options.FailureThreshold < 1)
		{
			failures.Add($"{nameof(CircuitBreakerOptions.FailureThreshold)} must be >= 1 (was {options.FailureThreshold}).");
		}

		// SuccessThreshold must be positive
		if (options.SuccessThreshold < 1)
		{
			failures.Add($"{nameof(CircuitBreakerOptions.SuccessThreshold)} must be >= 1 (was {options.SuccessThreshold}).");
		}

		// OpenDuration must be positive
		if (options.OpenDuration <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CircuitBreakerOptions.OpenDuration)} must be positive (was {options.OpenDuration}).");
		}

		// OperationTimeout must be positive
		if (options.OperationTimeout <= TimeSpan.Zero)
		{
			failures.Add($"{nameof(CircuitBreakerOptions.OperationTimeout)} must be positive (was {options.OperationTimeout}).");
		}

		// MaxHalfOpenTests must be positive
		if (options.MaxHalfOpenTests < 1)
		{
			failures.Add($"{nameof(CircuitBreakerOptions.MaxHalfOpenTests)} must be >= 1 (was {options.MaxHalfOpenTests}).");
		}

		// Cross-property: OperationTimeout should not exceed OpenDuration (operations would always timeout during probe)
		if (options.OperationTimeout >= options.OpenDuration)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.OperationTimeout)} ({options.OperationTimeout}) should be less than " +
				$"{nameof(CircuitBreakerOptions.OpenDuration)} ({options.OpenDuration}).");
		}

		// Cross-property: SuccessThreshold should not exceed MaxHalfOpenTests
		if (options.SuccessThreshold > options.MaxHalfOpenTests)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.SuccessThreshold)} ({options.SuccessThreshold}) must not exceed " +
				$"{nameof(CircuitBreakerOptions.MaxHalfOpenTests)} ({options.MaxHalfOpenTests}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
