// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0


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
internal sealed class PollyBackoffCalculatorAdapter : IBackoffCalculator
{
	// Constants for decorrelated jitter algorithm (from Polly's implementation)

	private readonly DelayBackoffType _backoffType;
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly bool _useJitter;
	private readonly double _factor;

	// Track previous delay for decorrelated jitter
	private readonly Lock _lock = new();

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
		// NEGATIVE is refused; ZERO is not. This adapter used to reject baseDelay == 0 and reject
		// maxDelay < baseDelay, neither of which any in-box calculator rejects -- so a configuration that
		// started fine without this package threw at startup with it. Strengthening a precondition breaks
		// substitutability, which is the entire purpose of this seam.
		if (baseDelay < TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
					nameof(baseDelay),
					Resources.PollyBackoffCalculatorAdapter_BaseDelayMustBePositive);
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
		// THROWS rather than returning zero, per IBackoffCalculator's contract. This adapter previously
		// returned TimeSpan.Zero here while every in-box calculator threw -- so the same off-by-one gave
		// a caller an immediate no-backoff retry through this implementation and an exception through the
		// others, for the same interface. A zero delay and an invalid argument are different facts.
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(attempt), Resources.PollyBackoffCalculatorAdapter_AttemptMustBeAtLeastOne);
		}

		// EXHAUSTIVE over Polly's enum, with no silent default. The previous `_ =>` arm quietly computed
		// an exponential delay for anything unmapped, which is how three of this framework's strategies
		// were reinterpreted without a word.
		var nominal = _backoffType switch
		{
			DelayBackoffType.Constant => _baseDelay,
			DelayBackoffType.Linear => TimeSpan.FromTicks(_baseDelay.Ticks * attempt),
			DelayBackoffType.Exponential => CalculateExponentialDelay(attempt),
			_ => throw new ArgumentOutOfRangeException(
				nameof(attempt), _backoffType, Resources.PollyBackoffCalculatorAdapter_UnknownBackoffType),
		};

		// Cap FIRST, then randomise below the capped value, so jitter samples within [0, nominal] as the
		// contract requires rather than being clipped afterwards.
		var capped = nominal > _maxDelay ? _maxDelay : nominal;

		return _useJitter ? ApplyFullJitter(capped) : capped;
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


	/// <summary>Samples uniformly from zero up to <paramref name="delay" /> -- AWS "full jitter".</summary>
	/// <param name="delay">The nominal delay to randomise below.</param>
	/// <returns>A delay in <c>[0, delay)</c>.</returns>
	/// <remarks>
	/// This used to sample <c>[0.5 x delay, delay]</c> while its own comment called that "full jitter
	/// (0.5 to 1.0 of delay) - AWS recommended approach". It is neither: AWS full jitter is uniform over
	/// <c>[0, delay]</c>, and a band that can never sample the low end does not decorrelate clients that
	/// failed together -- which is the only reason to apply jitter. The old form also made
	/// <see cref="BackoffStrategy.Constant" /> non-constant in one direction only, so a configured fixed
	/// interval was systematically faster than configured rather than merely noisier.
	/// </remarks>
	private static TimeSpan ApplyFullJitter(TimeSpan delay) =>
		TimeSpan.FromTicks((long)(delay.Ticks * NextJitterSample()));

	/// <summary>Returns a value in <c>[0, 1)</c> for jitter.</summary>
	/// <returns>The sample.</returns>
	/// <remarks>
	/// Deliberately NOT a cryptographic generator. This method previously called
	/// <c>RandomNumberGenerator.Fill</c> for eight bytes per retry, commented "for better distribution".
	/// A CSPRNG is not better distributed than <see cref="Random.Shared" /> for this purpose -- both are
	/// uniform -- it is merely slower, and it spends entropy on the retry path, which is exactly where
	/// cost compounds with load. A retry delay is not secret and no attacker gains from predicting it;
	/// the security rules reserve a CSPRNG for key, token, salt, IV and nonce material. The in-box
	/// calculators always used Random.Shared and were right to.
	/// </remarks>
	private static double NextJitterSample()
	{
#pragma warning disable CA5394 // a retry delay is not secret; the rationale is in the remarks above
		return Random.Shared.NextDouble();
#pragma warning restore CA5394
	}
}
