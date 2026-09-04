// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;

using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Diagnostics;

namespace Excalibur.Dispatch.Resilience;

/// <summary>
/// Implementation of the circuit breaker pattern for protecting operations from repeated failures.
/// </summary>
internal sealed partial class CircuitBreakerPolicy : ICircuitBreakerPolicy, ICircuitBreakerDiagnostics, ICircuitBreakerEvents
{
	private readonly CircuitBreakerOptions _options;
	private readonly ILogger? _logger;
	private readonly string _name;
	private readonly Func<Exception, bool>? _shouldHandle;
	private readonly Lock _lock = new();

	private CircuitState _state = CircuitState.Closed;
	private int _consecutiveFailures;
	private int _successfulProbes;
	private DateTimeOffset? _lastOpenedAt;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerPolicy"/> class.
	/// </summary>
	/// <param name="options">The circuit breaker configuration options.</param>
	/// <param name="name">The name of the circuit breaker (e.g., transport name).</param>
	/// <param name="logger">Optional logger instance. A non-generic <see cref="ILogger"/> is accepted so that
	/// callers constructing a policy through <see cref="ITransportCircuitBreakerRegistry"/> may forward their own
	/// logger; the source-generated log methods take <see cref="ILogger"/> directly, so no category is lost.</param>
	/// <param name="shouldHandle">Optional predicate to determine which exceptions should trip the circuit.</param>
	/// <param name="timeProvider">
	/// Clock used for the open-duration deadline. Defaults to <see cref="TimeProvider.System"/>; supply a
	/// controllable provider to exercise recovery without sleeping.
	/// </param>
	public CircuitBreakerPolicy(
		CircuitBreakerOptions options,
		string name = "default",
		ILogger? logger = null,
		Func<Exception, bool>? shouldHandle = null,
		TimeProvider? timeProvider = null)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_name = name ?? throw new ArgumentNullException(nameof(name));
		_logger = logger;
		_shouldHandle = shouldHandle;

		// The open-duration deadline is the only thing gating the half-open probe, so with a
		// hard-coded clock the one transition worth testing is reachable only by sleeping. That is
		// why the recovery path had no coverage.
		_timeProvider = timeProvider ?? TimeProvider.System;
	}

	private readonly TimeProvider _timeProvider;

	// Half-open admits ONE trial call. Without this every request that arrives while the circuit is
	// half-open sees HalfOpen, passes the Open check, and proceeds -- so a recovering dependency is
	// met with a stampede at the exact moment it is least able to take one.
	private int _halfOpenProbeInFlight;

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

	// The object that transitions is the only one that knows it transitioned. Emitting from here
	// covers every caller of the policy, not just the middleware, and removes the before/after
	// diffing a caller would otherwise have to do around each recorded outcome.
	private static readonly Meter TransitionMeter =
		new(DispatchTelemetryConstants.Meters.CircuitBreakerMiddleware, "1.0.0");

	private static readonly Counter<long> TransitionsCounter = TransitionMeter.CreateCounter<long>(
		"dispatch.circuit_breaker.transitions",
		unit: "{transitions}",
		description: "Number of circuit breaker state transitions, tagged with circuit.key, from_state and to_state.");

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
	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		Func<CancellationToken, Task<TResult>> action,
		Func<TResult, bool> isFailure,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(isFailure);

		EnsureCircuitAllowsExecution();

		try
		{
			var result = await action(cancellationToken).ConfigureAwait(false);

			if (isFailure(result))
			{
				RecordFailure();
			}
			else
			{
				RecordSuccess();
			}

			return result;
		}
		catch (Exception ex) when (ShouldHandleException(ex))
		{
			RecordFailure(ex);
			throw;
		}
	}

	private void RecordSuccess()
	{
		lock (_lock)
		{
			_consecutiveFailures = 0;

			if (_state == CircuitState.HalfOpen)
			{
				_ = Interlocked.Exchange(ref _halfOpenProbeInFlight, 0);
				_successfulProbes++;

				// One successful probe closes the circuit, matching Polly.
				if (_successfulProbes >= 1)
				{
					TransitionTo(CircuitState.Closed);
				}
			}
		}
	}

	private void RecordFailure(Exception? exception = null)
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
				_ = Interlocked.Exchange(ref _halfOpenProbeInFlight, 0);
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

		if (currentState == CircuitState.HalfOpen
			&& Interlocked.CompareExchange(ref _halfOpenProbeInFlight, 1, 0) != 0)
		{
			// A probe is already running. Everyone else waits rather than piling onto a dependency
			// that has not yet shown it recovered.
			throw new CircuitBreakerOpenException(_name, _options.OpenDuration);
		}

		if (currentState == CircuitState.Open)
		{
			var retryAfter = _lastOpenedAt.HasValue
				? _options.OpenDuration - (_timeProvider.GetUtcNow() - _lastOpenedAt.Value)
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

		var elapsed = _timeProvider.GetUtcNow() - _lastOpenedAt.Value;
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

	private static string ToTag(CircuitState state) => state switch
	{
		CircuitState.Closed => "closed",
		CircuitState.Open => "open",
		CircuitState.HalfOpen => "half_open",
		// Every declared state is named above; anything else is a new enum member whose tag
		// nobody has chosen, and inventing one by casing would hide that.
		_ => state.ToString(),
	};

	private void TransitionTo(CircuitState newState, Exception? triggeringException = null)
	{
		var previousState = _state;

		if (previousState == newState)
		{
			return;
		}

		TransitionsCounter.Add(
			1,
			new KeyValuePair<string, object?>("circuit.key", _name),
			new KeyValuePair<string, object?>("from_state", ToTag(previousState)),
			new KeyValuePair<string, object?>("to_state", ToTag(newState)));

		_state = newState;

		if (newState == CircuitState.Open)
		{
			_lastOpenedAt = _timeProvider.GetUtcNow();
			_ = Interlocked.Exchange(ref _halfOpenProbeInFlight, 0);
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
