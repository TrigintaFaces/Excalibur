// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Calculates Fibonacci-sequence backoff delays for retry operations.
/// </summary>
/// <remarks>
/// <para>
/// Fibonacci backoff uses the Fibonacci sequence to determine delays, providing a growth rate
/// between linear and exponential. The formula is: delay = baseDelay * fibonacci(attempt).
/// </para>
/// <para>
/// Fibonacci sequence: 1, 1, 2, 3, 5, 8, 13, 21, 34, 55, ...
/// This provides a gentler ramp-up than exponential backoff while still increasing over time.
/// </para>
/// </remarks>
internal sealed class FibonacciBackoffCalculator : IBackoffCalculator
{
	private readonly TimeSpan _baseDelay;
	private readonly TimeSpan _maxDelay;

	/// <summary>
	/// Initializes a new instance of the <see cref="FibonacciBackoffCalculator"/> class.
	/// </summary>
	/// <param name="baseDelay">The base delay, multiplied by the Fibonacci number for the attempt.</param>
	/// <param name="maxDelay">The maximum delay cap.</param>
	public FibonacciBackoffCalculator(TimeSpan baseDelay, TimeSpan? maxDelay = null)
	{
		if (baseDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(baseDelay),
				Resources.FibonacciBackoffCalculator_BaseDelayMustBePositive);
		}

		_baseDelay = baseDelay;
		_maxDelay = maxDelay ?? TimeSpan.FromMinutes(30);

		if (_maxDelay <= TimeSpan.Zero)
		{
			throw new ArgumentOutOfRangeException(
				nameof(maxDelay),
				Resources.FibonacciBackoffCalculator_MaxDelayMustBePositive);
		}
	}

	/// <inheritdoc />
	public TimeSpan CalculateDelay(int attempt)
	{
		if (attempt < 1)
		{
			throw new ArgumentOutOfRangeException(
				nameof(attempt),
				Resources.FibonacciBackoffCalculator_AttemptMustBeAtLeastOne);
		}

		var fib = GetFibonacci(attempt);
		var delayMs = _baseDelay.TotalMilliseconds * fib;

		// Clamp to max delay
		delayMs = Math.Min(delayMs, _maxDelay.TotalMilliseconds);

		return TimeSpan.FromMilliseconds(delayMs);
	}

	/// <summary>
	/// Computes the Fibonacci number for the given 1-based attempt.
	/// </summary>
	/// <remarks>
	/// Uses iterative computation to avoid stack overflow for large attempt values.
	/// Fibonacci(1)=1, Fibonacci(2)=1, Fibonacci(3)=2, Fibonacci(4)=3, ...
	/// </remarks>
	private static long GetFibonacci(int n)
	{
		if (n <= 2)
		{
			return 1;
		}

		long prev2 = 1;
		long prev1 = 1;

		for (var i = 3; i <= n; i++)
		{
			var current = prev1 + prev2;
			prev2 = prev1;
			prev1 = current;
		}

		return prev1;
	}
}
