// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Factory for creating backoff calculators based on strategy configuration.
/// </summary>
public static class BackoffCalculatorFactory
{
	/// <summary>
	/// Creates a backoff calculator based on the specified strategy.
	/// </summary>
	/// <param name="strategy"> The backoff strategy to use. </param>
	/// <param name="options"> The retry policy options. </param>
	/// <returns> An appropriate backoff calculator for the strategy. </returns>
	public static IBackoffCalculator Create(BackoffStrategy strategy, RetryPolicyOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return strategy switch
		{
			BackoffStrategy.Fixed => new FixedBackoffCalculator(options.Backoff.BaseDelay),
			BackoffStrategy.Linear => new LinearBackoffCalculator(options.Backoff.BaseDelay, options.Backoff.MaxDelay),
			BackoffStrategy.Exponential => new ExponentialBackoffCalculator(
				options.Backoff.BaseDelay,
				options.Backoff.MaxDelay,
				options.Backoff.BackoffMultiplier,
				enableJitter: false),
			BackoffStrategy.ExponentialWithJitter => new ExponentialBackoffCalculator(
				options.Backoff.BaseDelay,
				options.Backoff.MaxDelay,
				options.Backoff.BackoffMultiplier,
				enableJitter: true,
				options.Backoff.JitterFactor),
			_ => throw new ArgumentOutOfRangeException(
				nameof(strategy),
				strategy,
				Resources.BackoffCalculatorFactory_UnknownBackoffStrategy),
		};
	}

	/// <summary>
	/// Creates a backoff calculator based on the retry strategy enum.
	/// </summary>
	/// <param name="strategy"> The retry strategy to use. </param>
	/// <param name="options"> The retry policy options. </param>
	/// <returns> An appropriate backoff calculator for the strategy. </returns>
	public static IBackoffCalculator Create(RetryStrategy strategy, RetryPolicyOptions options)
	{
		ArgumentNullException.ThrowIfNull(options);

		return strategy switch
		{
			RetryStrategy.FixedDelay => new FixedBackoffCalculator(options.Backoff.BaseDelay),
			RetryStrategy.ExponentialBackoff => options.Backoff.EnableJitter
				? new ExponentialBackoffCalculator(
					options.Backoff.BaseDelay,
					options.Backoff.MaxDelay,
					options.Backoff.BackoffMultiplier,
					enableJitter: true,
					options.Backoff.JitterFactor)
				: new ExponentialBackoffCalculator(
					options.Backoff.BaseDelay,
					options.Backoff.MaxDelay,
					options.Backoff.BackoffMultiplier,
					enableJitter: false),
			_ => throw new ArgumentOutOfRangeException(
				nameof(strategy),
				strategy,
				Resources.BackoffCalculatorFactory_UnknownRetryStrategy),
		};
	}
}
