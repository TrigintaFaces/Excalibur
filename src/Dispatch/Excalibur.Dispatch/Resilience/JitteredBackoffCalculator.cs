// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Decorator that adds random jitter to any <see cref="IBackoffCalculator"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Jitter prevents the "thundering herd" problem by randomizing retry delays so that
/// multiple clients do not retry simultaneously. This decorator wraps any backoff calculator
/// and applies a configurable jitter factor.
/// </para>
/// <para>
/// The jitter range is: delay +/- (delay * jitterFactor * random).
/// For example, with jitterFactor=0.25 and a 1000ms delay, the result is between 750ms and 1250ms.
/// </para>
/// </remarks>
internal sealed class JitteredBackoffCalculator : IBackoffCalculator
{
	private readonly IBackoffCalculator _inner;
	private readonly double _jitterFactor;

	/// <summary>
	/// Initializes a new instance of the <see cref="JitteredBackoffCalculator"/> class.
	/// </summary>
	/// <param name="inner">The inner backoff calculator to wrap.</param>
	/// <param name="jitterFactor">The jitter factor (0.0 to 1.0). Defaults to 0.25.</param>
	public JitteredBackoffCalculator(IBackoffCalculator inner, double jitterFactor = 0.25)
	{
		_inner = inner ?? throw new ArgumentNullException(
			nameof(inner),
			Resources.JitteredBackoffCalculator_InnerCalculatorRequired);

		if (jitterFactor is < 0.0 or > 1.0)
		{
			throw new ArgumentOutOfRangeException(
				nameof(jitterFactor),
				Resources.JitteredBackoffCalculator_JitterFactorMustBeBetweenZeroAndOne);
		}

		_jitterFactor = jitterFactor;
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		var baseDelay = _inner.CalculateDelay(attempt);

		if (_jitterFactor == 0.0)
		{
			return baseDelay;
		}

		var delayMs = baseDelay.TotalMilliseconds;
		var jitterRange = delayMs * _jitterFactor;

		// Apply random jitter: delay +/- (jitterRange * random)
		// Random.Shared is thread-safe and suitable for non-cryptographic scenarios like backoff jitter
#pragma warning disable CA5394 // Do not use insecure randomness - jitter does not require cryptographic security
		var jitter = ((Random.Shared.NextDouble() * 2) - 1) * jitterRange;
#pragma warning restore CA5394

		delayMs = Math.Max(0, delayMs + jitter);

		return TimeSpan.FromMilliseconds(delayMs);
	}
}
