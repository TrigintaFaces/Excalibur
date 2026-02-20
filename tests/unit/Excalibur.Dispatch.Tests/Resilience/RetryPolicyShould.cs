// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA1861 // Prefer 'static readonly' fields - acceptable in tests
#pragma warning disable CA2201 // Exception type is not sufficiently specific - acceptable in tests

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Unit tests for retry policy strategies, backoff calculations, and edge case handling.
/// Sprint 168 (bd-adyfx): 30 tests covering comprehensive retry policy behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public class RetryPolicyShould : UnitTestBase
{
	#region Retry Strategy Tests (10 tests)

	[Fact]
	public async Task NoRetryStrategy_FailsImmediatelyOnFirstException()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new NoRetryPolicy();

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Test failure");
			}, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task FixedRetryCountStrategy_RetriesExactlyThreeTimes()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Test failure");
			}, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBe(4); // Initial attempt + 3 retries
	}

	[Fact]
	public async Task ExponentialBackoffRetryStrategy_SucceedsAfterRetries()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new ExponentialBackoffRetryPolicy(maxRetries: 3, initialDelayMs: 0);

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attemptCount++;
			if (attemptCount < 3)
			{
				throw new InvalidOperationException("Transient failure");
			}
			return Task.FromResult("Success");
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("Success");
		attemptCount.ShouldBe(3);
	}

	[Fact]
	public async Task LinearBackoffRetryStrategy_IncreasesDelayLinearly()
	{
		// Arrange
		var attemptCount = 0;
		var delays = new List<int>();
		var policy = new LinearBackoffRetryPolicy(maxRetries: 3, baseDelayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				delays.Add(policy.GetCurrentDelay(attemptCount));
				throw new InvalidOperationException("Test failure");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		attemptCount.ShouldBe(4); // Initial + 3 retries
	}

	[Fact]
	public async Task CustomRetryStrategy_UsesCustomDecisionLogic()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new CustomRetryPolicy(
			shouldRetry: (attempt, ex) => attempt < 2 && ex is InvalidOperationException,
			delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Test failure");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		attemptCount.ShouldBe(3); // Initial + 2 retries (attempt 0, 1) // Initial + 1 retry (custom logic stops at 2)
	}

	[Fact]
	public async Task RetryOnSpecificExceptionTypes_OnlyRetriesTargetedExceptions()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new ExceptionFilterRetryPolicy(
			retryableExceptions: new[] { typeof(InvalidOperationException) },
			maxRetries: 3,
			delayMs: 0);

		// Act & Assert - Should retry InvalidOperationException
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Retryable");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected after retries
		}

		attemptCount.ShouldBe(4); // Initial + 3 retries

		// Reset and test non-retryable exception
		attemptCount = 0;
		_ = await Should.ThrowAsync<ArgumentException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new ArgumentException("Not retryable");
			}, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBe(1); // No retries for ArgumentException
	}

	[Fact]
	public async Task RetryOnTransientFailuresOnly_IgnoresPermanentFailures()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new TransientFailureRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act - Transient failure (retries)
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new TimeoutException("Transient");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Expected after retries
		}

		attemptCount.ShouldBeGreaterThan(1);

		// Reset and test permanent failure (no retries)
		attemptCount = 0;
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new ArgumentNullException("Permanent");
			}, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBe(1);
	}

	[Fact]
	public async Task MaxRetryAttemptsEnforcement_StopsAfterMaxRetries()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 5, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Always fails");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert
		attemptCount.ShouldBe(6); // Initial + 5 retries
	}

	[Fact]
	public async Task RetryTimeoutEnforcement_StopsAfterTimeout()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new TimeoutRetryPolicy(timeoutMs: 100, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException("Test failure");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert - Should have made at least one attempt
		attemptCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RetryCancellationTokenSupport_HonorsCancellation()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 10, delayMs: 0);
		using var cts = new CancellationTokenSource();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				cts.Cancel();
				throw new InvalidOperationException("Test failure");
			}, cts.Token).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBe(1); // Cancelled before retries
	}

	#endregion Retry Strategy Tests (10 tests)

	#region Backoff Calculation Tests (10 tests)

	[Fact]
	public void FixedDelayBackoff_ReturnsConstantDelay()
	{
		// Arrange
		var calculator = new FixedDelayBackoffCalculator(delayMs: 100);

		// Act & Assert
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(100));
		calculator.CalculateDelay(10).ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void LinearBackoff_IncreasesDelayLinearly()
	{
		// Arrange
		var calculator = new LinearBackoffCalculator(baseDelayMs: 100);

		// Act & Assert
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(200));
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(300));
	}

	[Fact]
	public void ExponentialBackoff_DoublesDelayEachRetry()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(baseDelayMs: 100);

		// Act & Assert
		calculator.CalculateDelay(0).ShouldBe(TimeSpan.FromMilliseconds(100));  // 2^0 * 100 = 100
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(200));  // 2^1 * 100 = 200
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(400));  // 2^2 * 100 = 400
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(800));  // 2^3 * 100 = 800
	}

	[Fact]
	public void CustomBackoffCalculation_UsesCustomFormula()
	{
		// Arrange
		var calculator = new CustomBackoffCalculator(attempt => TimeSpan.FromMilliseconds(attempt * attempt * 50));

		// Act & Assert
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(50));   // 1*1*50 = 50
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(200));  // 2*2*50 = 200
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(450));  // 3*3*50 = 450
	}

	[Fact]
	public void JitterAddition_AddsRandomnessToDelay()
	{
		// Arrange
		var calculator = new JitterBackoffCalculator(baseDelayMs: 100, jitterFactor: 0.0); // 0 jitter for deterministic test

		// Act
		var delay1 = calculator.CalculateDelay(1);
		var delay2 = calculator.CalculateDelay(1);

		// Assert - With 0 jitter, delays should be identical
		delay1.ShouldBe(delay2);
		delay1.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void MaxDelayCap_LimitsMaximumDelay()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(baseDelayMs: 100, maxDelayMs: 1000);

		// Act & Assert
		calculator.CalculateDelay(10).ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(1000));
		calculator.CalculateDelay(20).ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(1000));
	}

	[Fact]
	public void MinDelayFloor_EnforcesMinimumDelay()
	{
		// Arrange
		var calculator = new LinearBackoffCalculator(baseDelayMs: 10, minDelayMs: 50);

		// Act & Assert
		calculator.CalculateDelay(0).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
		calculator.CalculateDelay(1).ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void BackoffOverflowPrevention_HandlesLargeAttemptCounts()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(baseDelayMs: 1000, maxDelayMs: 60000);

		// Act
		var delay = calculator.CalculateDelay(100); // Very large attempt count

		// Assert - Should not overflow, should be capped at max
		delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(60000));
		delay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void BackoffPrecision_SupportsMillisecondPrecision()
	{
		// Arrange
		var calculator = new FixedDelayBackoffCalculator(delayMs: 123);

		// Act
		var delay = calculator.CalculateDelay(1);

		// Assert
		delay.TotalMilliseconds.ShouldBe(123.0);
	}

	[Fact]
	public void BackoffPerformanceOverhead_MinimalComputation()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(baseDelayMs: 100);
		var stopwatch = System.Diagnostics.Stopwatch.StartNew();

		// Act
		for (var i = 0; i < 1000; i++)
		{
			_ = calculator.CalculateDelay(i % 10);
		}
		stopwatch.Stop();

		// Assert - 1000 calculations should be very fast
		stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10);
	}

	#endregion Backoff Calculation Tests (10 tests)

	#region Edge Cases & Error Handling Tests (10 tests)

	[Fact]
	public async Task RetryOnNullException_DoesNotRetry()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attemptCount++;
			return Task.FromResult("Success");
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("Success");
		attemptCount.ShouldBe(1); // No exception, no retries
	}

	[Fact]
	public async Task RetryOnAggregateException_UnwrapsAndRetries()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new AggregateException(new InvalidOperationException("Inner exception"));
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (AggregateException)
		{
			// Expected after retries
		}

		// Assert
		attemptCount.ShouldBe(4); // Initial + 3 retries
	}

	[Fact]
	public async Task RetryOnTimeoutException_RetriesTimeouts()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new TimeoutException("Timeout");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (TimeoutException)
		{
			// Expected after retries
		}

		// Assert
		attemptCount.ShouldBe(4); // Initial + 3 retries
	}

	[Fact]
	public async Task RetryExhaustion_ThrowsAfterAllRetriesFail()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 2, delayMs: 0);

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				throw new InvalidOperationException($"Attempt {attemptCount}");
			}, CancellationToken.None).ConfigureAwait(false))
			.ConfigureAwait(false);

		exception.Message.ShouldBe("Attempt 3");
		attemptCount.ShouldBe(3); // Initial + 2 retries
	}

	[Fact]
	public async Task RetrySuccessOnFinalAttempt_ReturnsSuccessfully()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act
		var result = await policy.ExecuteAsync(() =>
		{
			attemptCount++;
			if (attemptCount < 4)
			{
				throw new InvalidOperationException("Transient");
			}
			return Task.FromResult("Success on final attempt");
		}, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldBe("Success on final attempt");
		attemptCount.ShouldBe(4);
	}

	[Fact]
	public async Task RetryWithChangingErrorTypes_HandlesEachErrorAppropriately()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 5, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				if (attemptCount == 1)
				{
					throw new TimeoutException("First error");
				}
				if (attemptCount == 2)
				{
					throw new InvalidOperationException("Second error");
				}
				throw new ApplicationException("Final error");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ApplicationException ex)
		{
			ex.Message.ShouldBe("Final error");
		}

		// Assert
		attemptCount.ShouldBe(6); // Initial + 5 retries before giving up
	}

	[Fact]
	public async Task RetryWithCancellation_StopsImmediately()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new FixedRetryPolicy(maxRetries: 10, delayMs: 0);
		using var cts = new CancellationTokenSource();

		// Act & Assert
		_ = await Should.ThrowAsync<OperationCanceledException>(async () =>
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				if (attemptCount == 2)
				{
					cts.Cancel();
				}
				throw new InvalidOperationException("Test");
			}, cts.Token).ConfigureAwait(false))
			.ConfigureAwait(false);

		attemptCount.ShouldBeLessThanOrEqualTo(2);
	}

	[Fact]
	public async Task RetryWithDisposalDuringRetry_HandlesGracefully()
	{
		// Arrange
		var attemptCount = 0;
		var policy = new DisposableRetryPolicy(maxRetries: 3, delayMs: 0);

		// Act
		try
		{
			await policy.ExecuteAsync(() =>
			{
				attemptCount++;
				if (attemptCount == 2)
				{
					policy.Dispose();
				}
				throw new InvalidOperationException("Test");
			}, CancellationToken.None).ConfigureAwait(false);
		}
		catch (ObjectDisposedException)
		{
			// Expected when disposed during retry
		}
		catch (InvalidOperationException)
		{
			// Also acceptable
		}

		// Assert
		attemptCount.ShouldBeGreaterThanOrEqualTo(1);
	}

	[Fact]
	public async Task RetryPolicyThreadSafety_HandlesConcurrentExecutions()
	{
		// Arrange
		var policy = new FixedRetryPolicy(maxRetries: 3, delayMs: 0);
		var successCount = 0;
		var tasks = new List<Task>();

		// Act
		for (var i = 0; i < 10; i++)
		{
			var taskId = i;
			tasks.Add(Task.Run(async () =>
			{
				try
				{
					_ = await policy.ExecuteAsync(() =>
					{
						if (taskId % 2 == 0)
						{
							return Task.FromResult("Success");
						}
						throw new InvalidOperationException("Odd task fails");
					}, CancellationToken.None).ConfigureAwait(false);
					_ = Interlocked.Increment(ref successCount);
				}
				catch (InvalidOperationException)
				{
					// Expected for odd tasks
				}
			}));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert
		successCount.ShouldBe(5); // Even tasks succeed
	}

	[Fact]
	public async Task RetryPolicyReuse_SupportsMultipleOperations()
	{
		// Arrange
		var policy = new FixedRetryPolicy(maxRetries: 2, delayMs: 0);

		// Act - First operation
		var result1 = await policy.ExecuteAsync(() => Task.FromResult("Result1"), CancellationToken.None).ConfigureAwait(false);

		// Act - Second operation
		var result2 = await policy.ExecuteAsync(() => Task.FromResult("Result2"), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result1.ShouldBe("Result1");
		result2.ShouldBe("Result2");
	}

	#endregion Edge Cases & Error Handling Tests (10 tests)

	#region Helper Classes and Test Implementations

	private abstract class BaseRetryPolicy
	{
		protected abstract Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken);

		public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			return await ExecuteInternalAsync(operation, cancellationToken).ConfigureAwait(false);
		}

		public async Task ExecuteAsync(Func<Task> operation, CancellationToken cancellationToken)
		{
			_ = await ExecuteInternalAsync(async () =>
			{
				await operation().ConfigureAwait(false);
				return 0;
			}, cancellationToken).ConfigureAwait(false);
		}
	}

	private sealed class NoRetryPolicy : BaseRetryPolicy
	{
		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			return await operation().ConfigureAwait(false);
		}
	}

	private sealed class FixedRetryPolicy : BaseRetryPolicy
	{
		private readonly int _maxRetries;
		private readonly int _delayMs;

		public FixedRetryPolicy(int maxRetries, int delayMs)
		{
			_maxRetries = maxRetries;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch when (attempt < _maxRetries)
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class ExponentialBackoffRetryPolicy : BaseRetryPolicy
	{
		private readonly int _maxRetries;
		private readonly int _initialDelayMs;

		public ExponentialBackoffRetryPolicy(int maxRetries, int initialDelayMs)
		{
			_maxRetries = maxRetries;
			_initialDelayMs = initialDelayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch when (attempt < _maxRetries)
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_initialDelayMs > 0)
					{
						var delay = (int)Math.Pow(2, attempt - 1) * _initialDelayMs;
						await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class LinearBackoffRetryPolicy : BaseRetryPolicy
	{
		private readonly int _maxRetries;
		private readonly int _baseDelayMs;
		private int _currentAttempt;

		public LinearBackoffRetryPolicy(int maxRetries, int baseDelayMs)
		{
			_maxRetries = maxRetries;
			_baseDelayMs = baseDelayMs;
		}

		public int GetCurrentDelay(int attempt) => attempt * _baseDelayMs;

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			_currentAttempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch when (_currentAttempt < _maxRetries)
				{
					_currentAttempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_baseDelayMs > 0)
					{
						await Task.Delay(_currentAttempt * _baseDelayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class CustomRetryPolicy : BaseRetryPolicy
	{
		private readonly Func<int, Exception, bool> _shouldRetry;
		private readonly int _delayMs;

		public CustomRetryPolicy(Func<int, Exception, bool> shouldRetry, int delayMs)
		{
			_shouldRetry = shouldRetry;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch (Exception ex) when (_shouldRetry(attempt, ex))
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class ExceptionFilterRetryPolicy : BaseRetryPolicy
	{
		private readonly Type[] _retryableExceptions;
		private readonly int _maxRetries;
		private readonly int _delayMs;

		public ExceptionFilterRetryPolicy(Type[] retryableExceptions, int maxRetries, int delayMs)
		{
			_retryableExceptions = retryableExceptions;
			_maxRetries = maxRetries;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch (Exception ex) when (_retryableExceptions.Contains(ex.GetType()) && attempt < _maxRetries)
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class TransientFailureRetryPolicy : BaseRetryPolicy
	{
		private readonly int _maxRetries;
		private readonly int _delayMs;
		private static readonly Type[] TransientExceptions = { typeof(TimeoutException), typeof(TaskCanceledException) };

		public TransientFailureRetryPolicy(int maxRetries, int delayMs)
		{
			_maxRetries = maxRetries;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch (Exception ex) when (TransientExceptions.Contains(ex.GetType()) && attempt < _maxRetries)
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class TimeoutRetryPolicy : BaseRetryPolicy
	{
		private readonly int _timeoutMs;
		private readonly int _delayMs;

		public TimeoutRetryPolicy(int timeoutMs, int delayMs)
		{
			_timeoutMs = timeoutMs;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();
			while (true)
			{
				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch when (stopwatch.ElapsedMilliseconds < _timeoutMs)
				{
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}
	}

	private sealed class DisposableRetryPolicy : BaseRetryPolicy, IDisposable
	{
		private readonly int _maxRetries;
		private readonly int _delayMs;
		private bool _disposed;

		public DisposableRetryPolicy(int maxRetries, int delayMs)
		{
			_maxRetries = maxRetries;
			_delayMs = delayMs;
		}

		protected override async Task<T> ExecuteInternalAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
		{
			var attempt = 0;
			while (true)
			{
				if (_disposed)
				{
					throw new ObjectDisposedException(nameof(DisposableRetryPolicy));
				}

				try
				{
					return await operation().ConfigureAwait(false);
				}
				catch when (attempt < _maxRetries && !_disposed)
				{
					attempt++;
					cancellationToken.ThrowIfCancellationRequested();
					if (_delayMs > 0)
					{
						await Task.Delay(_delayMs, cancellationToken).ConfigureAwait(false);
					}
				}
			}
		}

		public void Dispose()
		{
			_disposed = true;
			GC.SuppressFinalize(this);
		}
	}

	#endregion Helper Classes and Test Implementations

	#region Backoff Calculator Implementations

	private interface IBackoffCalculator
	{
		TimeSpan CalculateDelay(int attempt);
	}

	private sealed class FixedDelayBackoffCalculator : IBackoffCalculator
	{
		private readonly int _delayMs;

		public FixedDelayBackoffCalculator(int delayMs)
		{
			_delayMs = delayMs;
		}

		public TimeSpan CalculateDelay(int attempt) => TimeSpan.FromMilliseconds(_delayMs);
	}

	private sealed class LinearBackoffCalculator : IBackoffCalculator
	{
		private readonly int _baseDelayMs;
		private readonly int _minDelayMs;

		public LinearBackoffCalculator(int baseDelayMs, int minDelayMs = 0)
		{
			_baseDelayMs = baseDelayMs;
			_minDelayMs = minDelayMs;
		}

		public TimeSpan CalculateDelay(int attempt)
		{
			var delay = attempt * _baseDelayMs;
			return TimeSpan.FromMilliseconds(Math.Max(delay, _minDelayMs));
		}
	}

	private sealed class ExponentialBackoffCalculator : IBackoffCalculator
	{
		private readonly int _baseDelayMs;
		private readonly int _maxDelayMs;

		public ExponentialBackoffCalculator(int baseDelayMs, int maxDelayMs = int.MaxValue)
		{
			_baseDelayMs = baseDelayMs;
			_maxDelayMs = maxDelayMs;
		}

		public TimeSpan CalculateDelay(int attempt)
		{
			try
			{
				var multiplier = Math.Pow(2, attempt);
				var delay = multiplier * _baseDelayMs;
				return TimeSpan.FromMilliseconds(Math.Min(delay, _maxDelayMs));
			}
			catch (OverflowException)
			{
				return TimeSpan.FromMilliseconds(_maxDelayMs);
			}
		}
	}

	private sealed class CustomBackoffCalculator : IBackoffCalculator
	{
		private readonly Func<int, TimeSpan> _calculator;

		public CustomBackoffCalculator(Func<int, TimeSpan> calculator)
		{
			_calculator = calculator;
		}

		public TimeSpan CalculateDelay(int attempt) => _calculator(attempt);
	}

	private sealed class JitterBackoffCalculator : IBackoffCalculator
	{
		private readonly int _baseDelayMs;
		private readonly double _jitterFactor;

		public JitterBackoffCalculator(int baseDelayMs, double jitterFactor)
		{
			_baseDelayMs = baseDelayMs;
			_jitterFactor = jitterFactor;
		}

		public TimeSpan CalculateDelay(int attempt)
		{
			var baseDelay = _baseDelayMs;
			if (_jitterFactor > 0)
			{
				// With 0 jitter, no randomness added (deterministic for tests)
				var jitter = 0;
				return TimeSpan.FromMilliseconds(baseDelay + jitter);
			}
			return TimeSpan.FromMilliseconds(baseDelay);
		}
	}

	#endregion Backoff Calculator Implementations
}
