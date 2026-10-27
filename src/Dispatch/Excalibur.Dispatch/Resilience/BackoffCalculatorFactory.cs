// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Builds the <see cref="IBackoffCalculator" /> a set of retry options describes.
/// </summary>
/// <remarks>
/// <para>
/// <b>Growth law, then jitter, in that order.</b> The strategy selects one of three growth laws and
/// <see cref="RetryOptions.UseJitter" /> decides whether the result is randomised. Previously jitter was
/// decided per strategy arm: the exponential arm hardcoded it OFF, one arm consulted the option, and two
/// strategy names implied it. The out-of-box configuration therefore requested jitter and received none,
/// silently, because the default strategy's arm had already answered the question with a literal. Reading
/// the option once, here, outside the switch, is what makes that state unreachable rather than fixed.
/// </para>
/// </remarks>
internal static class BackoffCalculatorFactory
{
	/// <summary>
	/// Creates the calculator for the given strategy and options.
	/// </summary>
	/// <param name="strategy">The growth law to apply.</param>
	/// <param name="options">The retry options supplying the delays, multiplier and jitter switch.</param>
	/// <returns>The calculator.</returns>
	/// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
	/// <exception cref="ArgumentOutOfRangeException">
	/// <paramref name="strategy" /> is not a declared <see cref="BackoffStrategy" />. An undeclared value
	/// is refused rather than defaulted: a cast from an unmapped integer is a configuration error, and
	/// substituting a curve for it is how a consumer silently gets behaviour they did not select.
	/// </exception>
	public static IBackoffCalculator Create(BackoffStrategy strategy, RetryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		IBackoffCalculator nominal = strategy switch
		{
			BackoffStrategy.Constant => new FixedBackoffCalculator(options.BaseDelay),
			BackoffStrategy.Linear => new LinearBackoffCalculator(options.BaseDelay, options.MaxDelay),
			BackoffStrategy.Exponential => new ExponentialBackoffCalculator(
				options.BaseDelay,
				options.MaxDelay,
				options.BackoffMultiplier),
			_ => throw new ArgumentOutOfRangeException(
				nameof(strategy),
				strategy,
				Resources.BackoffCalculatorFactory_UnknownBackoffStrategy),
		};

		return options.UseJitter ? new JitteredBackoffCalculator(nominal) : nominal;
	}
}
