// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.CloudNative;
using Excalibur.Dispatch.Options.Resilience;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Polly;
using Polly.CircuitBreaker;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Polly-based implementation of circuit breaker pattern, replacing custom implementations with trusted library.
/// </summary>
public partial class PollyCircuitBreakerAdapter : IResiliencePattern, IPatternObservable, IAsyncDisposable
{
	private readonly ResiliencePipeline _pipeline;
	private readonly CircuitBreakerOptions _options;
	private readonly ILogger _logger;
	private readonly List<IPatternObserver> _observers = [];
#if NET9_0_OR_GREATER

	private readonly Lock _observerLock = new();

#else

	private readonly object _observerLock = new();

#endif
	private int _state = (int)ResilienceState.Closed;
	private long _totalRequests;
	private long _successfulRequests;
	private long _failedRequests;
	private long _rejectedRequests;
	private long _fallbackExecutions;

	/// <summary>
	/// Initializes a new instance of the <see cref="PollyCircuitBreakerAdapter" /> class.
	/// </summary>
	/// <param name="name"> The name of the circuit breaker. </param>
	/// <param name="options"> Circuit breaker configuration options. </param>
	/// <param name="logger"> Optional logger instance. </param>
	public PollyCircuitBreakerAdapter(string name, CircuitBreakerOptions options, ILogger? logger = null)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? NullLogger.Instance;

		// Create Polly resilience pipeline with circuit breaker
		_pipeline = new ResiliencePipelineBuilder()
			.AddCircuitBreaker(new CircuitBreakerStrategyOptions
			{
				FailureRatio = 0.5, // 50% failure rate threshold
				SamplingDuration = TimeSpan.FromSeconds(30),
				MinimumThroughput = _options.FailureThreshold,
				BreakDuration = _options.OpenDuration,
				ShouldHandle = new PredicateBuilder().Handle<Exception>(),
			})
			.AddTimeout(_options.OperationTimeout)
			.Build();

		// Note: Polly 8.x event handling is different - would need custom telemetry setup For now, we'll track state changes through our
		// own metrics
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public IReadOnlyDictionary<string, object> Configuration => new Dictionary<string, object>(StringComparer.Ordinal)
	{
		[nameof(_options.FailureThreshold)] = _options.FailureThreshold,
		[nameof(_options.SuccessThreshold)] = _options.SuccessThreshold,
		[nameof(_options.OpenDuration)] = _options.OpenDuration,
		[nameof(_options.OperationTimeout)] = _options.OperationTimeout,
		[nameof(_options.MaxHalfOpenTests)] = _options.MaxHalfOpenTests,
	};

	/// <inheritdoc />
	public PatternHealthStatus HealthStatus => State switch
	{
		ResilienceState.Closed => PatternHealthStatus.Healthy,
		ResilienceState.HalfOpen => PatternHealthStatus.Degraded,
		ResilienceState.Open => PatternHealthStatus.Unhealthy,
		_ => PatternHealthStatus.Unknown,
	};

	/// <inheritdoc />
	public ResilienceState State => (ResilienceState)Volatile.Read(ref _state);

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MA0061:Method overrides should not change default values", Justification = "Preserves optional cancellation token for consumers.")]
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		_ = Interlocked.Increment(ref _totalRequests);

		try
		{
			var result = await _pipeline.ExecuteAsync(async _ => await operation().ConfigureAwait(false), cancellationToken)
				.ConfigureAwait(false);

			Volatile.Write(ref _state, (int)ResilienceState.Closed);
			_ = Interlocked.Increment(ref _successfulRequests);
			return result;
		}
		catch (BrokenCircuitException)
		{
			Volatile.Write(ref _state, (int)ResilienceState.Open);
			_ = Interlocked.Increment(ref _rejectedRequests);
			LogCircuitBreakerRejected(Name);
			throw new CircuitBreakerOpenException(Name);
		}
		catch (Exception ex)
		{
			_ = Interlocked.Increment(ref _failedRequests);
			LogOperationFailed(Name, ex);
			throw;
		}
	}

	/// <inheritdoc />
	[System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "MA0061:Method overrides should not change default values", Justification = "Preserves optional cancellation token for consumers.")]
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, Func<Task<T>> fallback, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);
		ArgumentNullException.ThrowIfNull(fallback);

		try
		{
			return await ExecuteAsync(operation, cancellationToken).ConfigureAwait(false);
		}
		catch (CircuitBreakerOpenException)
		{
			_ = Interlocked.Increment(ref _fallbackExecutions);
			LogFallbackExecuted(Name);
			return await fallback().ConfigureAwait(false);
		}
	}

	/// <inheritdoc />
	/// <remarks>Polly manages circuit reset internally; the adapter logs requests for observability.</remarks>
	public void Reset()
	{
		LogResetRequested(Name);
		Volatile.Write(ref _state, (int)ResilienceState.Closed);
	}

	/// <inheritdoc />
	public PatternMetrics GetMetrics()
	{
		_ = GetCircuitBreakerMetrics();
		return new PatternMetrics
		{
			TotalOperations = _totalRequests,
			SuccessfulOperations = _successfulRequests,
			FailedOperations = _failedRequests,
			AverageOperationTime = TimeSpan.Zero, // Could calculate from recorded latencies
			CustomMetrics = new Dictionary<string, object>(StringComparer.Ordinal)
			{
				["RejectedRequests"] = _rejectedRequests,
				["FallbackExecutions"] = _fallbackExecutions,
				["State"] = State.ToString(),
			},
		};
	}

	/// <inheritdoc />
	public Task InitializeAsync(IReadOnlyDictionary<string, object> configuration, CancellationToken cancellationToken)
	{
		LogInitializing(Name);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StartAsync(CancellationToken cancellationToken)
	{
		LogStarting(Name);
		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task StopAsync(CancellationToken cancellationToken)
	{
		LogStopping(Name);
		return Task.CompletedTask;
	}

	/// <summary>
	/// Gets circuit breaker specific metrics.
	/// </summary>
	/// <returns> Circuit breaker metrics. </returns>
	public CircuitBreakerMetrics GetCircuitBreakerMetrics() =>
		new()
		{
			TotalRequests = _totalRequests,
			SuccessfulRequests = _successfulRequests,
			FailedRequests = _failedRequests,
			RejectedRequests = _rejectedRequests,
			FallbackExecutions = _fallbackExecutions,
			AverageResponseTime = TimeSpan.Zero, // Could calculate from recorded latencies
			CurrentState = State,
			ConsecutiveFailures = 0, // Polly manages this internally
			ConsecutiveSuccesses = 0, // Polly manages this internally
		};

	/// <inheritdoc />
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

	/// <inheritdoc />
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

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		try
		{
			using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
			await StopAsync(timeoutCts.Token).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogStopError(Name, ex);
		}

		// ResiliencePipeline in Polly 8.x doesn't implement IDisposable Resources are managed automatically
		lock (_observerLock)
		{
			_observers.Clear();
		}

		Volatile.Write(ref _state, (int)ResilienceState.Closed);
		GC.SuppressFinalize(this);
	}

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.CircuitBreakerRejected, LogLevel.Warning,
		"Circuit breaker {Name} is OPEN - request rejected")]
	private partial void LogCircuitBreakerRejected(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerOperationFailed, LogLevel.Error,
		"Operation failed in circuit breaker {Name}")]
	private partial void LogOperationFailed(string name, Exception ex);

	[LoggerMessage(ResilienceEventId.CircuitBreakerFallbackExecuted, LogLevel.Warning,
		"Circuit breaker {Name} is OPEN - executing fallback")]
	private partial void LogFallbackExecuted(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerResetRequested, LogLevel.Information,
		"Circuit breaker {Name} reset requested (Polly manages state internally)")]
	private partial void LogResetRequested(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerInitializing, LogLevel.Information,
		"Initializing Polly circuit breaker {Name} with configuration")]
	private partial void LogInitializing(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerStarting, LogLevel.Information,
		"Starting Polly circuit breaker {Name}")]
	private partial void LogStarting(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerStopping, LogLevel.Information,
		"Stopping Polly circuit breaker {Name}")]
	private partial void LogStopping(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerObserverSubscribed, LogLevel.Debug,
		"Observer subscribed to circuit breaker {Name}")]
	private partial void LogObserverSubscribed(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerObserverUnsubscribed, LogLevel.Debug,
		"Observer unsubscribed from circuit breaker {Name}")]
	private partial void LogObserverUnsubscribed(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerStopError, LogLevel.Error,
		"Error stopping circuit breaker {Name} during disposal")]
	private partial void LogStopError(string name, Exception ex);
}
