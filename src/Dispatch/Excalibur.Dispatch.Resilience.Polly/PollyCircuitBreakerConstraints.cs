// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Globalization;

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Rejects circuit breaker settings the ratio-based provider cannot express, so a value that would be
/// silently reinterpreted — or rejected with a message naming only the underlying library's own types —
/// fails at construction with a diagnostic that names the setting the caller actually supplied.
/// </summary>
internal static class PollyCircuitBreakerConstraints
{
	/// <summary>
	/// The fewest calls a ratio-based breaker can evaluate a failure proportion over. A single call
	/// admits no proportion, so the provider rejects a lower minimum throughput.
	/// </summary>
	private const int MinimumObservableCalls = 2;

	/// <summary>Throws when <paramref name="options" /> asks for behaviour the provider cannot implement.</summary>
	/// <param name="options"> The circuit breaker configuration supplied by the caller. </param>
	/// <param name="circuitName"> The circuit name, used to identify the offending registration. </param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when a setting has no expression on this provider. </exception>
	internal static void ThrowIfNotExpressible(CircuitBreakerOptions options, string circuitName)
	{
		ArgumentNullException.ThrowIfNull(options);

		if (options.FailureThreshold < MinimumObservableCalls)
		{
			throw new ArgumentOutOfRangeException(
				nameof(options),
				options.FailureThreshold,
				string.Format(
					CultureInfo.InvariantCulture,
					"Circuit breaker '{0}' sets {1}={2}, which this provider cannot implement: it measures a "
					+ "failure proportion over a rolling window and so requires at least {3} observed calls. "
					+ "Use {1} >= {3} with {4}=1.0 to open once every observed call in the window has failed, "
					+ "or select a count-based circuit breaker to open on the first failure.",
					circuitName,
					nameof(CircuitBreakerOptions.FailureThreshold),
					options.FailureThreshold,
					MinimumObservableCalls,
					nameof(CircuitBreakerOptions.FailureRatio)));
		}
	}
}
