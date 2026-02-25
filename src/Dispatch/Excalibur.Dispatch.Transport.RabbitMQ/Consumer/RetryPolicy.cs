// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.RabbitMQ;

/// <summary>
/// Defines retry policy options for RabbitMQ message processing.
/// </summary>
public sealed class RetryPolicy
{
	private RetryPolicy()
	{
	}

	/// <summary>
	/// Gets the maximum number of retry attempts.
	/// </summary>
	/// <value>The maximum number of retry attempts.</value>
	public int MaxRetries { get; private init; }

	/// <summary>
	/// Gets the initial delay before the first retry.
	/// </summary>
	/// <value>The initial delay before the first retry.</value>
	public TimeSpan InitialDelay { get; private init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the maximum delay between retries.
	/// </summary>
	/// <value>The maximum delay between retries.</value>
	public TimeSpan MaxDelay { get; private init; } = TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets the multiplier for exponential backoff.
	/// </summary>
	/// <value>The multiplier for exponential backoff.</value>
	public double BackoffMultiplier { get; private init; } = 2.0;

	/// <summary>
	/// Gets a value indicating whether exponential backoff is enabled.
	/// </summary>
	/// <value><see langword="true"/> if exponential backoff is enabled; otherwise, <see langword="false"/>.</value>
	public bool UseExponentialBackoff { get; private init; }

	/// <summary>
	/// Creates a retry policy with no retries.
	/// </summary>
	/// <returns>A retry policy that does not retry failed messages.</returns>
	public static RetryPolicy None() => new()
	{
		MaxRetries = 0,
		UseExponentialBackoff = false,
	};

	/// <summary>
	/// Creates a retry policy with a fixed number of retries and constant delay.
	/// </summary>
	/// <param name="maxRetries">The maximum number of retry attempts.</param>
	/// <param name="delay">The delay between retry attempts. Defaults to 1 second.</param>
	/// <returns>A retry policy with fixed delay.</returns>
	public static RetryPolicy Fixed(int maxRetries, TimeSpan? delay = null) => new()
	{
		MaxRetries = maxRetries,
		InitialDelay = delay ?? TimeSpan.FromSeconds(1),
		MaxDelay = delay ?? TimeSpan.FromSeconds(1),
		UseExponentialBackoff = false,
	};

	/// <summary>
	/// Creates a retry policy with exponential backoff.
	/// </summary>
	/// <param name="maxRetries">The maximum number of retry attempts.</param>
	/// <param name="initialDelay">The initial delay before the first retry. Defaults to 1 second.</param>
	/// <param name="maxDelay">The maximum delay between retries. Defaults to 5 minutes.</param>
	/// <param name="multiplier">The backoff multiplier. Defaults to 2.0.</param>
	/// <returns>A retry policy with exponential backoff.</returns>
	public static RetryPolicy Exponential(
		int maxRetries,
		TimeSpan? initialDelay = null,
		TimeSpan? maxDelay = null,
		double multiplier = 2.0) => new()
		{
			MaxRetries = maxRetries,
			InitialDelay = initialDelay ?? TimeSpan.FromSeconds(1),
			MaxDelay = maxDelay ?? TimeSpan.FromMinutes(5),
			BackoffMultiplier = multiplier,
			UseExponentialBackoff = true,
		};

	/// <summary>
	/// Calculates the delay for a given retry attempt.
	/// </summary>
	/// <param name="attemptNumber">The current attempt number (1-based).</param>
	/// <returns>The delay before the next retry attempt.</returns>
	public TimeSpan CalculateDelay(int attemptNumber)
	{
		if (attemptNumber <= 0)
		{
			return TimeSpan.Zero;
		}

		if (!UseExponentialBackoff)
		{
			return InitialDelay;
		}

		var exponentialDelay = TimeSpan.FromTicks(
			(long)(InitialDelay.Ticks * Math.Pow(BackoffMultiplier, attemptNumber - 1)));

		return exponentialDelay > MaxDelay ? MaxDelay : exponentialDelay;
	}
}
