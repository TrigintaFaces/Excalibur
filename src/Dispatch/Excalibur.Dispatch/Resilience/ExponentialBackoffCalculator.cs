// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates exponential backoff delays with optional jitter for retry operations.
/// </summary>
/// <remarks>
/// <para>
/// Exponential backoff increases the delay between retries exponentially, which helps prevent overwhelming a recovering service. Adding
/// jitter randomizes the delays to prevent the "thundering herd" problem where multiple clients retry simultaneously.
/// </para>
/// <para> The formula used is: delay = min(baseDelay * multiplier^(attempt-1) Ã‚Â± jitter, maxDelay) </para>
/// </remarks>
public sealed class ExponentialBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;
	private readonly double _multiplier;
	private readonly bool _enableJitter;
	private readonly double _jitterFactor;

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with default options.
	/// </summary>
	public ExponentialBackoffCalculator()
		: this(new RetryPolicyOptions())
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class.
	/// </summary>
	/// <param name="options"> The retry policy options containing backoff configuration. </param>
	public ExponentialBackoffCalculator(RetryPolicyOptions options)
		: this(
			options?.BaseDelay ?? TimeSpan.FromSeconds(1),
			options?.MaxDelay ?? TimeSpan.FromMinutes(30),
			options?.BackoffMultiplier ?? 2.0,
			options?.EnableJitter ?? false,
			options?.JitterFactor ?? 0.1)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ExponentialBackoffCalculator" /> class with explicit parameters.
	/// </summary>
	/// <param name="baseDelay"> The base delay for the first retry. </param>
	/// <param name="maxDelay"> The maximum delay cap. </param>
	/// <param name="multiplier"> The multiplier for exponential growth (typically 2.0). </param>
	/// <param name="enableJitter"> Whether to add random jitter to delays. </param>
	/// <param name="jitterFactor"> The jitter factor (0.0 to 1.0) for randomization. </param>
	public ExponentialBackoffCalculator(
		TimeSpan baseDelay,
		TimeSpan maxDelay,
		double multiplier = 2.0,
		bool enableJitter = true,
		double jitterFactor = 0.1)
	{
		if (baseDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(baseDelay), Resources.ExponentialBackoffCalculator_BaseDelayMustBePositive);
		}

		if (maxDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(nameof(maxDelay), Resources.ExponentialBackoffCalculator_MaxDelayMustBePositive);
		}

		if (multiplier < 1.0)
		{
			throw new ArgumentOutOfRangeException(nameof(multiplier), Resources.ExponentialBackoffCalculator_MultiplierMustBeAtLeastOne);
		}

		if (jitterFactor is < 0.0 or > 1.0)
		{
			throw new ArgumentOutOfRangeException(nameof(jitterFactor),
				Resources.ExponentialBackoffCalculator_JitterFactorMustBeBetweenZeroAndOne);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay;
		_multiplier = multiplier;
		_enableJitter = enableJitter;
		_jitterFactor = jitterFactor;
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(nameof(attempt), Resources.ExponentialBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		// Calculate exponential delay: baseDelay * multiplier^(attempt-1)
		var exponentialFactor = Math.Pow(_multiplier, attempt - 1);
		var delayMs = _baseDelay.TotalMilliseconds * exponentialFactor;

		// Apply jitter if enabled
		if (_enableJitter && _jitterFactor > 0)
		{
			delayMs = ApplyJitter(delayMs);
		}

		// Clamp to max delay
		delayMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);

		// Ensure non-negative
		delayMs = Math.Max(0, delayMs);

		return TimeSpan.FromMilliseconds(delayMs);
	}

	/// <summary>
	/// Applies decorrelated jitter using the "Full Jitter" algorithm.
	/// </summary>
	/// <remarks>
	/// Full jitter: delay = random(0, baseDelay * 2^attempt) This provides the best distribution of retry times across multiple clients.
	/// See: https://aws.amazon.com/blogs/architecture/exponential-backoff-and-jitter/
	/// </remarks>
	private double ApplyJitter(double delayMs)
	{
		// Calculate the jitter range as a percentage of the delay
		var jitterRange = delayMs * _jitterFactor;

		// Apply random jitter: delay Ã‚Â± (jitterRange * random) Using decorrelated jitter for better distribution Random.Shared is
		// thread-safe and suitable for non-cryptographic scenarios like backoff jitter
#pragma warning disable CA5394 // Do not use insecure randomness - jitter does not require cryptographic security
		var jitter = ((Random.Shared.NextDouble() * 2) - 1) * jitterRange;
#pragma warning restore CA5394

		return delayMs + jitter;
	}

	/// <summary>
	/// Creates a calculator with recommended settings for high-throughput scenarios.
	/// </summary>
	/// <returns> A calculator with short base delay and full jitter. </returns>
	public static ExponentialBackoffCalculator CreateForHighThroughput()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(10),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5);
	}

	/// <summary>
	/// Creates a calculator with recommended settings for message queue retries.
	/// </summary>
	/// <returns> A calculator suitable for message processing retries. </returns>
	public static ExponentialBackoffCalculator CreateForMessageQueue()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(5),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.25);
	}

	/// <summary>
	/// Creates a calculator with aggressive retry settings for transient failures.
	/// </summary>
	/// <returns> A calculator with short delays for transient failure scenarios. </returns>
	public static ExponentialBackoffCalculator CreateForTransientFailures()
	{
		return new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(50),
			maxDelay: TimeSpan.FromSeconds(5),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.3);
	}
}
