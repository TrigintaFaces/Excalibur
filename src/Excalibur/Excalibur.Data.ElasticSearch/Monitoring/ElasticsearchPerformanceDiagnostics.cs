// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Elastic.Clients.Elasticsearch;
using Elastic.Transport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.ElasticSearch.Monitoring;

/// <summary>
/// Provides performance diagnostics and slow operation detection for Elasticsearch operations.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="ElasticsearchPerformanceDiagnostics" /> class.
/// </remarks>
/// <param name="logger"> The logger for performance diagnostics. </param>
/// <param name="options"> The monitoring configuration options. </param>
public sealed class ElasticsearchPerformanceDiagnostics(
	ILogger<ElasticsearchPerformanceDiagnostics> logger,
	IOptions<ElasticsearchMonitoringOptions> options)
{
	private readonly ILogger<ElasticsearchPerformanceDiagnostics> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	private readonly PerformanceDiagnosticsOptions _settings =
		options?.Value?.Performance ?? throw new ArgumentNullException(nameof(options));

	private readonly Random _random = new();
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
		var shouldSample = _random.NextDouble() < _settings.SamplingRate;

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
	/// <param name="operationType"> The type of operation that completed. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <param name="indexName"> The name of the index that was operated on. </param>
	/// <param name="memoryUsage"> The memory usage during the operation (if tracking enabled). </param>
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
				LogSlowOperation(operationType, duration, response as TransportResponse, indexName, memoryUsage);
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
	/// Adds response-specific performance indicators to the log data.
	/// </summary>
	/// <param name="logData"> The log data dictionary to populate. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	private static void AddResponsePerformanceIndicators(Dictionary<string, object> logData, TransportResponse response)
	{
		try
		{
			switch (response)
			{
				case SearchResponse<object> { IsValidResponse: true } searchResponse:
					logData["ElasticsearchTookMs"] = searchResponse.Took;
					logData["HitsTotal"] = searchResponse.HitsMetadata?.Total?.Match(
						static totalHits => totalHits != null ? totalHits.Value : 0,
						static longValue => longValue) ?? 0;
					logData["TimedOut"] = searchResponse.TimedOut;
					if (searchResponse.Shards != null)
					{
						logData["ShardFailures"] = searchResponse.Shards.Failed;
					}

					break;

				case BulkResponse { IsValidResponse: true } bulkResponse:
					logData["ElasticsearchTookMs"] = bulkResponse.Took;
					logData["BulkItems"] = bulkResponse.Items.Count;
					logData["BulkErrors"] = bulkResponse.Errors;
					break;
				default:
					break;
			}
		}
		catch (Exception)
		{
			// Ignore errors in adding performance indicators
		}
	}

	/// <summary>
	/// Updates the performance metrics for an operation type.
	/// </summary>
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="success"> Whether the operation was successful. </param>
	private void UpdateOperationMetrics(string operationType, TimeSpan duration, bool success) =>
		_operationMetrics.AddOrUpdate(
			operationType,
			static (key, state) => new PerformanceMetrics(key, state.duration, state.success),
			static (_, existing, state) => existing.UpdateWith(state.duration, state.success),
			(duration, success));

	/// <summary>
	/// Logs information about a slow operation.
	/// </summary>
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="duration"> The operation duration. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <param name="indexName"> The name of the index. </param>
	/// <param name="memoryUsage"> The memory usage information. </param>
	private void LogSlowOperation(
		string operationType,
		TimeSpan duration,
		TransportResponse? response,
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
				["Success"] = response?.ApiCallDetails?.HttpStatusCode is >= 200 and < 300,
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

			// Add response-specific performance indicators
			if (response != null)
			{
				AddResponsePerformanceIndicators(logData, response);
			}

			_logger.LogWarning(
				"Slow Elasticsearch operation detected: {OperationType} took {DurationMs}ms (threshold: {ThresholdMs}ms) {@LogData}",
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
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="response"> The Elasticsearch response. </param>
	/// <param name="duration"> The total operation duration. </param>
	/// <param name="indexName"> The name of the index. </param>
	private void AnalyzeQueryPerformance(
		string operationType,
		object response,
		TimeSpan duration,
		string? indexName)
	{
		try
		{
			if (response is not SearchResponse<object> searchResponse || !searchResponse.IsValidResponse)
			{
				return;
			}

			var analysis = new Dictionary<string, object>
(StringComparer.Ordinal)
			{
				["OperationType"] = operationType,
				["TotalDurationMs"] = duration.TotalMilliseconds,
				["ElasticsearchTookMs"] = searchResponse.Took,
				["NetworkOverheadMs"] = duration.TotalMilliseconds - searchResponse.Took,
				["HitsTotal"] = searchResponse.HitsMetadata?.Total != null
					? searchResponse.HitsMetadata.Total.Match(
						static totalHits => (totalHits?.Value) ?? 0L,
						static longValue => longValue)
					: 0,
				["TimedOut"] = searchResponse.TimedOut,
				["Timestamp"] = DateTimeOffset.UtcNow,
			};

			if (!string.IsNullOrWhiteSpace(indexName))
			{
				analysis["IndexName"] = indexName;
			}

			// Analyze shard performance
			if (searchResponse.Shards != null)
			{
				var shardAnalysis = new
				{
					searchResponse.Shards.Total,
					searchResponse.Shards.Successful,
					searchResponse.Shards.Failed,
					SuccessRate = searchResponse.Shards.Total > 0
						? (double)searchResponse.Shards.Successful / searchResponse.Shards.Total
						: 0.0,
				};
				analysis["Shards"] = shardAnalysis;

				// Log warning if shard failures detected
				if (searchResponse.Shards.Failed > 0)
				{
					_logger.LogWarning(
						"Elasticsearch query had shard failures: {Failed}/{Total} shards failed for {OperationType} {@Analysis}",
						searchResponse.Shards.Failed, searchResponse.Shards.Total, operationType, analysis);
				}
			}

			// Check for performance issues
			var hasPerformanceIssues = false;
			var issues = new List<string>();

			// High network overhead
			var networkOverhead = duration.TotalMilliseconds - searchResponse.Took;
			if (networkOverhead > searchResponse.Took * 0.5) // Network overhead > 50% of ES processing time
			{
				issues.Add("high network overhead");
				hasPerformanceIssues = true;
			}

			// Query timeout
			if (searchResponse.TimedOut)
			{
				issues.Add("query timeout");
				hasPerformanceIssues = true;
			}

			// Inefficient query (high took time relative to results)
			var hitsCount = searchResponse.HitsMetadata?.Total != null
				? searchResponse.HitsMetadata.Total.Match(
					static totalHits => (totalHits?.Value) ?? 0L,
					static longValue => longValue)
				: 0;
			if (searchResponse.Took > 1000 && hitsCount < 100) // > 1 second for < 100 results
			{
				issues.Add("potentially inefficient query");
				hasPerformanceIssues = true;
			}

			if (hasPerformanceIssues)
			{
				analysis["PerformanceIssues"] = issues;
				_logger.LogInformation(
					"Elasticsearch query performance analysis for {OperationType}: {Issues} {@Analysis}",
					operationType, string.Join(", ", issues), analysis);
			}
			else
			{
				_logger.LogDebug(
					"Elasticsearch query performance analysis for {OperationType} {@Analysis}",
					operationType, analysis);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to analyze query performance for operation: {OperationType}", operationType);
		}
	}

	/// <summary>
	/// Represents performance metrics for an operation type.
	/// </summary>
	/// <remarks>
	/// Initializes a new instance of the <see cref="PerformanceMetrics" /> class.
	/// </remarks>
	/// <param name="operationType"> The type of operation. </param>
	/// <param name="initialDuration"> The initial operation duration. </param>
	/// <param name="initialSuccess"> Whether the initial operation was successful. </param>
	public sealed class PerformanceMetrics(string operationType, TimeSpan initialDuration, bool initialSuccess)
	{
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

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
		/// <value>
		/// The operation type.
		/// </value>
		public string OperationType { get; } = operationType;

		/// <summary>
		/// Gets the total number of operations.
		/// </summary>
		/// <value>
		/// The total number of operations.
		/// </value>
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
		/// <value>
		/// The number of successful operations.
		/// </value>
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
		/// <value>
		/// The success rate as a percentage.
		/// </value>
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
		/// <value>
		/// The average operation duration in milliseconds.
		/// </value>
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
		/// <value>
		/// The minimum operation duration in milliseconds.
		/// </value>
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
		/// <value>
		/// The maximum operation duration in milliseconds.
		/// </value>
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
		/// <value>
		/// The timestamp when these metrics were last updated.
		/// </value>
		public DateTimeOffset LastUpdated { get; private set; } = DateTimeOffset.UtcNow;

		/// <summary>
		/// Updates the metrics with a new operation.
		/// </summary>
		/// <param name="duration"> The operation duration. </param>
		/// <param name="success"> Whether the operation was successful. </param>
		/// <returns> The updated metrics instance. </returns>
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
	public sealed class MemoryUsageInfo
	{
		/// <summary>
		/// Gets or sets the number of bytes allocated during the operation.
		/// </summary>
		/// <value>
		/// The number of bytes allocated during the operation.
		/// </value>
		public long AllocatedBytes { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 0 garbage collections during the operation.
		/// </summary>
		/// <value>
		/// The number of Generation 0 garbage collections during the operation.
		/// </value>
		public int Gen0Collections { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 1 garbage collections during the operation.
		/// </summary>
		/// <value>
		/// The number of Generation 1 garbage collections during the operation.
		/// </value>
		public int Gen1Collections { get; set; }

		/// <summary>
		/// Gets or sets the number of Generation 2 garbage collections during the operation.
		/// </summary>
		/// <value>
		/// The number of Generation 2 garbage collections during the operation.
		/// </value>
		public int Gen2Collections { get; set; }
	}

	/// <summary>
	/// Represents the context for tracking performance of an individual operation.
	/// </summary>
	public sealed class PerformanceContext : IDisposable
	{
		private readonly ElasticsearchPerformanceDiagnostics _diagnostics;
		private readonly string _operationType;
		private readonly string? _indexName;
		private readonly bool _enableSampling;
		private readonly DateTimeOffset _startTime;
		private readonly MemoryUsageInfo? _startMemoryUsage;
		private volatile bool _disposed;

		internal PerformanceContext(
			ElasticsearchPerformanceDiagnostics diagnostics,
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
		/// <param name="response"> The Elasticsearch response. </param>
		public void Complete(TransportResponse? response = null)
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
