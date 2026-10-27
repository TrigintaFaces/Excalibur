// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Builds the <see cref="IBackoffCalculator" /> a retry configuration describes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Growth law, then jitter, in that order.</b> The strategy selects one of three growth laws and a
/// separate switch decides whether the result is randomised. Previously jitter was decided per strategy
/// arm: the exponential arm hardcoded it OFF, one arm consulted the option, and two strategy names implied
/// it. The out-of-box configuration therefore requested jitter and received none, silently, because the
/// default strategy's arm had already answered the question with a literal. Reading the switch once, here,
/// outside the growth-law selection, is what makes that state unreachable rather than fixed.
/// </para>
/// <para>
/// <b>This type is public because <see cref="IBackoffCalculator" /> was not obtainable.</b> The interface
/// has always been public while every implementation of it is <see langword="internal" />, so a consumer --
/// and any sibling package in this framework -- could name the contract and never acquire one. That is what
/// drove callers to the one public thing they could reach, the pure
/// <see cref="ExponentialBackoff.Calculate(int, in BackoffParameters)" />, and it is why a jitter flag grew
/// on <see cref="BackoffParameters" /> and gave this framework a second, divergent jitter implementation.
/// Supplying the factory removes the pressure that created that divergence instead of forbidding its
/// symptom.
/// </para>
/// <para>
/// <b>Jitter composes over every law, and it samples below the already-capped delay.</b> The returned
/// calculator caps to the configured maximum FIRST and randomises underneath it. The opposite order --
/// randomise, then cap -- silently annihilates jitter once the nominal ladder grows past the maximum,
/// because every sampled value then exceeds the cap and the result is exactly the maximum with probability
/// one. That is a thundering herd arriving in lockstep during precisely the sustained outage jitter exists
/// to spread, and it is what this framework shipped.
/// </para>
/// </remarks>
public static class BackoffCalculatorFactory
{
	/// <summary>
	/// Creates the calculator for the given strategy and retry options.
	/// </summary>
	/// <param name="strategy">The growth law to apply.</param>
	/// <param name="options">The retry options supplying the delays, multiplier and jitter switch.</param>
	/// <returns>The calculator.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="strategy" /> is not a declared <see cref="BackoffStrategy" />.
	/// </exception>
	public static IBackoffCalculator Create(BackoffStrategy strategy, RetryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return Create(
			strategy,
			options.BaseDelay,
			options.MaxDelay,
			options.BackoffMultiplier,
			options.UseJitter);
	}

	/// <summary>
	/// Creates the calculator for the given strategy and backoff schedule.
	/// </summary>
	/// <param name="strategy">The growth law to apply.</param>
	/// <param name="parameters">The schedule supplying the base delay, ceiling and multiplier.</param>
	/// <param name="useJitter">
	/// <see langword="true" /> to randomise each delay below its capped nominal value, decorrelating
	/// callers that failed together; otherwise <see langword="false" /> for a deterministic schedule.
	/// </param>
	/// <returns>The calculator.</returns>
	/// <remarks>
	/// Takes the schedule as a <see cref="BackoffParameters" /> so a caller that already holds one -- a
	/// transport mapping its own retry options, typically -- can obtain a calculator without restating the
	/// values. <see cref="BackoffParameters.Multiplier" /> is clamped up to <c>1.0</c> when smaller, which
	/// matches <see cref="ExponentialBackoff.Calculate(int, in BackoffParameters)" /> and that property's
	/// documented behaviour; a default-constructed schedule therefore yields a flat ladder rather than
	/// throwing.
	/// </remarks>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="strategy" /> is not a declared <see cref="BackoffStrategy" />.
	/// </exception>
	public static IBackoffCalculator Create(BackoffStrategy strategy, in BackoffParameters parameters, bool useJitter) =>
		Create(strategy, parameters.BaseDelay, parameters.MaxDelay, parameters.Multiplier, useJitter);

	private static IBackoffCalculator Create(
		BackoffStrategy strategy,
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		double multiplier,
		bool useJitter)
	{
		IBackoffCalculator nominal = strategy switch
		{
			BackoffStrategy.Constant => new FixedBackoffCalculator(baseDelay),
			BackoffStrategy.Linear => new LinearBackoffCalculator(baseDelay, maxDelay),

			// Clamped rather than validated: BackoffParameters.Multiplier documents values below 1.0 as
			// clamped up, and the pure function already clamps them, so throwing here would make the two
			// public entry points disagree about the same configuration.
			BackoffStrategy.Exponential => new ExponentialBackoffCalculator(
				baseDelay,
				maxDelay,
				multiplier < 1.0 ? 1.0 : multiplier),
			_ => throw new ArgumentOutOfRangeException(
				nameof(strategy),
				strategy,
				Resources.BackoffCalculatorFactory_UnknownBackoffStrategy),
		};

		return useJitter ? new JitteredBackoffCalculator(nominal) : nominal;
	}
}
