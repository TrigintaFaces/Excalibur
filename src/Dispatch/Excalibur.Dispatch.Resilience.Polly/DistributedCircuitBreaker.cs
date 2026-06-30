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
	private ConcurrentBag<Task> _backgroundTasks = new();
	private readonly SemaphoreSlim _metricsGate = new(1, 1);
	private volatile bool _disposed;
	private volatile CircuitState _lastKnownState;

	// Number of buckets the rolling sampling window (SamplingDuration) is divided into for the
	// windowed open decision (zxb7fp). Mirrors Polly v8's rolling-health bucketing shape.
	private const int WindowBucketCount = 10;

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
				// FR-116-2: OperationCanceledException is never a failure — non-tripping.
			ShouldHandle = new PredicateBuilder().Handle<Exception>(ex => ex is not OperationCanceledException),
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

		// Single distributed state fetch to avoid double round-trip
		var stateData = await GetDistributedStateAsync(cancellationToken).ConfigureAwait(false);
		var state = stateData?.State ?? CircuitState.Closed;
		_lastKnownState = state;

		if (state == CircuitState.Open)
		{
			if (stateData != null && DateTimeOffset.UtcNow < stateData.OpenUntil)
			{
				// FR-116-1 / AC-116-3: translate Polly's BrokenCircuitException to the canonical exception;
				// never leak Polly internals to callers. Carry the RetryAfter hint (AC-116-7).
				var retryAfter = stateData.OpenUntil - DateTimeOffset.UtcNow;
				if (retryAfter < TimeSpan.Zero)
				{
					retryAfter = TimeSpan.Zero;
				}
				throw new CircuitBreakerOpenException(Name, retryAfter);
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

			// Fast path: ExecuteAsync already fetched the authoritative state above (and refreshed
			// _lastKnownState at :108, including any HalfOpen transition), so call the core directly
			// to avoid a redundant distributed-state read on the hot path. The manual
			// RecordSuccessAsync entry point does its own authoritative read (bd-0snskv).
			await RecordSuccessCoreAsync(_lastKnownState, cancellationToken).ConfigureAwait(false);
			return result;
		}
		catch (Exception ex) when (ex is not OperationCanceledException)
		{
			// FR-116-2: cancellation is not a failure — non-tripping.
			await RecordFailureAsync(cancellationToken, ex).ConfigureAwait(false);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RecordSuccessAsync(CancellationToken cancellationToken)
	{
		// Manual record path: refresh the authoritative circuit state from the distributed store
		// before delegating to the close-gate. Unlike ExecuteAsync (which refreshes _lastKnownState
		// from its single state fetch at the top), a direct caller never observes a cross-instance
		// HalfOpen transition, so without this read the close-gate would test a stale local field
		// and the circuit would never close on the manual path (bd-0snskv). ExecuteAsync preserves
		// its fast path by calling RecordSuccessCoreAsync directly with the state it already read.
		var authoritativeState = await GetStateAsync(cancellationToken).ConfigureAwait(false);
		await RecordSuccessCoreAsync(authoritativeState, cancellationToken).ConfigureAwait(false);
	}

	private async Task RecordSuccessCoreAsync(CircuitState authoritativeState, CancellationToken cancellationToken)
	{
		await _metricsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Keep the local cache consistent with the authoritative state used for the gate.
			_lastKnownState = authoritativeState;

			var metrics = await GetOrCreateMetricsAsync(cancellationToken).ConfigureAwait(false);

			metrics.SuccessCount++;
			metrics.ConsecutiveSuccesses++;
			metrics.ConsecutiveFailures = 0;
			metrics.LastSuccess = DateTimeOffset.UtcNow;

			// Count the success as an in-window attempt so the rolling failure ratio has a denominator.
			metrics.RecordWindow(failure: false, DateTimeOffset.UtcNow.UtcTicks, WindowBucketTicks, WindowBucketCount);

			await SaveMetricsAsync(metrics, cancellationToken).ConfigureAwait(false);

			// Check if we should close the circuit.
			// ConsecutiveSuccesses is incremented above and reset to 0 on any failure (RecordFailureAsync),
			// so this gate fires only after SuccessThresholdToClose *consecutive* successes while half-open.
			// The gate tests the authoritative store state (not a possibly-stale local field) so a
			// cross-instance HalfOpen transition is honored on every entry path (bd-0snskv).
			if (authoritativeState == CircuitState.HalfOpen && metrics.ConsecutiveSuccesses >= _options.SuccessThresholdToClose)
			{
				await TransitionToClosedAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
		{
			throw; // Never swallow cancellation
		}
#pragma warning disable CA1031 // Coordination errors should not crash the caller
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
#pragma warning restore CA1031
		finally
		{
			_metricsGate.Release();
		}
	}

	/// <inheritdoc />
	public async Task RecordFailureAsync(CancellationToken cancellationToken, Exception? exception = null)
	{
		await _metricsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
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

			// Count the failure as an in-window attempt AND an in-window failure.
			var nowTicks = DateTimeOffset.UtcNow.UtcTicks;
			metrics.RecordWindow(failure: true, nowTicks, WindowBucketTicks, WindowBucketCount);

			await SaveMetricsAsync(metrics, cancellationToken).ConfigureAwait(false);

			// Windowed open decision (Polly v8 semantics, zxb7fp): trip on the failure RATIO only once at
			// least MinimumThroughput attempts have accumulated within the SamplingDuration window, and
			// compare the ROLLING-WINDOW ratio (not a lifetime-cumulative one) to FailureRatio. The
			// ConsecutiveFailures fallback is retained so a hard burst still trips promptly. SamplingDuration
			// and MinimumThroughput are now genuinely wired (were advertised-but-unwired). Counts are bucketed
			// by wall-clock; minor inter-instance clock skew merely smears counts across adjacent buckets,
			// which is acceptable for a breaker heuristic (not a fencing/safety invariant).
			var (windowAttempts, windowFailureRatio) = metrics.GetWindow(nowTicks, WindowBucketTicks, WindowBucketCount);
			var windowedRatioTrips = windowAttempts >= _options.MinimumThroughput
				&& windowFailureRatio > _options.FailureRatio;
			if (windowedRatioTrips ||
				metrics.ConsecutiveFailures >= _options.ConsecutiveFailureThreshold)
			{
				LogThresholdExceeded(Name, (int)windowAttempts, _options.MinimumThroughput);
				await TransitionToOpenAsync(cancellationToken).ConfigureAwait(false);
			}
		}
		catch (Exception ex)
		{
			LogCoordinationError(Name, ex);
		}
		finally
		{
			_metricsGate.Release();
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
		await _shutdownCts.CancelAsync().ConfigureAwait(false);

		// Wait for tracked background tasks to complete (atomic drain)
		try
		{
			var finalTasks = Interlocked.Exchange(ref _backgroundTasks, new ConcurrentBag<Task>());
			await Task.WhenAll(finalTasks.ToArray()).ConfigureAwait(false);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown
		}

		await _syncTimer.DisposeAsync().ConfigureAwait(false);
		_shutdownCts.Dispose();
		_metricsGate.Dispose();
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

		// Atomic drain: swap the bag to avoid ToArray()+Clear() race
		var drained = Interlocked.Exchange(ref _backgroundTasks, new ConcurrentBag<Task>());
		foreach (var t in drained)
		{
			if (!t.IsCompleted)
			{
				_backgroundTasks.Add(t);
			}
		}
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
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			// Expected during shutdown -- swallow
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

	// Width of a single rolling-window bucket in ticks. Guards against a sub-tick width when
	// SamplingDuration.Ticks < WindowBucketCount.
	private long WindowBucketTicks => Math.Max(1L, _options.SamplingDuration.Ticks / WindowBucketCount);

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
