// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Resilience.Polly;

/// <summary>
/// Implementation of graceful degradation service.
/// </summary>
public partial class GracefulDegradationService : IGracefulDegradationService, IDisposable, IAsyncDisposable
{
	private readonly GracefulDegradationOptions _options;
	private readonly ILogger<GracefulDegradationService> _logger;
	private readonly System.Collections.Concurrent.ConcurrentDictionary<string, OperationStatistics> _operationStats;
	private readonly Timer _healthCheckTimer;
	private DateTimeOffset _lastLevelChange;
	private volatile string _lastChangeReason = "Initial";
	private volatile HealthMetrics _currentHealth;
	private volatile DegradationLevel _currentLevel;
	private volatile bool _disposed;

	// CPU delta tracking for real CPU usage calculation
	private TimeSpan _previousCpuTime;
	private DateTimeOffset _previousCpuSampleTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="GracefulDegradationService" /> class.
	/// </summary>
	/// <param name="options">The graceful degradation configuration options.</param>
	/// <param name="logger">The logger instance for logging degradation events.</param>
	public GracefulDegradationService(
		IOptions<GracefulDegradationOptions> options,
		ILogger<GracefulDegradationService> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_operationStats = new System.Collections.Concurrent.ConcurrentDictionary<string, OperationStatistics>(StringComparer.Ordinal);
		_currentLevel = DegradationLevel.Normal;
		_lastLevelChange = DateTimeOffset.UtcNow;
		_currentHealth = new HealthMetrics();

		// Initialize CPU tracking for delta-based calculation
		using var currentProcess = Process.GetCurrentProcess();
		_previousCpuTime = currentProcess.TotalProcessorTime;
		_previousCpuSampleTime = DateTimeOffset.UtcNow;

		// Start health monitoring
		_healthCheckTimer = new Timer(
			CheckSystemHealth,
			state: null,
			TimeSpan.Zero,
			_options.HealthCheckInterval);
	}

	/// <inheritdoc />
	public DegradationLevel CurrentLevel
	{
		get => _currentLevel;
		private set => _currentLevel = value;
	}

	/// <inheritdoc />
	public async Task<T> ExecuteWithDegradationAsync<T>(
		DegradationContext<T> context,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(context);

		// Check if operation should be rejected based on current level
		if (ShouldRejectOperation(context))
		{
			LogOperationRejected(context.OperationName);
			throw new DegradationRejectedException(
				$"Operation '{context.OperationName}' rejected at degradation level {CurrentLevel}");
		}

		// Track operation
		var stats = _operationStats.GetOrAdd(context.OperationName, _ => new OperationStatistics());
		stats.RecordAttempt();

		try
		{
			return await TryExecutePrimaryOrFallbackAsync(context, stats).ConfigureAwait(false);
		}
		catch
		{
			stats.RecordFailure();
			throw;
		}
	}

	/// <inheritdoc />
	public void SetLevel(DegradationLevel level, string reason)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(reason);

		if (CurrentLevel != level)
		{
			var previousLevel = CurrentLevel;
			CurrentLevel = level;
			_lastLevelChange = DateTimeOffset.UtcNow;
			_lastChangeReason = reason;

			LogLevelChanged(level, reason);

			// Notify observers if configured
			OnLevelChanged(previousLevel, level, reason);
		}
	}

	/// <inheritdoc />
	public DegradationMetrics GetMetrics()
	{
		var stats = _operationStats.ToDictionary(
			static pair => pair.Key,
			static pair => pair.Value.Clone(),
			StringComparer.Ordinal);

		var total = stats.Sum(static entry => entry.Value.TotalAttempts);
		var successes = stats.Sum(static entry => entry.Value.Successes);

		return new DegradationMetrics
		{
			CurrentLevel = CurrentLevel,
			LastLevelChange = _lastLevelChange,
			LastChangeReason = _lastChangeReason,
			OperationStatistics = stats,
			HealthMetrics = _currentHealth,
			TotalOperations = total,
			TotalFallbacks = stats.Sum(static s => s.Value.FallbackExecutions),
			SuccessRate = total > 0 ? (double)successes / total : 1.0,
		};
	}

	/// <summary>
	/// Disposes the service.
	/// </summary>
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases managed resources.
	/// </summary>
	/// <param name="disposing">Indicates whether managed resources should be released.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_healthCheckTimer.Dispose();
		}

		_disposed = true;
	}

	/// <summary>
	/// Asynchronously releases resources, ensuring the health check timer callback has completed.
	/// </summary>
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// DisposeAsync on Timer waits for any in-flight callback to complete
		await _healthCheckTimer.DisposeAsync().ConfigureAwait(false);

		GC.SuppressFinalize(this);
	}

	private async Task<T> TryExecutePrimaryOrFallbackAsync<T>(
		DegradationContext<T> context,
		OperationStatistics stats)
	{
		// Run primary operation when at Normal level or when the operation is critical
		if (CurrentLevel == DegradationLevel.Normal || context.IsCritical)
		{
			try
			{
				var result = await context.PrimaryOperation().ConfigureAwait(false);
				stats.RecordSuccess();
				return result;
			}
			catch (Exception ex)
			{
				stats.RecordFailure();
				LogPrimaryOperationFailed(ex, context.OperationName);

				if (context.Fallbacks.Count == 0)
				{
					throw;
				}
			}
		}

		return await ExecuteFallbackAsync(context, stats).ConfigureAwait(false);
	}

	private async Task<T> ExecuteFallbackAsync<T>(
		DegradationContext<T> context,
		OperationStatistics stats)
	{
		if (context.Fallbacks.TryGetValue(CurrentLevel, out var fallback))
		{
			LogFallbackExecuted(context.OperationName, CurrentLevel);
			stats.RecordFallback();

			var result = await fallback().ConfigureAwait(false);
			stats.RecordSuccess();
			return result;
		}

		foreach (var level in Enum.GetValues<DegradationLevel>().Where(l => l > CurrentLevel).Order())
		{
			if (context.Fallbacks.TryGetValue(level, out fallback))
			{
				LogFallbackExecuted(context.OperationName, level);
				stats.RecordFallback();

				var result = await fallback().ConfigureAwait(false);
				stats.RecordSuccess();
				return result;
			}
		}

		throw new NoFallbackAvailableException(
			$"No fallback available for operation '{context.OperationName}' at level {CurrentLevel}");
	}

	private bool ShouldRejectOperation<T>(DegradationContext<T> context)
	{
		// Critical operations are never rejected
		if (context.IsCritical)
		{
			return false;
		}

		// Apply rejection rules based on level and priority
		return CurrentLevel switch
		{
			DegradationLevel.Normal => false,
			DegradationLevel.Emergency => !context.IsCritical,
			_ => context.Priority < _options.GetPriorityThreshold(CurrentLevel),
		};
	}

	private void CheckSystemHealth(object? state)
	{
		try
		{
			// Collect health metrics
			var health = CollectHealthMetrics();
			_currentHealth = health;

			LogHealthMetricsUpdated(
				health.CpuUsagePercent,
				health.MemoryUsagePercent,
				health.ErrorRate * 100);

			// Determine appropriate level based on health
			var recommendedLevel = DetermineLevel(health);

			// Auto-adjust if enabled
			if (_options.EnableAutoAdjustment && recommendedLevel != CurrentLevel)
			{
				SetLevel(recommendedLevel, "Auto-adjusted based on health metrics");
			}
		}
		catch (Exception ex)
		{
			LogHealthCheckError(ex);
		}
	}

	private HealthMetrics CollectHealthMetrics()
	{
		// Calculate error rate from operation statistics
		var totalOps = _operationStats.Sum(static entry => entry.Value.TotalAttempts);
		var failures = _operationStats.Sum(static entry => entry.Value.Failures);
		var errorRate = totalOps > 0 ? (double)failures / totalOps : 0;

		// Real memory metric: Environment.WorkingSet (bytes -> percentage of 2GB baseline)
		var workingSetBytes = Environment.WorkingSet;
		const long assumedMaxBytes = 2_147_483_648L;
		var memoryUsage = (double)workingSetBytes / assumedMaxBytes * 100;

		// Real CPU metric: Process.TotalProcessorTime delta calculation
		double cpuUsage;
		try
		{
			using var currentProcess = Process.GetCurrentProcess();
			var currentCpuTime = currentProcess.TotalProcessorTime;
			var now = DateTimeOffset.UtcNow;
			var elapsed = now - _previousCpuSampleTime;

			if (elapsed.TotalMilliseconds > 0)
			{
				var cpuDelta = (currentCpuTime - _previousCpuTime).TotalMilliseconds;
				cpuUsage = cpuDelta / (elapsed.TotalMilliseconds * Environment.ProcessorCount) * 100;
				cpuUsage = Math.Clamp(cpuUsage, 0, 100);
			}
			else
			{
				cpuUsage = 0;
			}

			_previousCpuTime = currentCpuTime;
			_previousCpuSampleTime = now;
		}
		catch (Exception)
		{
			// Fallback if Process metrics are unavailable (e.g., sandboxed environments)
			cpuUsage = 0;
		}

		return new HealthMetrics
		{
			CpuUsagePercent = cpuUsage,
			MemoryUsagePercent = memoryUsage,
			ErrorRate = errorRate,
			ResponseTimeMs = 0,
			ActiveConnections = 0,
			Timestamp = DateTimeOffset.UtcNow,
		};
	}

	private DegradationLevel DetermineLevel(HealthMetrics health)
	{
		// Check levels from most severe to least severe
		DegradationLevel[] levelsDescending =
		[
			DegradationLevel.Emergency,
			DegradationLevel.Severe,
			DegradationLevel.Major,
			DegradationLevel.Moderate,
			DegradationLevel.Minor,
		];

		foreach (var level in levelsDescending)
		{
			if (health.ErrorRate > _options.GetErrorRateThreshold(level) ||
				health.CpuUsagePercent > _options.GetCpuThreshold(level) ||
				health.MemoryUsagePercent > _options.GetMemoryThreshold(level))
			{
				return level;
			}
		}

		return DegradationLevel.Normal;
	}

	partial void OnLevelChanged(DegradationLevel previousLevel, DegradationLevel newLevel, string reason);

	// Source-generated logging methods
	[LoggerMessage(ResilienceEventId.DegradationLevelChanged, LogLevel.Warning,
		"Degradation level changed to {Level}. Reason: {Reason}")]
	private partial void LogLevelChanged(DegradationLevel level, string reason);

	[LoggerMessage(ResilienceEventId.FallbackExecuted, LogLevel.Warning,
		"Fallback executed for operation '{Operation}' at level {Level}")]
	private partial void LogFallbackExecuted(string operation, DegradationLevel level);

	[LoggerMessage(ResilienceEventId.DegradationOperationRejected, LogLevel.Error,
		"Operation '{Operation}' rejected due to degradation policy")]
	private partial void LogOperationRejected(string operation);

	[LoggerMessage(ResilienceEventId.DegradationHealthMetricsUpdated, LogLevel.Debug,
		"Health metrics updated - CPU: {Cpu}%, Memory: {Memory}%, Error Rate: {ErrorRate}%")]
	private partial void LogHealthMetricsUpdated(double cpu, double memory, double errorRate);

	[LoggerMessage(ResilienceEventId.DegradationPrimaryOperationFailed, LogLevel.Error,
		"Primary operation '{Operation}' failed")]
	private partial void LogPrimaryOperationFailed(Exception ex, string operation);

	[LoggerMessage(ResilienceEventId.DegradationHealthCheckError, LogLevel.Error,
		"Error during health check")]
	private partial void LogHealthCheckError(Exception ex);
}
