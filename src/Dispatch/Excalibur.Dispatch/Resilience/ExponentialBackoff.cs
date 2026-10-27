// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Provides a stateless, allocation-free exponential backoff delay calculation.
/// </summary>
/// <remarks>
/// <para>
/// This is the single source of truth for the exponential backoff math used by transports and retry strategies.
/// The computed delay is exactly <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>.
/// </para>
/// <para>
/// <b>It computes the NOMINAL delay and applies no randomisation, which is what makes it genuinely pure.</b>
/// It previously applied a symmetric jitter band here, and the "pure and total" claim below was false while
/// it did -- the method called <see cref="Random.Shared" />, so a caller who read the documentation and
/// cached a result got a value the next call would not reproduce. Randomisation now belongs solely to the
/// calculator obtained from <see cref="BackoffCalculatorFactory" />, so there is one jitter implementation
/// in this package rather than a second one reachable through a flag on a value type.
/// </para>
/// <para>
/// The function is pure and total: same inputs, same output, and it never throws. Out-of-range inputs are
/// clamped (see <see cref="Calculate(int, in BackoffParameters)" />) rather than rejected.
/// </para>
/// </remarks>
public static class ExponentialBackoff
{
	/// <summary>
	/// Calculates the exponential backoff delay for the given attempt.
	/// </summary>
	/// <param name="attempt">The 1-based retry attempt number. Values below <c>1</c> are treated as <c>1</c>.</param>
	/// <param name="parameters">The backoff schedule parameters.</param>
	/// <returns>
	/// A non-negative delay equal to <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>.
	/// </returns>
	/// <remarks>
	/// This method is pure and total: it never throws, and the same arguments always produce the same delay.
	/// <paramref name="attempt" /> is clamped to at least <c>1</c>,
	/// <see cref="BackoffParameters.Multiplier" /> is clamped up to <c>1.0</c> when smaller, the result is capped at
	/// <see cref="BackoffParameters.MaxDelay" /> and is never negative. To randomise the result, use the
	/// calculator from <see cref="BackoffCalculatorFactory" /> rather than post-processing this value: it
	/// samples BELOW the already-capped delay, which is the ordering that keeps jitter alive once the
	/// nominal ladder passes the cap.
	/// </remarks>
	public static TimeSpan Calculate(int attempt, in BackoffParameters parameters)
	{
		// Clamp attempt: treat anything below 1 as the first attempt.
		if (attempt < 1)
		{
			attempt = 1;
		}

		// Clamp multiplier up to 1.0 so the delay never shrinks below the base.
		var multiplier = parameters.Multiplier < 1.0 ? 1.0 : parameters.Multiplier;

		// Calculate exponential delay: baseDelay * multiplier^(attempt-1)
		var exponentialFactor = Math.Pow(multiplier, attempt - 1);
		var delayMs = parameters.BaseDelay.TotalMilliseconds * exponentialFactor;

		// Guard NaN before the cap so the pure/total never-throws contract holds. A zero BaseDelay times an
		// overflowed exponential factor yields 0 * Infinity = NaN, which would propagate through Min/Max and
		// make TimeSpan.FromMilliseconds throw. NaN -> 0 (a zero base means zero delay). A positive base that
		// overflows to +Infinity is already capped by the Min below.
		if (double.IsNaN(delayMs))
		{
			delayMs = 0;
		}

		// Clamp to max delay.
		delayMs = Math.Min(delayMs, parameters.MaxDelay.TotalMilliseconds);

		// Ensure non-negative.
		delayMs = Math.Max(0, delayMs);

		return TimeSpan.FromMilliseconds(delayMs);
	}
}
