// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.CloudNative;

/// <summary>
/// Unified circuit breaker implementation consolidating patterns from multiple locations. Provides resilient operation execution with
/// automatic failure detection and recovery.
/// </summary>
public partial class CircuitBreakerPattern : IResiliencePattern, IPatternObservable, IAsyncDisposable
{
	/// <summary>
	/// Cached composite formats for string formatting performance.
	/// </summary>
	private static readonly CompositeFormat ConsecutiveFailuresFormat =
		CompositeFormat.Parse(ErrorConstants.CircuitBreakerConsecutiveFailures);

	private static readonly CompositeFormat RecoveryConfirmedFormat = CompositeFormat.Parse(ErrorConstants.CircuitBreakerRecoveryConfirmed);

	private readonly CircuitBreakerOptions _options;
	private readonly ILogger _logger;
	private readonly SemaphoreSlim _halfOpenSemaphore;
	private readonly List<IPatternObserver> _observers = [];
#if NET9_0_OR_GREATER

	private readonly Lock _observerLock = new();

#else

	private readonly object _observerLock = new();

#endif

	private readonly CircuitBreakerMetrics _metrics = new();
#if NET9_0_OR_GREATER

	private readonly Lock _stateLock = new();

#else

	private readonly object _stateLock = new();

#endif
	private readonly ConcurrentDictionary<string, long> _operationLatencies = new(StringComparer.Ordinal);
	private int _consecutiveFailures;
	private int _consecutiveSuccesses;
	private DateTimeOffset _openedAt = DateTimeOffset.MinValue;

	/// <summary>
	/// Initializes a new instance of the <see cref="CircuitBreakerPattern" /> class.
	/// </summary>
	/// <param name="name"> The name of the circuit breaker. </param>
	/// <param name="options"> The circuit breaker configuration options. </param>
	/// <param name="logger"> The logger instance for this circuit breaker. </param>
	public CircuitBreakerPattern(string name, CircuitBreakerOptions options, ILogger? logger = null)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;
		_halfOpenSemaphore = new SemaphoreSlim(_options.MaxHalfOpenTests, _options.MaxHalfOpenTests);
	}


	/// <summary>
	/// Gets the name of the circuit breaker pattern.
	/// </summary>
	/// <value>The current <see cref="Name"/> value.</value>
	public string Name { get; }

	/// <summary>
	/// Gets the configuration settings for the circuit breaker as a dictionary.
	/// </summary>
	/// <value>The current <see cref="Configuration"/> value.</value>
	public IReadOnlyDictionary<string, object> Configuration => new Dictionary<string, object>
(StringComparer.Ordinal)
	{
		[nameof(_options.FailureThreshold)] = _options.FailureThreshold,
		[nameof(_options.SuccessThreshold)] = _options.SuccessThreshold,
		[nameof(_options.OpenDuration)] = _options.OpenDuration,
		[nameof(_options.OperationTimeout)] = _options.OperationTimeout,
		[nameof(_options.MaxHalfOpenTests)] = _options.MaxHalfOpenTests,
	};

	/// <summary>
	/// Gets the current health status of the circuit breaker based on its state.
	/// </summary>
	/// <value>The current <see cref="HealthStatus"/> value.</value>
	public PatternHealthStatus HealthStatus => State switch
	{
		ResilienceState.Closed => PatternHealthStatus.Healthy,
		ResilienceState.HalfOpen => PatternHealthStatus.Degraded,
		ResilienceState.Open => PatternHealthStatus.Unhealthy,
		_ => PatternHealthStatus.Unknown,
	};

	/// <summary>
	/// Gets the pattern metrics for monitoring and diagnostics.
	/// </summary>
	/// <returns> A PatternMetrics object containing performance and state information. </returns>
	public PatternMetrics GetMetrics()
	{
		var circuitMetrics = GetCircuitBreakerMetrics();
		return new PatternMetrics
		{
			TotalOperations = circuitMetrics.TotalRequests,
			SuccessfulOperations = circuitMetrics.SuccessfulRequests,
			FailedOperations = circuitMetrics.FailedRequests,
			AverageOperationTime = circuitMetrics.AverageResponseTime,
			CustomMetrics = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["RejectedRequests"] = circuitMetrics.RejectedRequests,
				["FallbackExecutions"] = circuitMetrics.FallbackExecutions,
				["ConsecutiveFailures"] = circuitMetrics.ConsecutiveFailures,
				["ConsecutiveSuccesses"] = circuitMetrics.ConsecutiveSuccesses,
				["State"] = circuitMetrics.CurrentState.ToString(),
			},
		};
	}

	/// <summary>
	/// Initializes the circuit breaker pattern with the specified configuration.
	/// </summary>
	/// <param name="configuration"> The initialization configuration parameters. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous initialization operation. </returns>
	public Task InitializeAsync(IReadOnlyDictionary<string, object> configuration, CancellationToken cancellationToken)
	{
		LogCircuitBreakerInitializing(Name);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Starts the circuit breaker pattern.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous start operation. </returns>
	public Task StartAsync(CancellationToken cancellationToken)
	{
		LogCircuitBreakerStarting(Name);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Stops the circuit breaker pattern.
	/// </summary>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> A task representing the asynchronous stop operation. </returns>
	public Task StopAsync(CancellationToken cancellationToken)
	{
		LogCircuitBreakerStopping(Name);
		return Task.CompletedTask;
	}



	/// <summary>
	/// Gets the current resilience state of the circuit breaker (Closed, Open, HalfOpen).
	/// </summary>
	/// <value>The current <see cref="State"/> value.</value>
	public ResilienceState State { get; private set; } = ResilienceState.Closed;

	/// <summary>
	/// Executes an operation through the circuit breaker, throwing an exception if the circuit is open.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> The result of the operation. </returns>
	/// <exception cref="CircuitBreakerOpenException"> Thrown when the circuit is open and no fallback is provided. </exception>
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken) =>
		await ExecuteAsync(operation, () => throw new CircuitBreakerOpenException(Name), cancellationToken).ConfigureAwait(false);

	/// <summary>
	/// Executes an operation through the circuit breaker with a fallback function.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. </typeparam>
	/// <param name="operation"> The operation to execute. </param>
	/// <param name="fallback"> The fallback function to execute if the circuit is open or the operation fails. </param>
	/// <param name="cancellationToken"> A cancellation token to cancel the operation. </param>
	/// <returns> The result of the operation or fallback function. </returns>
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(fallback);

		_metrics.IncrementTotalRequests();
		var startTimestamp = ValueStopwatch.GetTimestamp();

		try
		{
			// Check circuit state
			var canExecute = ShouldAllowRequest();
			if (!canExecute)
			{
				_metrics.IncrementRejectedRequests();
				LogCircuitBreakerOpenExecutingFallback(Name);

				_metrics.IncrementFallbackExecutions();
				return await fallback().ConfigureAwait(false);
			}

			// Execute operation with timeout
			using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			cts.CancelAfter(_options.OperationTimeout);

			var result = await operation().ConfigureAwait(false);

			// Record success
			RecordSuccess();
			_metrics.IncrementSuccessfulRequests();

			return result;
		}
		catch (Exception ex)
		{
			LogOperationFailed(ex, Name);

			// Record failure
			RecordFailure();
			_metrics.IncrementFailedRequests();

			// Execute fallback
			_metrics.IncrementFallbackExecutions();
			return await fallback().ConfigureAwait(false);
		}
		finally
		{
			var elapsedTicks = ValueStopwatch.GetTimestamp() - startTimestamp;
			var elapsedMs = elapsedTicks * (1000.0 / ValueStopwatch.GetFrequency());
			RecordLatency((long)elapsedMs);
			UpdateAverageResponseTime();
		}
	}

	/// <summary>
	/// Manually resets the circuit breaker to the closed state, clearing all failure counters.
	/// </summary>
	public void Reset()
	{
		lock (_stateLock)
		{
			var previousState = State;
			State = ResilienceState.Closed;
			_consecutiveFailures = 0;
			_consecutiveSuccesses = 0;
			_openedAt = DateTimeOffset.MinValue;

			_metrics.CurrentState = State;
			_metrics.ConsecutiveFailures = _consecutiveFailures;
			_metrics.ConsecutiveSuccesses = _consecutiveSuccesses;

			LogCircuitBreakerReset(Name);

			NotifyStateChange(previousState, State, ErrorConstants.CircuitBreakerManualReset);
		}
	}

	private bool ShouldAllowRequest()
	{
		lock (_stateLock)
		{
			switch (State)
			{
				case ResilienceState.Closed:
					return true;

				case ResilienceState.Open:
					if (DateTimeOffset.UtcNow - _openedAt >= _options.OpenDuration)
					{
						TransitionToHalfOpen();
						return _halfOpenSemaphore.Wait(0);
					}

					return false;

				case ResilienceState.HalfOpen:
					return _halfOpenSemaphore.Wait(0);

				default:
					return false;
			}
		}
	}

	private void RecordSuccess()
	{
		lock (_stateLock)
		{
			_consecutiveFailures = 0;
			_consecutiveSuccesses++;

			if (State == ResilienceState.HalfOpen && _consecutiveSuccesses >= _options.SuccessThreshold)
			{
				TransitionToClosed();
			}

			_metrics.ConsecutiveSuccesses = _consecutiveSuccesses;
			_metrics.ConsecutiveFailures = _consecutiveFailures;
		}
	}

	private void RecordFailure()
	{
		lock (_stateLock)
		{
			_consecutiveSuccesses = 0;
			_consecutiveFailures++;

			switch (State)
			{
				case ResilienceState.Closed when _consecutiveFailures >= _options.FailureThreshold:
				case ResilienceState.HalfOpen:
					TransitionToOpen();
					break;

				case ResilienceState.Closed:
					// Stay closed, continue monitoring failures
					break;

				case ResilienceState.Open:
					// Already open, no action needed
					break;
				default:
					break;
			}

			_metrics.ConsecutiveSuccesses = _consecutiveSuccesses;
			_metrics.ConsecutiveFailures = _consecutiveFailures;
		}
	}

	private void TransitionToOpen()
	{
		var previousState = State;
		State = ResilienceState.Open;
		_openedAt = DateTimeOffset.UtcNow;
		_metrics.CurrentState = State;

		LogCircuitBreakerOpenTransition(Name, _consecutiveFailures);

		NotifyStateChange(previousState, State,
			string.Format(CultureInfo.InvariantCulture, ConsecutiveFailuresFormat, _consecutiveFailures));
	}

	private void TransitionToHalfOpen()
	{
		var previousState = State;
		State = ResilienceState.HalfOpen;
		_consecutiveSuccesses = 0;
		_consecutiveFailures = 0;
		_metrics.CurrentState = State;

		// Reset half-open semaphore
		while (_halfOpenSemaphore.CurrentCount < _options.MaxHalfOpenTests)
		{
			_ = _halfOpenSemaphore.Release();
		}

		LogCircuitBreakerHalfOpenTransition(Name);
		NotifyStateChange(previousState, State, ErrorConstants.CircuitBreakerTestingRecovery);
	}

	private void TransitionToClosed()
	{
		var previousState = State;
		State = ResilienceState.Closed;
		_consecutiveFailures = 0;
		_metrics.CurrentState = State;

		LogCircuitBreakerClosedTransition(Name, _consecutiveSuccesses);

		NotifyStateChange(previousState, State,
			string.Format(CultureInfo.InvariantCulture, RecoveryConfirmedFormat, _consecutiveSuccesses));
	}

	private void RecordLatency(long milliseconds) =>
		_ = _operationLatencies.AddOrUpdate(
			"default",
			(key, state) =>
			{
				_ = key;
				return state;
			},
			(key, old, state) =>
			{
				_ = key;
				return (old + state) / 2;
			},
			milliseconds);

	private void UpdateAverageResponseTime()
	{
		if (_operationLatencies.IsEmpty)
		{
			return;
		}

		var avgMs = _operationLatencies.Values.Average();
		_metrics.AverageResponseTime = TimeSpan.FromMilliseconds(avgMs);
	}



	/// <summary>
	/// Gets the current metrics for the circuit breaker including request counts, states, and performance data.
	/// </summary>
	/// <returns> A CircuitBreakerMetrics object containing comprehensive metrics information. </returns>
	public CircuitBreakerMetrics GetCircuitBreakerMetrics() =>
		new()
		{
			TotalRequests = _metrics.TotalRequests,
			SuccessfulRequests = _metrics.SuccessfulRequests,
			FailedRequests = _metrics.FailedRequests,
			RejectedRequests = _metrics.RejectedRequests,
			FallbackExecutions = _metrics.FallbackExecutions,
			AverageResponseTime = _metrics.AverageResponseTime,
			CurrentState = _metrics.CurrentState,
			ConsecutiveFailures = _metrics.ConsecutiveFailures,
			ConsecutiveSuccesses = _metrics.ConsecutiveSuccesses,
		};

	private void NotifyStateChange(ResilienceState previousState, ResilienceState newState, string reason)
	{
		var stateChange = new PatternStateChange
		{
			PreviousState = previousState.ToString(),
			NewState = newState.ToString(),
			Reason = reason,
			Context = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["ConsecutiveFailures"] = _consecutiveFailures,
				["ConsecutiveSuccesses"] = _consecutiveSuccesses,
				["TotalRequests"] = _metrics.TotalRequests,
			},
		};

		lock (_observerLock)
		{
			foreach (var observer in _observers)
			{
				try
				{
					_ = observer.OnPatternStateChangedAsync(this, stateChange);
				}
				catch (Exception ex)
				{
					LogObserverNotificationError(ex, Name);
				}
			}
		}
	}



	/// <summary>
	/// Subscribes an observer to receive notifications about pattern state changes and metrics updates.
	/// </summary>
	/// <param name="observer"> The observer to subscribe for notifications. </param>
	public void Subscribe(IPatternObserver observer)
	{
		ArgumentNullException.ThrowIfNull(observer);

		lock (_observerLock)
		{
			if (!_observers.Contains(observer))
			{
				_observers.Add(observer);
				LogObserverSubscribed(Name);
			}
		}
	}

	/// <summary>
	/// Unsubscribes an observer from receiving notifications about pattern state changes and metrics updates.
	/// </summary>
	/// <param name="observer"> The observer to unsubscribe from notifications. </param>
	public void Unsubscribe(IPatternObserver observer)
	{
		ArgumentNullException.ThrowIfNull(observer);

		lock (_observerLock)
		{
			if (_observers.Remove(observer))
			{
				LogObserverUnsubscribed(Name);
			}
		}
	}



	/// <summary>
	/// Asynchronously disposes the circuit breaker, stopping it and cleaning up resources.
	/// </summary>
	/// <returns> A task representing the asynchronous disposal operation. </returns>
	public async ValueTask DisposeAsync()
	{
		try
		{
			await StopAsync(CancellationToken.None).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogCircuitBreakerStopError(ex, Name);
		}

		_halfOpenSemaphore.Dispose();

		lock (_observerLock)
		{
			_observers.Clear();
		}

		GC.SuppressFinalize(this);
	}

	#region LoggerMessage Definitions

	[LoggerMessage(CoreEventId.CircuitBreakerInitializing, LogLevel.Information,
		"Initializing circuit breaker {Name} with configuration")]
	private partial void LogCircuitBreakerInitializing(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerStarting, LogLevel.Information,
		"Starting circuit breaker {Name}")]
	private partial void LogCircuitBreakerStarting(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerStopping, LogLevel.Information,
		"Stopping circuit breaker {Name}")]
	private partial void LogCircuitBreakerStopping(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerOpenExecutingFallback, LogLevel.Warning,
		"Circuit breaker {Name} is OPEN - executing fallback")]
	private partial void LogCircuitBreakerOpenExecutingFallback(string name);

	[LoggerMessage(CoreEventId.OperationFailed, LogLevel.Error,
		"Operation failed in circuit breaker {Name}")]
	private partial void LogOperationFailed(Exception ex, string name);

	[LoggerMessage(CoreEventId.CircuitBreakerReset, LogLevel.Information,
		"Circuit breaker {Name} manually reset to CLOSED state")]
	private partial void LogCircuitBreakerReset(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerOpenTransition, LogLevel.Warning,
		"Circuit breaker {Name} transitioned to OPEN state after {Failures} consecutive failures")]
	private partial void LogCircuitBreakerOpenTransition(string name, int failures);

	[LoggerMessage(CoreEventId.CircuitBreakerHalfOpenTransition, LogLevel.Information,
		"Circuit breaker {Name} transitioned to HALF-OPEN state")]
	private partial void LogCircuitBreakerHalfOpenTransition(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerClosedTransition, LogLevel.Information,
		"Circuit breaker {Name} transitioned to CLOSED state after {Successes} consecutive successes")]
	private partial void LogCircuitBreakerClosedTransition(string name, int successes);

	[LoggerMessage(CoreEventId.ObserverNotificationError, LogLevel.Error,
		"Error notifying observer of state change in circuit breaker {Name}")]
	private partial void LogObserverNotificationError(Exception ex, string name);

	[LoggerMessage(CoreEventId.ObserverSubscribed, LogLevel.Debug,
		"Observer subscribed to circuit breaker {Name}")]
	private partial void LogObserverSubscribed(string name);

	[LoggerMessage(CoreEventId.ObserverUnsubscribed, LogLevel.Debug,
		"Observer unsubscribed from circuit breaker {Name}")]
	private partial void LogObserverUnsubscribed(string name);

	[LoggerMessage(CoreEventId.CircuitBreakerStopError, LogLevel.Error,
		"Error stopping circuit breaker {Name} during disposal")]
	private partial void LogCircuitBreakerStopError(Exception ex, string name);

	#endregion
}
