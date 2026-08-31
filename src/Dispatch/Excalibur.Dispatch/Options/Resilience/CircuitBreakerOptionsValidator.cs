// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Options.Resilience;

/// <summary>
/// Validates <see cref="CircuitBreakerOptions"/> at startup via the <c>ValidateOnStart</c> pipeline.
/// </summary>
/// <remarks>
/// Performs cross-property constraint checks for circuit breaker configuration.
/// </remarks>
public sealed class CircuitBreakerOptionsValidator : IValidateOptions<CircuitBreakerOptions>
{
	/// <summary>The shortest rolling window a ratio-based circuit breaker provider accepts.</summary>
	private static readonly TimeSpan MinimumSamplingDuration = TimeSpan.FromMilliseconds(500);

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

		// FailureRatio must be a usable proportion. Zero would open the circuit on an empty window.
		if (options.FailureRatio is <= 0.0 or > 1.0)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.FailureRatio)} must be greater than 0 and at most 1 " +
				$"(was {options.FailureRatio.ToString(CultureInfo.InvariantCulture)}).");
		}

		// SamplingDuration is a rolling window; ratio-based providers require at least 500ms.
		if (options.SamplingDuration < MinimumSamplingDuration)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.SamplingDuration)} must be at least {MinimumSamplingDuration} " +
				$"(was {options.SamplingDuration}).");
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

		// Cross-property: OperationTimeout should not exceed OpenDuration (operations would always timeout during probe)
		if (options.OperationTimeout >= options.OpenDuration)
		{
			failures.Add(
				$"{nameof(CircuitBreakerOptions.OperationTimeout)} ({options.OperationTimeout}) should be less than " +
				$"{nameof(CircuitBreakerOptions.OpenDuration)} ({options.OpenDuration}).");
		}

		return failures.Count > 0
			? ValidateOptionsResult.Fail(failures)
			: ValidateOptionsResult.Success;
	}
}
