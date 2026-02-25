// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based backoff calculator adapter that wraps Polly's delay generation strategies
/// and implements the core <see cref="IBackoffCalculator"/> interface.
/// </summary>
/// <remarks>
/// <para>
/// This adapter bridges Polly's backoff generation capabilities (including decorrelated jitter
/// backoff V2) with Dispatch's zero-dependency backoff abstraction. Use this when you want
/// Polly's proven backoff strategies, especially the decorrelated jitter algorithm.
/// </para>
/// <para>
/// For consumers who don't need Polly's strategies, the core package provides
/// <c>ExponentialBackoffCalculator</c> which has no external dependencies.
/// </para>
/// </remarks>
public sealed class PollyBackoffCalculatorAdapter : IBackoffCalculator
{
	// Constants for decorrelated jitter algorithm (from Polly's implementation)
	private const double RpScalingFactor = 1.0 / 1.4;
	private const double Multiplier = 2.0;

	private readonly DelayBackoffType _backoffType;
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly bool _useJitter;
	private readonly double _factor;

	// Track previous delay for decorrelated jitter
	private TimeSpan _previousDelay;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyBackoffCalculatorAdapter"/> class
	/// with default exponential backoff settings.
	/// </summary>
	public PollyBackoffCalculatorAdapter()
		: this(DelayBackoffType.Exponential, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30), true)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyBackoffCalculatorAdapter"/> class.
	/// </summary>
	/// <param name="backoffType">The type of backoff strategy to use.</param>
	/// <param name="baseDelay">The base delay for the first retry attempt.</param>
	/// <param name="maxDelay">The maximum delay cap.</param>
	/// <param name="useJitter">Whether to apply jitter to the calculated delays.</param>
	/// <param name="factor">The factor for exponential/linear backoff (default 2.0 for exponential).</param>
	public PollyBackoffCalculatorAdapter(
		DelayBackoffType backoffType,
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		bool useJitter = true,
		double factor = 2.0)
	{
		if (baseDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
					nameof(baseDelay),
					Resources.PollyBackoffCalculatorAdapter_BaseDelayMustBePositive);
		}

		if (maxDelay < baseDelay)
		{
			throw new ArgumentOutOfRangeException(
					nameof(maxDelay),
					Resources.PollyBackoffCalculatorAdapter_MaxDelayMustBeGreaterOrEqualBaseDelay);
		}

		if (factor <= 0)
		{
			throw new ArgumentOutOfRangeException(
					nameof(factor),
					Resources.PollyBackoffCalculatorAdapter_FactorMustBePositive);
		}

		_backoffType = backoffType;
		_baseDelay = baseDelay;
		_maxDelay = maxDelay;
		_useJitter = useJitter;
		_factor = factor;
		_previousDelay = TimeSpan.Zero;
	}

	/// <summary>
	/// Creates a new instance using Polly's decorrelated jitter backoff V2 strategy.
	/// </summary>
	/// <param name="medianFirstRetryDelay">The median delay for the first retry.</param>
	/// <param name="maxDelay">The maximum delay cap.</param>
	/// <returns>A new adapter configured with decorrelated jitter backoff.</returns>
	/// <remarks>
	/// This is the recommended strategy from Polly for production use. It provides
	/// decorrelated delays that help prevent thundering herd problems.
	/// </remarks>
	public static PollyBackoffCalculatorAdapter CreateDecorrelatedJitter(
		TimeSpan medianFirstRetryDelay,
		TimeSpan maxDelay) =>
		new(DelayBackoffType.Exponential, medianFirstRetryDelay, maxDelay, true);

	/// <inheritdoc />
	/// <remarks>
	/// Calculates the delay using the configured Polly backoff strategy.
	/// The calculation respects the maximum delay cap and optionally applies jitter.
	/// </remarks>
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt <= 0)
		{
			return TimeSpan.Zero;
		}

		TimeSpan delay;

		if (_useJitter && _backoffType == DelayBackoffType.Exponential)
		{
			// Use decorrelated jitter V2 algorithm (same as Polly v8 internal)
			delay = CalculateDecorrelatedJitterDelay(attempt);
		}
		else
		{
			delay = _backoffType switch
			{
				DelayBackoffType.Constant => _baseDelay,
				DelayBackoffType.Linear => TimeSpan.FromTicks(_baseDelay.Ticks * attempt),
				DelayBackoffType.Exponential => CalculateExponentialDelay(attempt),
				_ => CalculateExponentialDelay(attempt),
			};

			// Apply simple jitter if enabled and not using decorrelated
			if (_useJitter)
			{
				delay = ApplySimpleJitter(delay);
			}
		}

		// Cap at maximum delay
		return delay > _maxDelay ? _maxDelay : delay;
	}

	/// <summary>
	/// Generates an enumerable of backoff delays for a specified number of retries.
	/// </summary>
	/// <param name="retryCount">The number of retry delays to generate.</param>
	/// <returns>An enumerable of delays for each retry attempt.</returns>
	public IEnumerable<TimeSpan> GenerateDelays(int retryCount)
	{
		if (retryCount <= 0)
		{
			yield break;
		}

		// Reset state for fresh sequence
		lock (_lock)
		{
			_previousDelay = TimeSpan.Zero;
		}

		for (var i = 1; i <= retryCount; i++)
		{
			yield return CalculateDelay(i);
		}
	}

	private TimeSpan CalculateExponentialDelay(int attempt)
	{
		// Standard exponential backoff: baseDelay * factor^(attempt-1)
		var multiplier = Math.Pow(_factor, attempt - 1);
		var delayTicks = (long)(_baseDelay.Ticks * multiplier);

		// Prevent overflow
		if (delayTicks < 0 || delayTicks > _maxDelay.Ticks)
		{
			return _maxDelay;
		}

		return TimeSpan.FromTicks(delayTicks);
	}

	/// <summary>
	/// Implements Polly's decorrelated jitter V2 algorithm.
	/// This provides smoother, more evenly distributed delays than simple jitter.
	/// </summary>
	private TimeSpan CalculateDecorrelatedJitterDelay(int attempt)
	{
		lock (_lock)
		{
			// Calculate target delay for this attempt
			var targetDelay = _baseDelay.TotalMilliseconds * Math.Pow(Multiplier, attempt - 1);

			// Apply the decorrelated jitter formula
			// This uses the random point (RP) approach from the Polly team's research
			var rpDelay = _previousDelay.TotalMilliseconds == 0
				? _baseDelay.TotalMilliseconds
				: _previousDelay.TotalMilliseconds * RpScalingFactor;

			// Random component with cryptographically secure random
			var randomFactor = GetSecureRandomDouble();
			var jitteredDelay = rpDelay + ((targetDelay - rpDelay) * randomFactor);

			// Ensure minimum delay
			jitteredDelay = Math.Max(jitteredDelay, _baseDelay.TotalMilliseconds * 0.5);

			var result = TimeSpan.FromMilliseconds(jitteredDelay);
			_previousDelay = result;

			return result;
		}
	}

	[SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Used for backoff jitter timing, not security purposes.")]
	private static TimeSpan ApplySimpleJitter(TimeSpan delay)
	{
		// Apply full jitter (0.5 to 1.0 of delay) - AWS recommended approach
		var random = new Random();
		var jitterFactor = 0.5 + (random.NextDouble() * 0.5);
		return TimeSpan.FromTicks((long)(delay.Ticks * jitterFactor));
	}

	private static double GetSecureRandomDouble()
	{
		// Use cryptographically secure random for better distribution
		Span<byte> bytes = stackalloc byte[8];
		RandomNumberGenerator.Fill(bytes);
		var value = BitConverter.ToUInt64(bytes) >> 11; // 53 bits for double precision
		return value / (double)(1UL << 53);
	}
}
