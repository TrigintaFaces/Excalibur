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
/// This adapter maps Polly's <see cref="DelayBackoffType" /> vocabulary onto Dispatch's
/// <see cref="IBackoffCalculator" /> seam, so a consumer who has chosen the Polly package selects a growth
/// law by the same name Polly uses.
/// </para>
/// <para>
/// <b>It computes the delays itself, and that is forced rather than preferred.</b> Polly exposes no public
/// API that yields the delay for an arbitrary attempt: the computation lives in
/// <c>Polly.Retry.RetryHelper</c>, which is <see langword="internal" /> (verified against
/// Polly.Core 8.6.6 assembly metadata -- it is absent from <c>GetExportedTypes()</c>), and a Polly delay
/// otherwise exists only inside a running pipeline's retry loop. An <see cref="IBackoffCalculator" /> is
/// asked "what delay for attempt N" with no execution in flight, so there is nothing to delegate to.
/// </para>
/// <para>
/// <b>Consequently the jitter here is AWS full jitter, NOT Polly's DecorrelatedJitterBackoffV2.</b> This
/// used to be documented the other way round, which was false in a way no test could catch: Polly's own
/// XML documents DecorrelatedJitterBackoffV2 as distributing between 0 and roughly 1.5 to 2 times the
/// nominal delay, whereas this samples uniformly over <c>[0, nominal]</c>. Both reach the low end, which is
/// the property <see cref="IBackoffCalculator" /> actually requires; they are not the same distribution,
/// and claiming Polly's algorithm while shipping another is the kind of divergence this seam exists to
/// prevent. The retry EXECUTION path in this package -- <c>PollyRetryPolicyAdapter</c> -- does use a real
/// <c>RetryStrategyOptions</c> with Polly's own <c>UseJitter</c>, so a consumer who wants Polly's exact
/// distribution gets it there.
/// </para>
/// <para>
/// For consumers who don't need Polly's strategies, the core package provides
/// <c>ExponentialBackoffCalculator</c> which has no external dependencies.
/// </para>
/// </remarks>
internal sealed class PollyBackoffCalculatorAdapter : IBackoffCalculator
{
	private readonly DelayBackoffType _backoffType;
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly bool _useJitter;
	private readonly double _factor;

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
	/// Creates an exponential calculator with full jitter applied.
	/// </summary>
	/// <param name="firstRetryDelay">The delay for the first retry, before jitter.</param>
	/// <param name="maxDelay">The maximum delay cap.</param>
	/// <returns>A new adapter configured for exponential growth with full jitter.</returns>
	/// <remarks>
	/// <b>Renamed from <c>CreateDecorrelatedJitter</c>, which it never was.</b> That name and its
	/// documentation promised Polly's DecorrelatedJitterBackoffV2 and returned this -- AWS full jitter,
	/// uniform over <c>[0, nominal]</c>, computed here. Polly's algorithm is unreachable from outside
	/// Polly; see the remarks on the type. The parameter was also called <c>medianFirstRetryDelay</c>,
	/// which described the decorrelated algorithm's median-seeking behaviour rather than what this
	/// computes: full jitter over an exponential ladder has a MEAN of half the nominal, not a median at
	/// it, so a caller sizing their first retry from that name got half what they asked for.
	/// </remarks>
	public static PollyBackoffCalculatorAdapter CreateJitteredExponential(
		TimeSpan firstRetryDelay,
		TimeSpan maxDelay) =>
		new(DelayBackoffType.Exponential, firstRetryDelay, maxDelay, true);

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

		// No state to reset. This used to acquire a lock here and clear the previous-delay field that the
		// decorrelated-jitter path threaded forward; that path is gone and the calculator is stateless, so
		// what remained was an empty lock block -- an acquire and release that guarded nothing.
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
