// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Saga.Models;

/// <summary>
/// Defines retry behavior for saga steps.
/// </summary>
public sealed class RetryPolicy
{
	/// <summary>
	/// Gets or sets the maximum number of retry attempts.
	/// </summary>
	/// <value>the maximum number of retry attempts.</value>
	public int MaxAttempts { get; set; } = 3;

	/// <summary>
	/// Gets or sets the initial delay between retries.
	/// </summary>
	/// <value>the initial delay between retries.</value>
	public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

	/// <summary>
	/// Gets or sets the maximum delay between retries.
	/// </summary>
	/// <value>the maximum delay between retries.</value>
	public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

	/// <summary>
	/// Gets or sets the backoff multiplier.
	/// </summary>
	/// <value>the backoff multiplier.</value>
	public double BackoffMultiplier { get; set; } = 2.0;

	/// <summary>
	/// Gets or sets a value indicating whether to use jitter in retry delays.
	/// </summary>
	/// <value><see langword="true"/> if to use jitter in retry delays.; otherwise, <see langword="false"/>.</value>
	public bool UseJitter { get; set; } = true;

	/// <summary>
	/// Creates a retry policy with exponential backoff.
	/// </summary>
	/// <param name="maxAttempts"> Maximum retry attempts. </param>
	/// <param name="initialDelay"> Initial delay. </param>
	/// <returns> A new retry policy. </returns>
	public static RetryPolicy ExponentialBackoff(int maxAttempts = 3, TimeSpan? initialDelay = null) =>
		new()
		{
			MaxAttempts = maxAttempts,
			InitialDelay = initialDelay ?? TimeSpan.FromSeconds(1),
			BackoffMultiplier = 2.0,
			UseJitter = true,
		};

	/// <summary>
	/// Creates a retry policy with fixed delay.
	/// </summary>
	/// <param name="maxAttempts"> Maximum retry attempts. </param>
	/// <param name="delay"> Fixed delay between retries. </param>
	/// <returns> A new retry policy. </returns>
	public static RetryPolicy FixedDelay(int maxAttempts = 3, TimeSpan? delay = null) =>
		new() { MaxAttempts = maxAttempts, InitialDelay = delay ?? TimeSpan.FromSeconds(1), BackoffMultiplier = 1.0, UseJitter = false };

	/// <summary>
	/// Calculates the delay for a given attempt.
	/// </summary>
	/// <param name="attempt"> The attempt number (1-based). </param>
	/// <returns> The calculated delay. </returns>
	public TimeSpan GetDelay(int attempt)
	{
		var delay = InitialDelay.TotalMilliseconds * Math.Pow(BackoffMultiplier, attempt - 1);
		delay = Math.Min(delay, MaxDelay.TotalMilliseconds);

		if (UseJitter)
		{
			// Jitter for retry delays doesn't require cryptographic randomness - it's for load distribution, not security
			// R0.8: Do not use insecure randomness
#pragma warning disable CA5394
			var jitter = new Random().NextDouble() * 0.2; // 0-20% jitter
#pragma warning restore CA5394
			delay *= 1 + jitter;
		}

		return TimeSpan.FromMilliseconds(delay);
	}
}

