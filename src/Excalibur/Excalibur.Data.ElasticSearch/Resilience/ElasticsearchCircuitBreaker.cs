// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Resilience;

/// <summary>
/// Implements a circuit breaker pattern for Elasticsearch operations to prevent cascading failures.
/// </summary>
public sealed class ElasticsearchCircuitBreaker : IElasticsearchCircuitBreaker
{
	private readonly CircuitBreakerOptions _settings;
	private readonly ILogger<ElasticsearchCircuitBreaker> _logger;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private readonly Queue<DateTimeOffset> _recentRequests = new();
	private readonly Queue<DateTimeOffset> _recentFailures = new();

	private CircuitBreakerState _state = CircuitBreakerState.Closed;
	private DateTimeOffset _stateChangedAt = DateTimeOffset.UtcNow;
	private int _consecutiveFailures;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="ElasticsearchCircuitBreaker" /> class.
	/// </summary>
	/// <param name="options"> The Elasticsearch configuration options containing circuit breaker settings. </param>
	/// <param name="logger"> The logger for diagnostic information. </param>
	/// <exception cref="ArgumentNullException"> Thrown when any required parameter is null. </exception>
	public ElasticsearchCircuitBreaker(
		IOptions<ElasticsearchConfigurationOptions> options,
		ILogger<ElasticsearchCircuitBreaker> logger)
	{
		ArgumentNullException.ThrowIfNull(options);
		_settings = options.Value.Resilience.CircuitBreaker;
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
	}

	/// <inheritdoc />
	public bool IsOpen => _state == CircuitBreakerState.Open;

	/// <inheritdoc />
	public bool IsHalfOpen => _state == CircuitBreakerState.HalfOpen;

	/// <inheritdoc />
	public CircuitBreakerState State
	{
		get
		{
			lock (_lock)
			{
				UpdateStateIfNeeded();
				return _state;
			}
		}
	}

	/// <inheritdoc />
	public double FailureRate
	{
		get
		{
			lock (_lock)
			{
				CleanupOldRecords();
				return _recentRequests.Count == 0 ? 0.0 : (double)_recentFailures.Count / _recentRequests.Count;
			}
		}
	}

	/// <inheritdoc />
	public int ConsecutiveFailures
	{
		get
		{
			lock (_lock)
			{
				return _consecutiveFailures;
			}
		}
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync()
	{
		if (!_settings.Enabled)
		{
			return;
		}

		lock (_lock)
		{
			var now = DateTimeOffset.UtcNow;
			_recentRequests.Enqueue(now);
			_consecutiveFailures = 0;

			CleanupOldRecords();

			// Transition from half-open to closed on success
			if (_state == CircuitBreakerState.HalfOpen)
			{
				TransitionToState(CircuitBreakerState.Closed, "Success in half-open state");
			}
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync()
	{
		if (!_settings.Enabled)
		{
			return;
		}

		lock (_lock)
		{
			var now = DateTimeOffset.UtcNow;
			_recentRequests.Enqueue(now);
			_recentFailures.Enqueue(now);
			_consecutiveFailures++;

			CleanupOldRecords();

			// Check if we should open the circuit
			if (_state is CircuitBreakerState.Closed or CircuitBreakerState.HalfOpen && ShouldOpenCircuit())
			{
				TransitionToState(
					CircuitBreakerState.Open,
					$"Failure threshold exceeded: {_consecutiveFailures} consecutive failures, {FailureRate:P1} failure rate");
			}
		}

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ArgumentNullException.ThrowIfNull(operation);

		if (!_settings.Enabled)
		{
			return await operation().ConfigureAwait(false);
		}

		// Check current state and update if needed
		lock (_lock)
		{
			UpdateStateIfNeeded();

			if (_state == CircuitBreakerState.Open)
			{
				_logger.LogWarning("Circuit breaker is open, rejecting request");
				throw new InvalidOperationException("Circuit breaker is open - operation blocked to prevent cascading failures");
			}
		}

		try
		{
			var result = await operation().ConfigureAwait(false);
			await RecordSuccessAsync().ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Operation failed");
			await RecordFailureAsync().ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (!_disposed)
		{
			_disposed = true;
		}
	}

	/// <summary>
	/// Updates the circuit breaker state based on time-based transitions.
	/// </summary>
	private void UpdateStateIfNeeded()
	{
		var now = DateTimeOffset.UtcNow;

		// Transition from open to half-open after break duration
		if (_state == CircuitBreakerState.Open &&
			now - _stateChangedAt >= _settings.BreakDuration)
		{
			TransitionToState(CircuitBreakerState.HalfOpen, "Break duration elapsed");
		}
	}

	/// <summary>
	/// Determines whether the circuit should be opened based on current failure metrics.
	/// </summary>
	/// <returns> True if the circuit should be opened, false otherwise. </returns>
	private bool ShouldOpenCircuit()
	{
		// Check consecutive failures threshold
		if (_consecutiveFailures >= _settings.FailureThreshold)
		{
			return true;
		}

		// Check failure rate threshold (only if we have enough throughput)
		if (_recentRequests.Count >= _settings.MinimumThroughput)
		{
			var currentFailureRate = (double)_recentFailures.Count / _recentRequests.Count;
			if (currentFailureRate >= _settings.FailureRateThreshold)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Transitions the circuit breaker to a new state and logs the change.
	/// </summary>
	/// <param name="newState"> The new state to transition Excalibur.Dispatch.Transport.Aws.Sqs.LongPolling.Configuration. </param>
	/// <param name="reason"> The reason for the state change. </param>
	private void TransitionToState(CircuitBreakerState newState, string reason)
	{
		var oldState = _state;
		_state = newState;
		_stateChangedAt = DateTimeOffset.UtcNow;

		_logger.LogInformation(
			"Circuit breaker state changed from {OldState} to {NewState}: {Reason}",
			oldState, newState, reason);

		// Reset consecutive failures when closing the circuit
		if (newState == CircuitBreakerState.Closed)
		{
			_consecutiveFailures = 0;
		}
	}

	/// <summary>
	/// Removes old request and failure records that are outside the sampling window.
	/// </summary>
	private void CleanupOldRecords()
	{
		var cutoffTime = DateTimeOffset.UtcNow - _settings.SamplingDuration;

		// Remove old requests
		while (_recentRequests.Count > 0 && _recentRequests.Peek() < cutoffTime)
		{
			_ = _recentRequests.Dequeue();
		}

		// Remove old failures
		while (_recentFailures.Count > 0 && _recentFailures.Peek() < cutoffTime)
		{
			_ = _recentFailures.Dequeue();
		}
	}

	/// <summary>
	/// Throws an <see cref="ObjectDisposedException" /> if the circuit breaker has been disposed.
	/// </summary>
	/// <exception cref="ObjectDisposedException"> Thrown when the circuit breaker has been disposed. </exception>
	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(ElasticsearchCircuitBreaker));
		}
	}
}
