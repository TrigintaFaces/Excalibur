// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Saga.Abstractions;

namespace Excalibur.Saga.Implementation;

/// <summary>
/// Default implementation of saga retry policy.
/// </summary>
public sealed class DefaultSagaRetryPolicy : ISagaRetryPolicy
{
	/// <inheritdoc />
	public int MaxAttempts { get; init; } = 3;

	/// <inheritdoc />
	public TimeSpan RetryDelay { get; init; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets the backoff multiplier for exponential backoff.
	/// </summary>
	/// <value>the backoff multiplier for exponential backoff.</value>
	public double BackoffMultiplier { get; init; } = 1;

	/// <summary>
	/// Gets a retry policy with exponential backoff.
	/// </summary>
	/// <param name="maxAttempts">The maximum number of retry attempts.</param>
	/// <param name="initialDelay">The initial delay before the first retry.</param>
	/// <returns>A saga retry policy with exponential backoff.</returns>
	public static ISagaRetryPolicy ExponentialBackoff(
		int maxAttempts = 3,
		TimeSpan? initialDelay = null) =>
		new DefaultSagaRetryPolicy
		{
			MaxAttempts = maxAttempts,
			RetryDelay = initialDelay ?? TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2,
		};

	/// <summary>
	/// Gets a retry policy with fixed delay.
	/// </summary>
	/// <param name="maxAttempts">The maximum number of retry attempts.</param>
	/// <param name="delay">The fixed delay between retries.</param>
	/// <returns>A saga retry policy with fixed delay.</returns>
	public static ISagaRetryPolicy FixedDelay(
		int maxAttempts = 3,
		TimeSpan? delay = null) =>
		new DefaultSagaRetryPolicy { MaxAttempts = maxAttempts, RetryDelay = delay ?? TimeSpan.FromSeconds(1), BackoffMultiplier = 1 };

	/// <inheritdoc />
	public bool ShouldRetry(Exception exception)
	{
		// Don't retry on cancellation
		if (exception is OperationCanceledException)
		{
			return false;
		}

		// Don't retry on argument exceptions
		if (exception is ArgumentException)
		{
			return false;
		}

		// Retry most other exceptions
		return true;
	}
}

