// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Provides a stateless, allocation-free exponential backoff delay calculation.
/// </summary>
/// <remarks>
/// <para>
/// This is the single source of truth for the exponential backoff math used by transports and retry strategies.
/// The computed delay is <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>, with optional symmetric jitter.
/// </para>
/// <para>
/// The function is pure and total: it never throws on the hot path and always returns a non-negative delay.
/// Out-of-range inputs are clamped (see <see cref="Calculate(int, in BackoffParameters)" />) rather than rejected.
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
	/// A non-negative delay equal to <c>min(BaseDelay * Multiplier^(attempt-1), MaxDelay)</c>, with symmetric
	/// jitter applied when <see cref="BackoffParameters.UseJitter" /> is enabled.
	/// </returns>
	/// <remarks>
	/// This method is pure and total: it never throws. <paramref name="attempt" /> is clamped to at least <c>1</c>,
	/// <see cref="BackoffParameters.Multiplier" /> is clamped up to <c>1.0</c> when smaller, the result is capped at
	/// <see cref="BackoffParameters.MaxDelay" /> and is never negative. When
	/// <see cref="BackoffParameters.UseJitter" /> is <see langword="false" /> the result is deterministic.
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

		// Apply symmetric jitter if enabled. This preserves the exact algorithm used by
		// ExponentialBackoffCalculator.ApplyJitter: delay +/- (delay * jitterFactor * [-1,1)).
		if (parameters.UseJitter && parameters.JitterFactor > 0)
		{
			var jitterRange = delayMs * parameters.JitterFactor;

			// Random.Shared is thread-safe and suitable for non-cryptographic backoff jitter.
#pragma warning disable CA5394 // Do not use insecure randomness - jitter does not require cryptographic security
			var jitter = ((Random.Shared.NextDouble() * 2) - 1) * jitterRange;
#pragma warning restore CA5394

			delayMs += jitter;
		}

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
