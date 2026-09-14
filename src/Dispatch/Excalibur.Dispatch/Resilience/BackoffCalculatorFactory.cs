// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Factory for creating backoff calculators based on strategy configuration.
/// </summary>
internal static class BackoffCalculatorFactory
{
	/// <summary>
	/// Creates a backoff calculator based on the specified strategy.
	/// </summary>
	/// <param name="strategy"> The backoff strategy to use. </param>
	/// <param name="options"> The retry options. </param>
	/// <returns> An appropriate backoff calculator for the strategy. </returns>
	public static IBackoffCalculator Create(BackoffStrategy strategy, RetryOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return strategy switch
		{
			BackoffStrategy.Fixed => new FixedBackoffCalculator(options.BaseDelay),
			BackoffStrategy.Linear => new LinearBackoffCalculator(options.BaseDelay, options.MaxDelay),
			BackoffStrategy.Exponential => new ExponentialBackoffCalculator(
				options.BaseDelay,
				options.MaxDelay,
				options.BackoffMultiplier,
				enableJitter: false),
			BackoffStrategy.ExponentialWithJitter => new ExponentialBackoffCalculator(
				options.BaseDelay,
				options.MaxDelay,
				options.BackoffMultiplier,
				enableJitter: true,
				options.JitterFactor),
			BackoffStrategy.Fibonacci => options.UseJitter
				? new JitteredBackoffCalculator(
					new FibonacciBackoffCalculator(options.BaseDelay, options.MaxDelay),
					options.JitterFactor)
				: new FibonacciBackoffCalculator(options.BaseDelay, options.MaxDelay),
			BackoffStrategy.FullJitter => new FullJitterBackoffCalculator(
				options.BaseDelay,
				options.MaxDelay,
				options.BackoffMultiplier),
			BackoffStrategy.DecorrelatedJitter => new DecorrelatedJitterBackoffCalculator(
				options.BaseDelay,
				options.MaxDelay),
			_ => throw new ArgumentOutOfRangeException(
				nameof(strategy),
				strategy,
				Resources.BackoffCalculatorFactory_UnknownBackoffStrategy),
		};
	}
}
