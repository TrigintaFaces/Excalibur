// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates exponential backoff delays with optional jitter for retry operations.
/// </summary>
/// <remarks>
/// <para>
/// Exponential backoff increases the delay between retries exponentially, which helps prevent overwhelming a recovering service. Adding
/// jitter randomizes the delays to prevent the "thundering herd" problem where multiple clients retry simultaneously.
/// </para>
/// <para> The formula used is: delay = min(baseDelay * multiplier^(attempt-1) +/- jitter, maxDelay) </para>
/// </remarks>
internal sealed class ExponentialBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly double _multiplier;
	private readonly bool _enableJitter;
	private readonly double _jitterFactor;
	private readonly Func<double> _jitterSource;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with default options.
	/// </summary>
	public ExponentialBackoffCalculator()
		: this(new RetryPolicyOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class.
	/// </summary>
	/// <param name="options"> The retry policy options containing backoff configuration. </param>
	public ExponentialBackoffCalculator(RetryPolicyOptions options)
		: this(
			options?.Backoff.BaseDelay ?? TimeSpan.FromSeconds(1),
			options?.Backoff.MaxDelay ?? TimeSpan.FromMinutes(30),
			options?.Backoff.BackoffMultiplier ?? 2.0,
			options?.Backoff.EnableJitter ?? false,
			options?.Backoff.JitterFactor ?? 0.1)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with explicit parameters.
	/// </summary>
	/// <param name="baseDelay"> The base delay for the first retry. </param>
	/// <param name="maxDelay"> The maximum delay cap. </param>
	/// <param name="multiplier"> The multiplier for exponential growth (typically 2.0). </param>
	/// <param name="enableJitter"> Whether to add random jitter to delays. </param>
	/// <param name="jitterFactor"> The jitter factor (0.0 to 1.0) for randomization. </param>
	/// <param name="jitterSource">
	/// Optional source of jitter randomness producing values in <c>[0.0, 1.0)</c>. When <see langword="null"/>,
	/// <see cref="Random.Shared"/> is used. Inject a seeded/controllable source to make jitter deterministic for
	/// testing (the delay becomes a pure function of <c>attempt</c> and the seeded sequence).
	/// </param>
	public ExponentialBackoffCalculator(
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		double multiplier = 2.0,
		bool enableJitter = true,
		double jitterFactor = 0.1,
		Func<double>? jitterSource = null)
	{
		if (baseDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(baseDelay), Resources.ExponentialBackoffCalculator_BaseDelayCannotBeNegative);
		}

		if (maxDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDelay), Resources.ExponentialBackoffCalculator_MaxDelayCannotBeNegative);
		}

		if (multiplier < 1.0)
		{
			throw new ArgumentOutOfRangeException(nameof(multiplier), Resources.ExponentialBackoffCalculator_MultiplierMustBeAtLeastOne);
		}

		if (jitterFactor is < 0.0 or > 1.0)
		{
			throw new ArgumentOutOfRangeException(nameof(jitterFactor),
				Resources.ExponentialBackoffCalculator_JitterFactorMustBeBetweenZeroAndOne);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay;
		_multiplier = multiplier;
		_enableJitter = enableJitter;
		_jitterFactor = jitterFactor;
#pragma warning disable CA5394 // Do not use insecure randomness - jitter does not require cryptographic security
		_jitterSource = jitterSource ?? Random.Shared.NextDouble;
#pragma warning restore CA5394
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		// This calculator rejects a non-positive attempt (its documented contract), whereas the shared
		// ExponentialBackoff.Calculate clamps it — so validate here before delegating.
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(attempt), Resources.ExponentialBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		var parameters = new BackoffParameters
		{
			BaseDelay = _baseDelay,
			MaxDelay = _maxDelay,
			Multiplier = _multiplier,
			UseJitter = false,
			JitterFactor = _jitterFactor,
		};

		// No-jitter path: delegate the exponential + NaN-guard + cap math entirely to ExponentialBackoff,
		// the single source of truth for that computation (shared with every transport's backoff).
		if (!_enableJitter || _jitterFactor <= 0)
		{
			return ExponentialBackoff.Calculate(attempt, in parameters);
		}

		// Jitter path: this calculator applies jitter through an INJECTABLE source (so tests can make the
		// delay deterministic), a capability the shared helper — which always uses Random.Shared — does not
		// expose. The exponential base is computed the same way, then jitter is applied before the cap.
		var exponentialFactor = Math.Pow(_multiplier, attempt - 1);
		var delayMs = ApplyJitter(_baseDelay.TotalMilliseconds * exponentialFactor);

		// Guard NaN before the cap so the never-throws contract holds: a zero base delay times an overflowed
		// exponential factor is 0 * Infinity = NaN, which would propagate through Min/Max and make
		// TimeSpan.FromMilliseconds throw. NaN -> 0; a +Infinity from a positive base is capped by Min below.
		if (double.IsNaN(delayMs))
		{
			delayMs = 0;
		}

		delayMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);
		delayMs = Math.Max(0, delayMs);

		return TimeSpan.FromMilliseconds(delayMs);
	}

	/// <summary>
	/// Applies symmetric proportional jitter to the computed delay.
	/// </summary>
	/// <remarks>
	/// The delay is randomized within <c>±(delayMs × jitterFactor)</c> — i.e.
	/// <c>delayMs + ((random[0,1) × 2) − 1) × (delayMs × jitterFactor)</c> — spreading retry times across
	/// clients to avoid thundering-herd synchronization. Randomness comes from the injected jitter source
	/// (default <see cref="System.Random.Shared"/>); a seeded source makes the result deterministic for tests.
	/// See: https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/
	/// </remarks>
	private double ApplyJitter(double delayMs)
	{
		// Calculate the jitter range as a percentage of the delay
		var jitterRange = delayMs * _jitterFactor;

		// Apply random jitter: delay +/- (jitterRange * random). The jitter source produces values in
		// [0.0, 1.0); the default (Random.Shared) is thread-safe and suitable for non-cryptographic
		// scenarios like backoff jitter. A seeded source makes the delay deterministic for tests.
		var jitter = ((_jitterSource() * 2) - 1) * jitterRange;

		return delayMs + jitter;
	}

	/// <summary>
	/// Creates a calculator with recommended settings for high-throughput scenarios.
	/// </summary>
	/// <returns> A calculator with short base delay and full jitter. </returns>
	public static ExponentialBackoffCalculator CreateForHighThroughput()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(10),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5);
	}

	/// <summary>
	/// Creates a calculator with recommended settings for message queue retries.
	/// </summary>
	/// <returns> A calculator suitable for message processing retries. </returns>
	public static ExponentialBackoffCalculator CreateForMessageQueue()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(5),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.25);
	}

	/// <summary>
	/// Creates a calculator with aggressive retry settings for transient failures.
	/// </summary>
	/// <returns> A calculator with short delays for transient failure scenarios. </returns>
	public static ExponentialBackoffCalculator CreateForTransientFailures()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(50),
			maxDelay: TimeSpan.FromSeconds(5),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.3);
	}
}
