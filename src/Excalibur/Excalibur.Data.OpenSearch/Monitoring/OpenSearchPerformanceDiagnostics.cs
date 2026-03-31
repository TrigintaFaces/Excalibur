// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using OpenSearch.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.OpenSearch.Monitoring;

/// <summary>
/// Provides performance diagnostics and slow operation detection for OpenSearch operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="OpenSearchPerformanceDiagnostics" /> class.
/// </remarks>
/// <param name="logger"> The logger for performance diagnostics. </param>
/// <param name="options"> The monitoring configuration options. </param>
internal sealed class OpenSearchPerformanceDiagnostics(
	ILogger<OpenSearchPerformanceDiagnostics> logger,
	IOptions<OpenSearchMonitoringOptions> options)
{
	private readonly ILogger<OpenSearchPerformanceDiagnostics> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly PerformanceDiagnosticsOptions _settings =
		options?.Value?.Performance ?? throw new ArgumentNullException(nameof(options));

	#pragma warning disable CA5394 // Random used for non-security sampling rate, not cryptographic purposes
	private readonly Random _random = new();
#pragma warning restore CA5394
	private readonly ConcurrentDictionary<string, PerformanceMetrics> _operationMetrics = new(StringComparer.Ordinal);

	/// <summary>
	/// Starts performance monitoring for an operation.
	/// </summary>
	/// <param name="operationType"> The type of operation being monitored. </param>
	/// <param name="indexName"> The name of the index being operated on. </param>
	/// <returns> A performance context for tracking the operation. </returns>
	public PerformanceContext StartOperation(string operationType, string? indexName = null)
	{
		if (!_settings.Enabled)
		{
			return new PerformanceContext(this, operationType, indexName, enableSampling: false);
		}

		// Determine if this operation should be sampled
		#pragma warning disable CA5394
		var shouldSample = _random.NextDouble() < _settings.SamplingRate;
#pragma warning restore CA5394

		return new PerformanceContext(this, operationType, indexName, enableSampling: shouldSample);
	}

	/// <summary>
	/// Gets the current performance metrics for all operations.
	/// </summary>
	/// <returns> A dictionary of operation types and their performance metrics. </returns>
	public IReadOnlyDictionary<string, PerformanceMetrics> GetPerformanceMetrics() =>
		_operationMetrics.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value, StringComparer.Ordinal);

	/// <summary>
	/// Resets the performance metrics for all operations.
	/// </summary>
	public void ResetMetrics() => _operationMetrics.Clear();

	/// <summary>
	/// Analyzes the performance of a completed operation.
	/// </summary>
	internal void AnalyzePerformance(
		string operationType,
		TimeSpan duration,
		object? response,
		string? indexName,
		MemoryUsageInfo? memoryUsage)
	{
		if (!_settings.Enabled)
		{
			return;
		}

		try
		{
			// Update operation metrics
			var isValid = response != null;
			UpdateOperationMetrics(operationType, duration, isValid);

			// Check for slow operations
			if (duration >= _settings.SlowOperationThreshold)
			{
				LogSlowOperation(operationType, duration, response as IResponse, indexName, memoryUsage);
			}

			// Analyze query performance if enabled and response is available
			if (_settings.AnalyzeQueryPerformance && response != null)
			{
				AnalyzeQueryPerformance(operationType, response, duration, indexName);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to analyze performance for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Updates the performance metrics for an operation type.
	/// </summary>
	private void UpdateOperationMetrics(string operationType, TimeSpan duration, bool success) =>
		_operationMetrics.AddOrUpdate(
			operationType,
			static (key, state) => new PerformanceMetrics(key, state.duration, state.success),
			static (_, existing, state) => existing.UpdateWith(state.duration, state.success),
			(duration, success));

	/// <summary>
	/// Logs information about a slow operation.
	/// </summary>
	private void LogSlowOperation(
		string operationType,
		TimeSpan duration,
		IResponse? response,
		string? indexName,
		MemoryUsageInfo? memoryUsage)
	{
		try
		{
			var logData = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["DurationMs"] = duration.TotalMilliseconds,
				["ThresholdMs"] = _settings.SlowOperationThreshold.TotalMilliseconds,
				["Success"] = response?.ApiCall?.HttpStatusCode is >= 200 and < 300,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				logData["IndexName"] = indexName;
			}

			if (memoryUsage != null)
			{
				logData["MemoryUsage"] = new
				{
					memoryUsage.AllocatedBytes,
					memoryUsage.Gen0Collections,
					memoryUsage.Gen1Collections,
					memoryUsage.Gen2Collections,
				};
			}

			_logger.LogWarning(
				"Slow OpenSearch operation detected: {OperationType} took {DurationMs}ms (threshold: {ThresholdMs}ms) {@LogData}",
				operationType, duration.TotalMilliseconds, _settings.SlowOperationThreshold.TotalMilliseconds, logData);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to log slow operation for: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Analyzes query performance based on the response statistics.
	/// </summary>
	private void AnalyzeQueryPerformance(
		string operationType,
		object response,
		TimeSpan duration,
		string? indexName)
	{
		try
		{
			// TODO: OpenSearch API adaptation needed - OpenSearch.Client uses NEST-style
			// ISearchResponse<T> with different property access patterns than Elastic 8.x.
			// Adapt once concrete search response types are available in the consuming code.
			if (response is not IResponse osResponse || !osResponse.IsValid)
			{
				return;
			}

			var analysis = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["TotalDurationMs"] = duration.TotalMilliseconds,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				analysis["IndexName"] = indexName;
			}

			_logger.LogDebug(
				"OpenSearch query performance analysis for {OperationType} {@Analysis}",
				operationType, analysis);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to analyze query performance for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Represents performance metrics for an operation type.
	/// </summary>
	internal sealed class PerformanceMetrics(string operationType, TimeSpan initialDuration, bool initialSuccess)
	{
#if NET9_0_OR_GREATER
		private readonly System.Threading.Lock _lock = new();
#else
		private readonly object _lock = new();
#endif
		private long _totalOperations = 1;
		private long _successfulOperations = initialSuccess ? 1 : 0;
		private double _totalDurationMs = initialDuration.TotalMilliseconds;
		private double _minDurationMs = initialDuration.TotalMilliseconds;
		private double _maxDurationMs = initialDuration.TotalMilliseconds;

		/// <summary>
		/// Gets the operation type.
		/// </summary>
		public string OperationType { get; } = operationType;

		/// <summary>
		/// Gets the total number of operations.
		/// </summary>
		public long TotalOperations
		{
			get
			{
				lock (_lock)
				{
					return _totalOperations;
				}
			}
		}

		/// <summary>
		/// Gets the number of successful operations.
		/// </summary>
		public long SuccessfulOperations
		{
			get
			{
				lock (_lock)
				{
					return _successfulOperations;
				}
			}
		}

		/// <summary>
		/// Gets the success rate as a percentage.
		/// </summary>
		public double SuccessRate
		{
			get
			{
				lock (_lock)
				{
					return _totalOperations > 0 ? (double)_successfulOperations / _totalOperations * 100 : 0;
				}
			}
		}

		/// <summary>
		/// Gets the average operation duration in milliseconds.
		/// </summary>
		public double AverageDurationMs
		{
			get
			{
				lock (_lock)
				{
					return _totalOperations > 0 ? _totalDurationMs / _totalOperations : 0;
				}
			}
		}

		/// <summary>
		/// Gets the minimum operation duration in milliseconds.
		/// </summary>
		public double MinDurationMs
		{
			get
			{
				lock (_lock)
				{
					return _minDurationMs == double.MaxValue ? 0 : _minDurationMs;
				}
			}
		}

		/// <summary>
		/// Gets the maximum operation duration in milliseconds.
		/// </summary>
		public double MaxDurationMs
		{
			get
			{
				lock (_lock)
				{
					return _maxDurationMs;
				}
			}
		}

		/// <summary>
		/// Gets the timestamp when these metrics were last updated.
		/// </summary>
		public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;

		/// <summary>
		/// Updates the metrics with a new operation.
		/// </summary>
		internal PerformanceMetrics UpdateWith(TimeSpan duration, bool success)
		{
			lock (_lock)
			{
				_totalOperations++;
				if (success)
				{
					_successfulOperations++;
				}

				var durationMs = duration.TotalMilliseconds;
				_totalDurationMs += durationMs;
				_minDurationMs = Math.Min(_minDurationMs, durationMs);
				_maxDurationMs = Math.Max(_maxDurationMs, durationMs);

				LastUpdated = DateTimeOffset.UtcNow;
			}

			return this;
		}
	}

	/// <summary>
	/// Represents memory usage information for an operation.
	/// </summary>
	internal sealed class MemoryUsageInfo
	{
		/// <summary>
		/// Gets or sets the number of bytes allocated during the operation.
		/// </summary>
		public long AllocatedBytes { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 0 garbage collections during the operation.
		/// </summary>
		public int Gen0Collections { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 1 garbage collections during the operation.
		/// </summary>
		public int Gen1Collections { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 2 garbage collections during the operation.
		/// </summary>
		public int Gen2Collections { get; set; }
	}

	/// <summary>
	/// Represents the context for tracking performance of an individual operation.
	/// </summary>
	internal sealed class PerformanceContext : IDisposable
	{
		private readonly OpenSearchPerformanceDiagnostics _diagnostics;
		private readonly string _operationType;
		private readonly string? _indexName;
		private readonly bool _enableSampling;
		private readonly DateTimeOffset _startTime;
		private readonly MemoryUsageInfo? _startMemoryUsage;
		private volatile bool _disposed;

		internal PerformanceContext(
			OpenSearchPerformanceDiagnostics diagnostics,
			string operationType,
			string? indexName,
			bool enableSampling)
		{
			_diagnostics = diagnostics;
			_operationType = operationType;
			_indexName = indexName;
			_enableSampling = enableSampling;
			_startTime = DateTimeOffset.UtcNow;

			// Capture initial memory usage if tracking is enabled
			if (_enableSampling && _diagnostics._settings.TrackMemoryUsage)
			{
				_startMemoryUsage = new MemoryUsageInfo
				{
					AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false),
					Gen0Collections = GC.CollectionCount(0),
					Gen1Collections = GC.CollectionCount(1),
					Gen2Collections = GC.CollectionCount(2),
				};
			}
		}

		/// <summary>
		/// Completes the performance tracking for the operation.
		/// </summary>
		/// <param name="response"> The OpenSearch response. </param>
		public void Complete(IResponse? response = null)
		{
			if (_disposed || !_enableSampling)
			{
				return;
			}

			try
			{
				var duration = DateTimeOffset.UtcNow - _startTime;
				MemoryUsageInfo? memoryUsage = null;

				// Calculate memory usage if tracking is enabled
				if (_startMemoryUsage != null)
				{
					memoryUsage = new MemoryUsageInfo
					{
						AllocatedBytes = GC.GetTotalAllocatedBytes(precise: false) - _startMemoryUsage.AllocatedBytes,
						Gen0Collections = GC.CollectionCount(0) - _startMemoryUsage.Gen0Collections,
						Gen1Collections = GC.CollectionCount(1) - _startMemoryUsage.Gen1Collections,
						Gen2Collections = GC.CollectionCount(2) - _startMemoryUsage.Gen2Collections,
					};
				}

				_diagnostics.AnalyzePerformance(_operationType, duration, response, _indexName, memoryUsage);
			}
			catch (Exception ex)
			{
				// Log error but don't throw to avoid disrupting the main operation
				_diagnostics._logger.LogError(ex, "Failed to complete performance tracking for operation: {OperationType}", _operationType);
			}
		}

		/// <inheritdoc />
		public void Dispose()
		{
			if (!_disposed)
			{
				Complete();
				_disposed = true;
			}
		}
	}
}
