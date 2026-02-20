// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for resilience patterns (circuit breaker, retry, bulkhead).
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class ResilienceBenchmarks
{
	private TestCircuitBreaker _closedCircuitBreaker = null!;
	private TestCircuitBreaker _openCircuitBreaker = null!;
	private TestBulkhead _bulkhead = null!;
	private Random _random = null!;

	[GlobalSetup]
	public void Setup()
	{
		_closedCircuitBreaker = new TestCircuitBreaker(failureThreshold: 5);
		_openCircuitBreaker = new TestCircuitBreaker(failureThreshold: 5);

		// Force open state
		for (var i = 0; i < 6; i++)
		{
			_openCircuitBreaker.RecordFailure();
		}

		_bulkhead = new TestBulkhead(maxConcurrent: 10);
		_random = new Random(42);
	}

	[Benchmark(Baseline = true)]
	public bool CircuitBreaker_CheckState_Closed()
	{
		return _closedCircuitBreaker.CanExecute();
	}

	[Benchmark]
	public bool CircuitBreaker_CheckState_Open()
	{
		return _openCircuitBreaker.CanExecute();
	}

	[Benchmark]
	public void CircuitBreaker_RecordSuccess()
	{
		_closedCircuitBreaker.RecordSuccess();
	}

	[Benchmark]
	public void CircuitBreaker_RecordFailure()
	{
		var cb = new TestCircuitBreaker(failureThreshold: 100);
		cb.RecordFailure();
	}

	[Benchmark]
	public TimeSpan CalculateExponentialBackoff_Attempt1()
	{
		return CalculateExponentialBackoff(1, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));
	}

	[Benchmark]
	public TimeSpan CalculateExponentialBackoff_Attempt5()
	{
		return CalculateExponentialBackoff(5, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));
	}

	[Benchmark]
	public TimeSpan CalculateExponentialBackoff_Attempt10()
	{
		return CalculateExponentialBackoff(10, TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(30));
	}

	[Benchmark]
	public TimeSpan CalculateBackoffWithJitter()
	{
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var jitter = (_random.NextDouble() * 2 - 1) * 0.2;
		return TimeSpan.FromMilliseconds(baseDelay.TotalMilliseconds * (1 + jitter));
	}

	[Benchmark]
	public bool Bulkhead_TryAcquire()
	{
		var result = _bulkhead.TryAcquire();
		if (result)
		{
			_bulkhead.Release();
		}

		return result;
	}

	[Benchmark]
	public int Bulkhead_GetAvailableSlots()
	{
		return _bulkhead.AvailableSlots;
	}

	[Benchmark]
	public bool ShouldRetry_TransientException()
	{
		return IsTransientException(new TimeoutException());
	}

	[Benchmark]
	public bool ShouldRetry_NonTransientException()
	{
		return IsTransientException(new ArgumentException());
	}

	[Benchmark]
	public RetryContext CreateRetryContext()
	{
		return new RetryContext
		{
			Attempt = 1,
			MaxAttempts = 3,
			StartedAt = DateTimeOffset.UtcNow,
			LastException = null,
		};
	}

	[Benchmark]
	public CircuitBreakerState CreateCircuitBreakerState()
	{
		return new CircuitBreakerState
		{
			State = CircuitState.Closed,
			FailureCount = 0,
			SuccessCount = 0,
			LastFailureTime = null,
			LastSuccessTime = null,
		};
	}

	private static TimeSpan CalculateExponentialBackoff(int attempt, TimeSpan baseDelay, TimeSpan maxDelay)
	{
		var delayMs = baseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
		return TimeSpan.FromMilliseconds(Math.Min(delayMs, maxDelay.TotalMilliseconds));
	}

	private static bool IsTransientException(Exception ex)
	{
		return ex is TimeoutException or HttpRequestException or TaskCanceledException;
	}

	private sealed class TestCircuitBreaker(int failureThreshold)
	{
		private int _failureCount;
		private int _successCount;
		private CircuitState _state = CircuitState.Closed;

		public bool CanExecute() => _state != CircuitState.Open;

		public void RecordSuccess()
		{
			_ = Interlocked.Increment(ref _successCount);
			if (_state == CircuitState.HalfOpen)
			{
				_state = CircuitState.Closed;
				_ = Interlocked.Exchange(ref _failureCount, 0);
			}
		}

		public void RecordFailure()
		{
			var failures = Interlocked.Increment(ref _failureCount);
			if (failures >= failureThreshold)
			{
				_state = CircuitState.Open;
			}
		}
	}

	private sealed class TestBulkhead(int maxConcurrent)
	{
		private int _currentCount;

		public int AvailableSlots => maxConcurrent - _currentCount;

		public bool TryAcquire()
		{
			var current = Interlocked.Increment(ref _currentCount);
			if (current > maxConcurrent)
			{
				_ = Interlocked.Decrement(ref _currentCount);
				return false;
			}

			return true;
		}

		public void Release()
		{
			_ = Interlocked.Decrement(ref _currentCount);
		}
	}

	public enum CircuitState
	{
		Closed,
		Open,
		HalfOpen,
	}

	public sealed class RetryContext
	{
		public int Attempt { get; init; }
		public int MaxAttempts { get; init; }
		public DateTimeOffset StartedAt { get; init; }
		public Exception? LastException { get; init; }
	}

	public sealed class CircuitBreakerState
	{
		public CircuitState State { get; init; }
		public int FailureCount { get; init; }
		public int SuccessCount { get; init; }
		public DateTimeOffset? LastFailureTime { get; init; }
		public DateTimeOffset? LastSuccessTime { get; init; }
	}
}
