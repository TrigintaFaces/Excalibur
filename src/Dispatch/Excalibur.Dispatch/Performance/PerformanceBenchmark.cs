// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


// R0.8: Random.Shared is acceptable for performance benchmarking, not security
#pragma warning disable CA5394

using Excalibur.Dispatch.Abstractions.Diagnostics;
using Excalibur.Dispatch.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Utility for benchmarking Excalibur framework performance and validating optimizations.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PerformanceBenchmark" /> class. </remarks>
/// <param name="metricsCollector"> The metrics collector to use for benchmarking. </param>
/// <param name="logger"> Logger for benchmark results. </param>
public sealed partial class PerformanceBenchmark(IPerformanceMetricsCollector metricsCollector, ILogger<PerformanceBenchmark> logger)
{
	private readonly IPerformanceMetricsCollector _metricsCollector =
		metricsCollector ?? throw new ArgumentNullException(nameof(metricsCollector));

	private readonly ILogger<PerformanceBenchmark> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <summary>
	/// Runs a comprehensive benchmark of the Dispatch pipeline performance.
	/// </summary>
	/// <param name="iterations"> Number of iterations to run for each test. </param>
	/// <param name="cancellationToken"> Cancellation token for the benchmark. </param>
	/// <returns> Comprehensive benchmark results. </returns>
	public async Task<BenchmarkResults> RunComprehensiveBenchmarkAsync(
		CancellationToken cancellationToken,
		int iterations = 10000)
	{
		ArgumentOutOfRangeException.ThrowIfLessThan(iterations, 1);

		LogBenchmarkStarting(iterations);

		var results = new BenchmarkResults { TestDate = DateTimeOffset.UtcNow, Iterations = iterations };

		// Reset metrics before starting
		_metricsCollector.Reset();

		var stopwatch = ValueStopwatch.StartNew();

		// Simulate middleware pipeline execution
		await SimulateMiddlewarePipelineAsync(iterations, cancellationToken).ConfigureAwait(false);

		// Simulate batch processing
		await SimulateBatchProcessingAsync(iterations / 100, cancellationToken).ConfigureAwait(false); // Run fewer batch operations

		// Simulate handler lookups
		await SimulateHandlerLookupsAsync(iterations * 2, cancellationToken).ConfigureAwait(false); // More lookups than messages

		// Simulate queue operations
		await SimulateQueueOperationsAsync(iterations, cancellationToken).ConfigureAwait(false);

		results.TotalDuration = stopwatch.Elapsed;

		// Get final metrics snapshot
		var snapshot = _metricsCollector.GetSnapshot();
		results.PerformanceSnapshot = snapshot;

		// Calculate derived metrics
		results.MessagesPerSecond = iterations / stopwatch.Elapsed.TotalSeconds;
		results.AverageLatencyMs = stopwatch.Elapsed.TotalMilliseconds / iterations;

		if (results.PerformanceSnapshot != null)
		{
			LogBenchmarkResults(results);
		}

		return results;
	}

	/// <summary>
	/// Simulates middleware pipeline execution for benchmarking.
	/// </summary>
	private async Task SimulateMiddlewarePipelineAsync(int iterations, CancellationToken cancellationToken)
	{
		for (var i = 0; i < iterations; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
			var executionTime = TimeSpan.FromMilliseconds(Random.Shared.NextDouble() * 10); // 0-10ms
			var middlewareCount = Random.Shared.Next(3, 8); // 3-7 middleware
			var memoryAllocated = Random.Shared.NextInt64(1024, 8192); // 1-8KB
#pragma warning restore CA5394

			_metricsCollector.RecordPipelineExecution(middlewareCount, executionTime, memoryAllocated);
			_metricsCollector.RecordMiddlewareExecution("ValidationMiddleware", executionTime / 3);
			_metricsCollector.RecordMiddlewareExecution("AuthorizationMiddleware", executionTime / 4);
			// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
			_metricsCollector.RecordMiddlewareExecution("CachingMiddleware", executionTime / 5, Random.Shared.NextDouble() > 0.1);
#pragma warning restore CA5394

			// Small delay to prevent CPU saturation
			if (i % 1000 == 0)
			{
				await Task.Delay(1, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Simulates batch processing for benchmarking.
	/// </summary>
	private async Task SimulateBatchProcessingAsync(int batchCount, CancellationToken cancellationToken)
	{
		for (var i = 0; i < batchCount; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
			var batchSize = Random.Shared.Next(50, 200); // 50-200 items per batch
			var processingTime = TimeSpan.FromMilliseconds(batchSize * Random.Shared.NextDouble() * 2); // ~2ms per item
			var parallelDegree = Random.Shared.Next(2, Environment.ProcessorCount);
			var successCount = Random.Shared.Next((int)(batchSize * 0.9), batchSize); // 90-100% success
#pragma warning restore CA5394
			var failureCount = batchSize - successCount;

			_metricsCollector.RecordBatchProcessing("InboxProcessor", batchSize, processingTime,
				parallelDegree, successCount, failureCount);

			if (i % 10 == 0)
			{
				await Task.Delay(1, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Simulates handler lookups for benchmarking.
	/// </summary>
	private async Task SimulateHandlerLookupsAsync(int lookups, CancellationToken cancellationToken)
	{
		var messageTypes = new[]
		{
			"OrderCreatedEvent", "PaymentProcessedEvent", "UserRegisteredEvent", "InventoryUpdatedEvent", "OrderShippedEvent",
		};

		for (var i = 0; i < lookups; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
			var messageType = messageTypes[Random.Shared.Next(messageTypes.Length)];
			var lookupTime = TimeSpan.FromTicks(Random.Shared.NextInt64(1000, 50000)); // Very fast lookups (0.1-5ms)
			var handlersFound = Random.Shared.Next(1, 4); // 1-3 handlers typically
#pragma warning restore CA5394

			_metricsCollector.RecordHandlerLookup(messageType, lookupTime, handlersFound);

			if (i % 2000 == 0)
			{
				await Task.Delay(1, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Simulates queue operations for benchmarking.
	/// </summary>
	private async Task SimulateQueueOperationsAsync(int operations, CancellationToken cancellationToken)
	{
		var queueNames = new[] { "orders", "payments", "notifications", "events" };
		var operationTypes = new[] { "enqueue", "dequeue", "peek" };

		// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
		var queueDepths = queueNames.ToDictionary(static q => q, static _ => Random.Shared.Next(10, 100), StringComparer.Ordinal);
#pragma warning restore CA5394

		for (var i = 0; i < operations; i++)
		{
			cancellationToken.ThrowIfCancellationRequested();

			// R0.8: Use secure random number generator for performance test data generation
#pragma warning disable CA5394
			var queueName = queueNames[Random.Shared.Next(queueNames.Length)];
			var operation = operationTypes[Random.Shared.Next(operationTypes.Length)];
			var itemCount = string.Equals(operation, "dequeue", StringComparison.Ordinal) ? Random.Shared.Next(1, 10) : 1;
			var duration = TimeSpan.FromTicks(Random.Shared.NextInt64(5000, 100000)); // 0.5-10ms
#pragma warning restore CA5394

			// Update simulated queue depth
			if (string.Equals(operation, "enqueue", StringComparison.Ordinal))
			{
				queueDepths[queueName] += itemCount;
			}
			else if (string.Equals(operation, "dequeue", StringComparison.Ordinal))
			{
				queueDepths[queueName] = Math.Max(0, queueDepths[queueName] - itemCount);
			}

			_metricsCollector.RecordQueueOperation(queueName, operation, itemCount, duration, queueDepths[queueName]);

			if (i % 1000 == 0)
			{
				await Task.Delay(1, cancellationToken).ConfigureAwait(false);
			}
		}
	}

	/// <summary>
	/// Logs comprehensive benchmark results.
	/// </summary>
	private void LogBenchmarkResults(BenchmarkResults results)
	{
		LogBenchmarkCompleted();
		LogTotalDuration(results.TotalDuration.TotalMilliseconds);
		LogMessagesPerSecond(results.MessagesPerSecond);
		LogAverageLatency(results.AverageLatencyMs);

		var snapshot = results.PerformanceSnapshot;
		if (snapshot == null)
		{
			return;
		}

		LogPipelinePerformance();
		LogTotalExecutions(snapshot.PipelineMetrics.TotalExecutions);
		LogAverageDuration(snapshot.PipelineMetrics.AverageDuration.TotalMilliseconds);
		LogAverageMiddlewareCount(snapshot.PipelineMetrics.AverageMiddlewareCount);
		LogMemoryPerExecution(snapshot.PipelineMetrics.AverageMemoryPerExecution);

		if (snapshot.BatchMetrics.Any())
		{
			foreach (var (processor, metrics) in snapshot.BatchMetrics)
			{
				LogBatchProcessing(processor);
				LogThroughput(metrics.ThroughputItemsPerSecond);
				LogSuccessRate(metrics.OverallSuccessRate);
				LogAverageParallelDegree(metrics.AverageParallelDegree);
			}
		}

		LogHandlerRegistry();
		LogCacheHitRate(snapshot.HandlerMetrics.CacheHitRate);
		LogAverageLookupTime(snapshot.HandlerMetrics.AverageLookupTime.TotalMilliseconds);
		LogHandlersPerLookup(snapshot.HandlerMetrics.AverageHandlersPerLookup);
	}

	// Source-generated logging methods
	[LoggerMessage(PerformanceEventId.BenchmarkStarting, LogLevel.Information,
		"Starting comprehensive performance benchmark with {Iterations} iterations")]
	private partial void LogBenchmarkStarting(int iterations);

	[LoggerMessage(PerformanceEventId.BenchmarkCompleted, LogLevel.Information,
		"Performance Benchmark Completed:")]
	private partial void LogBenchmarkCompleted();

	[LoggerMessage(PerformanceEventId.BenchmarkTotalDuration, LogLevel.Information,
		"  Total Duration: {Duration:F2}ms")]
	private partial void LogTotalDuration(double duration);

	[LoggerMessage(PerformanceEventId.BenchmarkMessagesPerSecond, LogLevel.Information,
		"  Messages/Second: {MessagesPerSecond:F0}")]
	private partial void LogMessagesPerSecond(double messagesPerSecond);

	[LoggerMessage(PerformanceEventId.BenchmarkAverageLatency, LogLevel.Information,
		"  Average Latency: {LatencyMs:F3}ms")]
	private partial void LogAverageLatency(double latencyMs);

	[LoggerMessage(PerformanceEventId.PipelinePerformanceHeader, LogLevel.Information,
		"Pipeline Performance:")]
	private partial void LogPipelinePerformance();

	[LoggerMessage(PerformanceEventId.PipelineTotalExecutions, LogLevel.Information,
		"  Total Executions: {Executions}")]
	private partial void LogTotalExecutions(int executions);

	[LoggerMessage(PerformanceEventId.PipelineAverageDuration, LogLevel.Information,
		"  Average Duration: {Duration:F3}ms")]
	private partial void LogAverageDuration(double duration);

	[LoggerMessage(PerformanceEventId.PipelineAverageMiddlewareCount, LogLevel.Information,
		"  Average Middleware Count: {Count:F1}")]
	private partial void LogAverageMiddlewareCount(double count);

	[LoggerMessage(PerformanceEventId.PipelineMemoryPerExecution, LogLevel.Information,
		"  Memory per Execution: {Memory} bytes")]
	private partial void LogMemoryPerExecution(long memory);

	[LoggerMessage(PerformanceEventId.BatchProcessingHeader, LogLevel.Information,
		"Batch Processing ({Processor}):")]
	private partial void LogBatchProcessing(string processor);

	[LoggerMessage(PerformanceEventId.BatchThroughput, LogLevel.Information,
		"  Throughput: {Throughput:F0} items/second")]
	private partial void LogThroughput(double throughput);

	[LoggerMessage(PerformanceEventId.BatchSuccessRate, LogLevel.Information,
		"  Success Rate: {SuccessRate:P1}")]
	private partial void LogSuccessRate(double successRate);

	[LoggerMessage(PerformanceEventId.BatchAverageParallelDegree, LogLevel.Information,
		"  Avg Parallel Degree: {ParallelDegree:F1}")]
	private partial void LogAverageParallelDegree(double parallelDegree);

	[LoggerMessage(PerformanceEventId.HandlerRegistryHeader, LogLevel.Information,
		"Handler Registry:")]
	private partial void LogHandlerRegistry();

	[LoggerMessage(PerformanceEventId.HandlerCacheHitRate, LogLevel.Information,
		"  Cache Hit Rate: {HitRate:P1}")]
	private partial void LogCacheHitRate(double hitRate);

	[LoggerMessage(PerformanceEventId.HandlerAverageLookupTime, LogLevel.Information,
		"  Average Lookup Time: {Time:F3}ms")]
	private partial void LogAverageLookupTime(double time);

	[LoggerMessage(PerformanceEventId.HandlersPerLookup, LogLevel.Information,
		"  Handlers per Lookup: {Count:F1}")]
	private partial void LogHandlersPerLookup(double count);
}
