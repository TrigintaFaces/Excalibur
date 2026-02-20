// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Text.Json;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Polly;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Distributed circuit breaker implementation using cache for coordination.
/// </summary>
public partial class DistributedCircuitBreaker : IDistributedCircuitBreaker, IAsyncDisposable
{
	private readonly DistributedCircuitBreakerOptions _options;
	private readonly IDistributedCache _cache;
	private readonly ILogger _logger;
	private readonly ResiliencePipeline _localPipeline;
	private readonly Timer _syncTimer;
	private readonly CancellationTokenSource _shutdownCts = new();
	private readonly ConcurrentBag<Task> _backgroundTasks = new();
	private volatile bool _disposed;
	private volatile CircuitState _lastKnownState;

	/// <summary>
	/// Initializes a new instance of the <see cref="DistributedCircuitBreaker" /> class.
	/// </summary>
	/// <param name="name">The name of the circuit breaker.</param>
	/// <param name="cache">The distributed cache used for coordination.</param>
	/// <param name="options">The configuration options for the circuit breaker.</param>
	/// <param name="logger">The logger used for diagnostic output.</param>
	public DistributedCircuitBreaker(
		string name,
		IDistributedCache cache,
		IOptions<DistributedCircuitBreakerOptions> options,
		ILogger<DistributedCircuitBreaker> logger)
	{
		Name = name ?? throw new ArgumentNullException(nameof(name));
		_cache = cache ?? throw new ArgumentNullException(nameof(cache));
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_lastKnownState = CircuitState.Closed;

		// Create local circuit breaker as fallback
		_localPipeline = new ResiliencePipelineBuilder()
			.AddCircuitBreaker(new global::Polly.CircuitBreaker.CircuitBreakerStrategyOptions
			{
				FailureRatio = _options.FailureRatio,
				SamplingDuration = _options.SamplingDuration,
				MinimumThroughput = _options.MinimumThroughput,
				BreakDuration = _options.BreakDuration,
				ShouldHandle = new PredicateBuilder().Handle<Exception>(),
			})
			.Build();

		// Start synchronization timer
		_syncTimer = new Timer(
			SynchronizeStateCallback,
			state: null,
			TimeSpan.Zero,
			_options.SyncInterval);
	}

	/// <summary>
	/// Gets the circuit breaker name, used for cache key generation and logging.
	/// </summary>
	public string Name { get; }

	/// <inheritdoc />
	public async Task<CircuitState> GetStateAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stateKey = GetStateKey();
			var stateData = await _cache.GetStringAsync(stateKey, cancellationToken).ConfigureAwait(false);

			if (string.IsNullOrEmpty(stateData))
			{
				return CircuitState.Closed;
			}

			var state = ParseState(stateData);
			return state.State;
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
			return _lastKnownState;
		}
	}

	/// <inheritdoc />
	public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(operation);

		// Check distributed state first
		var state = await GetStateAsync(cancellationToken).ConfigureAwait(false);

		if (state == CircuitState.Open)
		{
			var stateData = await GetDistributedStateAsync(cancellationToken).ConfigureAwait(false);
			if (stateData != null && DateTimeOffset.UtcNow < stateData.OpenUntil)
			{
				throw new global::Polly.CircuitBreaker.BrokenCircuitException($"Distributed circuit breaker '{Name}' is open");
			}

			// Try to transition to half-open
			await TransitionToHalfOpenAsync(cancellationToken).ConfigureAwait(false);
		}

		try
		{
			// Execute with local circuit breaker as well
			var result = await _localPipeline.ExecuteAsync(
				async _ => await operation().ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			await RecordSuccessAsync(cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex)
		{
			await RecordFailureAsync(cancellationToken, ex).ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync(CancellationToken cancellationToken)
	{
		try
		{
			var metricsKey = GetMetricsKey();
			var metrics = await GetOrCreateMetricsAsync(cancellationToken).ConfigureAwait(false);

			metrics.SuccessCount++;
			metrics.ConsecutiveFailures = 0;
			metrics.LastSuccess = DateTimeOffset.UtcNow;

			await SaveMetricsAsync(metrics, cancellationToken).ConfigureAwait(false);

			// Check if we should close the circuit
			if (_lastKnownState == CircuitState.HalfOpen && metrics.ConsecutiveSuccesses >= _options.SuccessThresholdToClose)
			{
				await TransitionToClosedAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(CancellationToken cancellationToken, Exception? exception = null)
	{
		try
		{
			var metrics = await GetOrCreateMetricsAsync(cancellationToken).ConfigureAwait(false);

			metrics.FailureCount++;
			metrics.ConsecutiveFailures++;
			metrics.ConsecutiveSuccesses = 0;
			metrics.LastFailure = DateTimeOffset.UtcNow;

			if (exception != null)
			{
				metrics.LastFailureReason = exception.Message;
			}

			await SaveMetricsAsync(metrics, cancellationToken).ConfigureAwait(false);

			// Check if we should open the circuit
			var total = metrics.SuccessCount + metrics.FailureCount;
			var failureRate = total > 0 ? (double)metrics.FailureCount / total : 0;
			if (failureRate > _options.FailureRatio ||
				metrics.ConsecutiveFailures >= _options.ConsecutiveFailureThreshold)
			{
				LogThresholdExceeded(Name, (int)metrics.FailureCount, _options.MinimumThroughput);
				await TransitionToOpenAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
	}

	/// <inheritdoc />
	public async Task ResetAsync(CancellationToken cancellationToken)
	{
		try
		{
			// Clear all distributed state
			await _cache.RemoveAsync(GetStateKey(), cancellationToken).ConfigureAwait(false);
			await _cache.RemoveAsync(GetMetricsKey(), cancellationToken).ConfigureAwait(false);

			_lastKnownState = CircuitState.Closed;
			LogCircuitClosed(Name);
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Signal shutdown to background tasks
#if NET8_0_OR_GREATER
		await _shutdownCts.CancelAsync().ConfigureAwait(false);
#else
		_shutdownCts.Cancel();
#endif

		// Wait for tracked background tasks to complete
		try
		{
			await Task.WhenAll(_backgroundTasks.ToArray()).ConfigureAwait(false);
		}
		catch (OperationCanceledException)
		{
			// Expected during shutdown
		}

		await _syncTimer.DisposeAsync().ConfigureAwait(false);
		_shutdownCts.Dispose();
		GC.SuppressFinalize(this);
	}

	private static DistributedCircuitState ParseState(string json) =>
		JsonSerializer.Deserialize(json, DistributedCircuitJsonContext.Default.DistributedCircuitState)
		?? new DistributedCircuitState { State = CircuitState.Closed };

	private void SynchronizeStateCallback(object? state)
	{
		if (_disposed)
		{
			return;
		}

		var task = SynchronizeStateCoreAsync();
		_backgroundTasks.Add(task);
	}

	private async Task SynchronizeStateCoreAsync()
	{
		try
		{
			var currentState = await GetStateAsync(_shutdownCts.Token).ConfigureAwait(false);
			if (currentState != _lastKnownState)
			{
				_lastKnownState = currentState;
				LogStateChanged(Name, currentState);
			}

			// Clean up expired states
			await CleanupExpiredStateAsync().ConfigureAwait(false);
		}
		catch (OperationCanceledException) when (_shutdownCts.IsCancellationRequested)
		{
			// Expected during shutdown — swallow
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
	}

	private async Task<DistributedCircuitState?> GetDistributedStateAsync(CancellationToken cancellationToken)
	{
		var stateData = await _cache.GetStringAsync(GetStateKey(), cancellationToken).ConfigureAwait(false);
		return string.IsNullOrEmpty(stateData) ? null : ParseState(stateData);
	}

	private async Task<DistributedCircuitMetrics> GetOrCreateMetricsAsync(CancellationToken cancellationToken)
	{
		var metricsData = await _cache.GetStringAsync(GetMetricsKey(), cancellationToken).ConfigureAwait(false);
		return string.IsNullOrEmpty(metricsData)
			? new DistributedCircuitMetrics()
			: JsonSerializer.Deserialize(metricsData, DistributedCircuitJsonContext.Default.DistributedCircuitMetrics)
			  ?? new DistributedCircuitMetrics();
	}

	private async Task SaveMetricsAsync(DistributedCircuitMetrics metrics, CancellationToken cancellationToken)
	{
		var json = JsonSerializer.Serialize(metrics, DistributedCircuitJsonContext.Default.DistributedCircuitMetrics);
		await _cache.SetStringAsync(
			GetMetricsKey(),
			json,
			new DistributedCacheEntryOptions { SlidingExpiration = _options.MetricsRetention },
			cancellationToken).ConfigureAwait(false);
	}

	private async Task TransitionToOpenAsync(CancellationToken cancellationToken)
	{
		var state = new DistributedCircuitState
		{
			State = CircuitState.Open,
			OpenedAt = DateTimeOffset.UtcNow,
			OpenUntil = DateTimeOffset.UtcNow.Add(_options.BreakDuration),
			InstanceId = Environment.MachineName,
		};

		var json = JsonSerializer.Serialize(state, DistributedCircuitJsonContext.Default.DistributedCircuitState);
		await _cache.SetStringAsync(
			GetStateKey(),
			json,
			new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = _options.BreakDuration.Add(TimeSpan.FromMinutes(1)) },
			cancellationToken).ConfigureAwait(false);

		_lastKnownState = CircuitState.Open;
		LogCircuitOpened(Name);
	}

	private async Task TransitionToHalfOpenAsync(CancellationToken cancellationToken)
	{
		var state = new DistributedCircuitState
		{
			State = CircuitState.HalfOpen,
			TransitionedAt = DateTimeOffset.UtcNow,
			InstanceId = Environment.MachineName,
		};

		var json = JsonSerializer.Serialize(state, DistributedCircuitJsonContext.Default.DistributedCircuitState);
		await _cache.SetStringAsync(
			GetStateKey(),
			json,
			new DistributedCacheEntryOptions { SlidingExpiration = TimeSpan.FromMinutes(5) },
			cancellationToken).ConfigureAwait(false);

		_lastKnownState = CircuitState.HalfOpen;
	}

	private async Task TransitionToClosedAsync(CancellationToken cancellationToken)
	{
		await _cache.RemoveAsync(GetStateKey(), cancellationToken).ConfigureAwait(false);
		_lastKnownState = CircuitState.Closed;
		LogCircuitClosed(Name);
	}

	private Task CleanupExpiredStateAsync()
	{
		if (_options.MetricsRetention <= TimeSpan.Zero)
		{
			return Task.CompletedTask;
		}

		// Sliding expirations configured on save will evict stale entries automatically; manual cleanup can be added when required.
		return Task.CompletedTask;
	}

	private string GetStateKey() => $"circuit-breaker:{Name}:state";

	private string GetMetricsKey() => $"circuit-breaker:{Name}:metrics";

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.CircuitBreakerStateChanged, LogLevel.Warning,
		"Distributed circuit breaker '{Name}' state changed to {State}")]
	private partial void LogStateChanged(string name, CircuitState state);

	[LoggerMessage(ResilienceEventId.CircuitBreakerThresholdExceeded, LogLevel.Error,
		"Circuit breaker '{Name}' failure threshold exceeded: {Failures}/{Threshold}")]
	private partial void LogThresholdExceeded(string name, int failures, int threshold);

	[LoggerMessage(ResilienceEventId.CircuitBreakerOpened, LogLevel.Warning,
		"Distributed circuit breaker '{Name}' opened across all instances")]
	private partial void LogCircuitOpened(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerClosed, LogLevel.Information,
		"Distributed circuit breaker '{Name}' closed")]
	private partial void LogCircuitClosed(string name);

	[LoggerMessage(ResilienceEventId.CircuitBreakerCoordinationError, LogLevel.Error,
		"Error coordinating state for distributed circuit breaker '{Name}'")]
	private partial void LogCoordinationError(string name, Exception ex);
}
