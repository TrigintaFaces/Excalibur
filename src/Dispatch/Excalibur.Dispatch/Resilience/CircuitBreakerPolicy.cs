// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Implementation of the circuit breaker pattern for protecting operations from repeated failures.
/// </summary>
public sealed partial class CircuitBreakerPolicy : ICircuitBreakerPolicy, ICircuitBreakerDiagnostics, ICircuitBreakerEvents
{
	private readonly CircuitBreakerOptions _options;
	private readonly ILogger<CircuitBreakerPolicy>? _logger;
	private readonly string _name;
	private readonly Func<Exception, bool>? _shouldHandle;
#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	private CircuitState _state = CircuitState.Closed;
	private int _consecutiveFailures;
	private int _successfulProbes;
	private DateTimeOffset? _lastOpenedAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerPolicy"/> class.
	/// </summary>
	/// <param name="options">The circuit breaker configuration options.</param>
	/// <param name="name">The name of the circuit breaker (e.g., transport name).</param>
	/// <param name="logger">Optional logger instance.</param>
	/// <param name="shouldHandle">Optional predicate to determine which exceptions should trip the circuit.</param>
	public CircuitBreakerPolicy(
		CircuitBreakerOptions options,
		string name = "default",
		ILogger<CircuitBreakerPolicy>? logger = null,
		Func<Exception, bool>? shouldHandle = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_logger = logger;
		_shouldHandle = shouldHandle;
	}

	/// <inheritdoc />
	public CircuitState State
	{
		get
		{
			lock (_lock)
			{
				// Check if we should transition from Open to HalfOpen
				if (_state == CircuitState.Open && ShouldAttemptReset())
				{
					TransitionTo(CircuitState.HalfOpen);
				}

				return _state;
			}
		}
	}

	/// <summary>
	/// Gets the number of consecutive failures since the last success.
	/// </summary>
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

	/// <summary>
	/// Gets the timestamp when the circuit was last opened.
	/// </summary>
	public DateTimeOffset? LastOpenedAt
	{
		get
		{
			lock (_lock)
			{
				return _lastOpenedAt;
			}
		}
	}

	/// <summary>
	/// Event raised when the circuit state changes.
	/// </summary>
	public event EventHandler<CircuitStateChangedEventArgs>? StateChanged;

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		CancellationToken cancellationToken)
	{
		EnsureCircuitAllowsExecution();

		try
		{
			var result = await action(cancellationToken).ConfigureAwait(false);
			RecordSuccess();
			return result;
		}
		catch (Exception ex) when (ShouldHandleException(ex))
		{
			RecordFailure(ex);
			throw;
		}
	}

	/// <inheritdoc />
	public void RecordSuccess()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;

			if (_state == CircuitState.HalfOpen)
			{
				_successfulProbes++;

				if (_successfulProbes >= _options.SuccessThreshold)
				{
					TransitionTo(CircuitState.Closed);
				}
			}
		}
	}

	/// <inheritdoc />
	public void RecordFailure(Exception? exception = null)
	{
		lock (_lock)
		{
			_consecutiveFailures++;

			if (_logger != null)
			{
				LogCircuitBreakerFailureRecorded(_logger, _name, _consecutiveFailures, exception);
			}

			if (_state == CircuitState.HalfOpen)
			{
				// Any failure in half-open reopens the circuit
				TransitionTo(CircuitState.Open, exception);
			}
			else if (_state == CircuitState.Closed && _consecutiveFailures >= _options.FailureThreshold)
			{
				TransitionTo(CircuitState.Open, exception);
			}
		}
	}

	/// <inheritdoc />
	public void Reset()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;
			_successfulProbes = 0;
			_lastOpenedAt = null;

			if (_state != CircuitState.Closed)
			{
				TransitionTo(CircuitState.Closed);
			}

			if (_logger != null)
			{
				LogCircuitBreakerManuallyReset(_logger, _name);
			}
		}
	}

	private void EnsureCircuitAllowsExecution()
	{
		var currentState = State; // This checks and potentially transitions Open -> HalfOpen

		if (currentState == CircuitState.Open)
		{
			var retryAfter = _lastOpenedAt.HasValue
				? _options.OpenDuration - (DateTimeOffset.UtcNow - _lastOpenedAt.Value)
				: _options.OpenDuration;

			if (retryAfter < TimeSpan.Zero)
			{
				retryAfter = TimeSpan.Zero;
			}

			throw new CircuitBreakerOpenException(_name, retryAfter);
		}
	}

	private bool ShouldAttemptReset()
	{
		if (!_lastOpenedAt.HasValue)
		{
			return false;
		}

		var elapsed = DateTimeOffset.UtcNow - _lastOpenedAt.Value;
		return elapsed >= _options.OpenDuration;
	}

	private bool ShouldHandleException(Exception exception)
	{
		if (_shouldHandle is not null)
		{
			return _shouldHandle(exception);
		}

		// Default: handle all exceptions except OperationCanceledException
		return exception is not OperationCanceledException;
	}

	private void TransitionTo(CircuitState newState, Exception? triggeringException = null)
	{
		var previousState = _state;

		if (previousState == newState)
		{
			return;
		}

		_state = newState;

		if (newState == CircuitState.Open)
		{
			_lastOpenedAt = DateTimeOffset.UtcNow;
			_successfulProbes = 0;

			if (_logger != null)
			{
				LogCircuitBreakerOpened(_logger, _name, _consecutiveFailures);
			}
		}
		else if (newState == CircuitState.HalfOpen)
		{
			_successfulProbes = 0;

			if (_logger != null)
			{
				LogCircuitBreakerHalfOpen(_logger, _name);
			}
		}
		else if (newState == CircuitState.Closed)
		{
			_consecutiveFailures = 0;
			_successfulProbes = 0;

			if (_logger != null)
			{
				LogCircuitBreakerClosed(_logger, _name);
			}
		}

		// Raise event
		var args = new CircuitStateChangedEventArgs
		{
			PreviousState = previousState,
			NewState = newState,
			CircuitName = _name,
			TriggeringException = triggeringException,
		};

		StateChanged?.Invoke(this, args);
	}

	[LoggerMessage(LogLevel.Warning,
		"Circuit breaker '{CircuitName}' recorded failure #{FailureCount}")]
	private static partial void LogCircuitBreakerFailureRecorded(
				ILogger logger,
				string circuitName,
				int failureCount,
				Exception? exception);

	[LoggerMessage(LogLevel.Information, "Circuit breaker '{CircuitName}' manually reset")]
	private static partial void LogCircuitBreakerManuallyReset(
				ILogger logger,
				string circuitName);

	[LoggerMessage(LogLevel.Warning,
		"Circuit breaker '{CircuitName}' OPENED after {FailureCount} consecutive failures")]
	private static partial void LogCircuitBreakerOpened(
				ILogger logger,
				string circuitName,
				int failureCount);

	[LoggerMessage(LogLevel.Information,
		"Circuit breaker '{CircuitName}' transitioned to HALF-OPEN, testing recovery")]
	private static partial void LogCircuitBreakerHalfOpen(
				ILogger logger,
				string circuitName);

	[LoggerMessage(LogLevel.Information, "Circuit breaker '{CircuitName}' CLOSED, service recovered")]
	private static partial void LogCircuitBreakerClosed(
				ILogger logger,
				string circuitName);
}
