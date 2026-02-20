// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Excalibur.Dispatch.Performance;

/// <summary>
/// Aggregates and exposes performance metrics collected across the Dispatch framework.
/// </summary>
public sealed class PerformanceMetricsCollector : IPerformanceMetricsCollector, IDisposable
{
	private readonly ConcurrentDictionary<string, ComponentMetricsData> _middlewareMetrics = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, BatchProcessingMetricsData> _batchMetrics = new(StringComparer.Ordinal);
	private readonly ConcurrentDictionary<string, QueueMetricsData> _queueMetrics = new(StringComparer.Ordinal);
#if NET9_0_OR_GREATER

	private readonly Lock _pipelineMetricsLock = new();

#else

	private readonly object _pipelineMetricsLock = new();

#endif
#if NET9_0_OR_GREATER

	private readonly Lock _handlerMetricsLock = new();

#else

	private readonly object _handlerMetricsLock = new();

#endif

	private PipelineMetricsData _pipelineMetrics = PipelineMetricsData.Empty;
	private HandlerRegistryMetricsData _handlerMetrics = HandlerRegistryMetricsData.Empty;
	private volatile bool _disposed;

	/// <inheritdoc />
	public void RecordMiddlewareExecution(string middlewareName, TimeSpan duration, bool success = true)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(middlewareName);

		_ = _middlewareMetrics.AddOrUpdate(
			middlewareName,
			(key, state) =>
			{
				_ = key;
				return new ComponentMetricsData(state.duration, state.success);
			},
			(key, existing, state) =>
			{
				_ = key;
				return existing.RecordExecution(state.duration, state.success);
			},
			(duration, success));
	}

	/// <inheritdoc />
	public void RecordPipelineExecution(int middlewareCount, TimeSpan totalDuration, long memoryAllocated = 0)
	{
		ThrowIfDisposed();
		ArgumentOutOfRangeException.ThrowIfNegative(middlewareCount);
		ArgumentOutOfRangeException.ThrowIfNegative(memoryAllocated);

		lock (_pipelineMetricsLock)
		{
			_pipelineMetrics = _pipelineMetrics.RecordExecution(middlewareCount, totalDuration, memoryAllocated);
		}
	}

	/// <inheritdoc />
	public void RecordBatchProcessing(string processorType, int batchSize, TimeSpan processingTime, int parallelDegree, int successCount,
		int failureCount)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(processorType);
		ArgumentOutOfRangeException.ThrowIfNegative(batchSize);
		ArgumentOutOfRangeException.ThrowIfNegative(parallelDegree);
		ArgumentOutOfRangeException.ThrowIfNegative(successCount);
		ArgumentOutOfRangeException.ThrowIfNegative(failureCount);

		_ = _batchMetrics.AddOrUpdate(
			processorType,
			(key, state) =>
			{
				_ = key;
				return new BatchProcessingMetricsData(state.batchSize, state.processingTime, state.parallelDegree, state.successCount,
					state.failureCount);
			},
			(key, existing, state) =>
			{
				_ = key;
				return existing.RecordBatch(state.batchSize, state.processingTime, state.parallelDegree, state.successCount,
					state.failureCount);
			},
			(batchSize, processingTime, parallelDegree, successCount, failureCount));
	}

	/// <inheritdoc />
	public void RecordHandlerLookup(string messageType, TimeSpan lookupTime, int handlersFound)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(messageType);
		ArgumentOutOfRangeException.ThrowIfNegative(handlersFound);

		var isCacheHit = lookupTime < TimeSpan.FromMilliseconds(1);

		lock (_handlerMetricsLock)
		{
			_handlerMetrics = _handlerMetrics.RecordLookup(lookupTime, handlersFound, isCacheHit);
		}
	}

	/// <inheritdoc />
	public void RecordQueueOperation(string queueName, string operation, int itemCount, TimeSpan duration, int queueDepth)
	{
		ThrowIfDisposed();
		ArgumentException.ThrowIfNullOrWhiteSpace(queueName);
		ArgumentException.ThrowIfNullOrWhiteSpace(operation);
		ArgumentOutOfRangeException.ThrowIfNegative(itemCount);
		ArgumentOutOfRangeException.ThrowIfNegative(queueDepth);

		_ = _queueMetrics.AddOrUpdate(
			queueName,
			(key, state) =>
			{
				_ = key;
				return new QueueMetricsData(state.operation, state.itemCount, state.duration, state.queueDepth);
			},
			(key, existing, state) =>
			{
				_ = key;
				return existing.RecordOperation(state.operation, state.itemCount, state.duration, state.queueDepth);
			},
			(operation, itemCount, duration, queueDepth));
	}

	/// <inheritdoc />
	public PerformanceSnapshot GetSnapshot()
	{
		ThrowIfDisposed();

		var middlewareSnapshot = _middlewareMetrics.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToComponentMetrics(), StringComparer.Ordinal);
		var batchSnapshot = _batchMetrics.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToBatchProcessingMetrics(), StringComparer.Ordinal);
		var queueSnapshot = _queueMetrics.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToQueueMetrics(), StringComparer.Ordinal);

		PipelineMetrics pipelineMetrics;
		HandlerRegistryMetrics handlerMetrics;

		lock (_pipelineMetricsLock)
		{
			pipelineMetrics = _pipelineMetrics.ToPipelineMetrics();
		}

		lock (_handlerMetricsLock)
		{
			handlerMetrics = _handlerMetrics.ToHandlerRegistryMetrics();
		}

		return new PerformanceSnapshot
		{
			MiddlewareMetrics = middlewareSnapshot,
			PipelineMetrics = pipelineMetrics,
			BatchMetrics = batchSnapshot,
			QueueMetrics = queueSnapshot,
			HandlerMetrics = handlerMetrics,
			Timestamp = DateTimeOffset.UtcNow,
		};
	}

	/// <inheritdoc />
	public void Reset()
	{
		ThrowIfDisposed();

		_middlewareMetrics.Clear();
		_batchMetrics.Clear();
		_queueMetrics.Clear();

		lock (_pipelineMetricsLock)
		{
			_pipelineMetrics = PipelineMetricsData.Empty;
		}

		lock (_handlerMetricsLock)
		{
			_handlerMetrics = HandlerRegistryMetricsData.Empty;
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_middlewareMetrics.Clear();
		_batchMetrics.Clear();
		_queueMetrics.Clear();

		_disposed = true;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	/// <summary>
	/// Immutable metrics for a single component type.
	/// </summary>
	private sealed record ComponentMetricsData
	{
		public ComponentMetricsData()
		{
			MaxDuration = TimeSpan.Zero;
		}

		public ComponentMetricsData(TimeSpan duration, bool success)
		{
			ExecutionCount = 1;
			TotalDuration = duration;
			MinDuration = duration;
			MaxDuration = duration;
			SuccessCount = success ? 1 : 0;
			FailureCount = success ? 0 : 1;
		}

		public int ExecutionCount { get; init; }

		public TimeSpan TotalDuration { get; init; }

		public TimeSpan MinDuration { get; init; } = TimeSpan.MaxValue;

		public TimeSpan MaxDuration { get; init; }

		public int SuccessCount { get; init; }

		public int FailureCount { get; init; }

		public ComponentMetricsData RecordExecution(TimeSpan duration, bool success) => this with
		{
			ExecutionCount = ExecutionCount + 1,
			TotalDuration = TotalDuration + duration,
			MinDuration = duration < MinDuration ? duration : MinDuration,
			MaxDuration = duration > MaxDuration ? duration : MaxDuration,
			SuccessCount = SuccessCount + (success ? 1 : 0),
			FailureCount = FailureCount + (success ? 0 : 1),
		};

		public ComponentMetrics ToComponentMetrics()
		{
			var averageDuration = ExecutionCount > 0 ? TotalDuration / ExecutionCount : TimeSpan.Zero;
			var successRate = ExecutionCount > 0 ? (double)SuccessCount / ExecutionCount : 0.0;

			return new ComponentMetrics
			{
				ExecutionCount = ExecutionCount,
				TotalDuration = TotalDuration,
				AverageDuration = averageDuration,
				MinDuration = MinDuration == TimeSpan.MaxValue ? TimeSpan.Zero : MinDuration,
				MaxDuration = MaxDuration,
				SuccessCount = SuccessCount,
				FailureCount = FailureCount,
				SuccessRate = successRate,
			};
		}
	}

	/// <summary>
	/// Immutable metrics for pipeline execution.
	/// </summary>
	/// <param name="TotalExecutions"> The total number of pipeline executions recorded. </param>
	/// <param name="TotalDuration"> The cumulative duration of all executions. </param>
	/// <param name="TotalMiddlewareCount"> The total number of middleware invocations across executions. </param>
	/// <param name="TotalMemoryAllocated"> The total memory allocated across executions. </param>
	private sealed record PipelineMetricsData(
		int TotalExecutions,
		TimeSpan TotalDuration,
		int TotalMiddlewareCount,
		long TotalMemoryAllocated)
	{
		public static PipelineMetricsData Empty { get; } = new(0, TimeSpan.Zero, 0, 0);

		public PipelineMetricsData RecordExecution(int middlewareCount, TimeSpan duration, long memoryAllocated) => new(
			TotalExecutions + 1,
			TotalDuration + duration,
			TotalMiddlewareCount + middlewareCount,
			TotalMemoryAllocated + memoryAllocated);

		public PipelineMetrics ToPipelineMetrics()
		{
			var averageDuration = TotalExecutions > 0 ? TotalDuration / TotalExecutions : TimeSpan.Zero;
			var averageMiddlewareCount = TotalExecutions > 0 ? (double)TotalMiddlewareCount / TotalExecutions : 0.0;
			var averageMemoryPerExecution = TotalExecutions > 0 ? TotalMemoryAllocated / TotalExecutions : 0L;

			return new PipelineMetrics
			{
				TotalExecutions = TotalExecutions,
				TotalDuration = TotalDuration,
				AverageDuration = averageDuration,
				AverageMiddlewareCount = averageMiddlewareCount,
				TotalMemoryAllocated = TotalMemoryAllocated,
				AverageMemoryPerExecution = averageMemoryPerExecution,
			};
		}
	}

	/// <summary>
	/// Immutable metrics for batch processing.
	/// </summary>
	private sealed record BatchProcessingMetricsData
	{
		public BatchProcessingMetricsData()
		{
		}

		public BatchProcessingMetricsData(int batchSize, TimeSpan processingTime, int parallelDegree, int successCount, int failureCount)
		{
			TotalBatches = 1;
			TotalItemsProcessed = batchSize;
			TotalProcessingTime = processingTime;
			TotalParallelDegree = parallelDegree;
			TotalSuccessCount = successCount;
			TotalFailureCount = failureCount;
		}

		public int TotalBatches { get; init; }

		public int TotalItemsProcessed { get; init; }

		public TimeSpan TotalProcessingTime { get; init; }

		public int TotalParallelDegree { get; init; }

		public int TotalSuccessCount { get; init; }

		public int TotalFailureCount { get; init; }

		public BatchProcessingMetricsData RecordBatch(int batchSize, TimeSpan processingTime, int parallelDegree, int successCount,
			int failureCount) => this with
			{
				TotalBatches = TotalBatches + 1,
				TotalItemsProcessed = TotalItemsProcessed + batchSize,
				TotalProcessingTime = TotalProcessingTime + processingTime,
				TotalParallelDegree = TotalParallelDegree + parallelDegree,
				TotalSuccessCount = TotalSuccessCount + successCount,
				TotalFailureCount = TotalFailureCount + failureCount,
			};

		public BatchProcessingMetrics ToBatchProcessingMetrics()
		{
			var averageBatchSize = TotalBatches > 0 ? (double)TotalItemsProcessed / TotalBatches : 0.0;
			var averageProcessingTimePerBatch = TotalBatches > 0 ? TotalProcessingTime / TotalBatches : TimeSpan.Zero;
			var averageProcessingTimePerItem = TotalItemsProcessed > 0 ? TotalProcessingTime / TotalItemsProcessed : TimeSpan.Zero;
			var averageParallelDegree = TotalBatches > 0 ? (double)TotalParallelDegree / TotalBatches : 0.0;
			var overallSuccessRate = (TotalSuccessCount + TotalFailureCount) > 0
				? (double)TotalSuccessCount / (TotalSuccessCount + TotalFailureCount)
				: 0.0;
			var throughputItemsPerSecond = TotalProcessingTime.TotalSeconds > 0
				? TotalItemsProcessed / TotalProcessingTime.TotalSeconds
				: 0.0;

			return new BatchProcessingMetrics
			{
				TotalBatches = TotalBatches,
				TotalItemsProcessed = TotalItemsProcessed,
				TotalProcessingTime = TotalProcessingTime,
				AverageBatchSize = averageBatchSize,
				AverageProcessingTimePerBatch = averageProcessingTimePerBatch,
				AverageProcessingTimePerItem = averageProcessingTimePerItem,
				AverageParallelDegree = averageParallelDegree,
				OverallSuccessRate = overallSuccessRate,
				ThroughputItemsPerSecond = throughputItemsPerSecond,
			};
		}
	}

	/// <summary>
	/// Immutable metrics for handler registry activity.
	/// </summary>
	private sealed record HandlerRegistryMetricsData
	{
		public static HandlerRegistryMetricsData Empty { get; } = new();

		public int TotalLookups { get; init; }

		public TimeSpan TotalLookupTime { get; init; }

		public int TotalHandlersFound { get; init; }

		public int CacheHits { get; init; }

		public int CacheMisses { get; init; }

		public HandlerRegistryMetricsData RecordLookup(TimeSpan lookupTime, int handlersFound, bool isCacheHit) => this with
		{
			TotalLookups = TotalLookups + 1,
			TotalLookupTime = TotalLookupTime + lookupTime,
			TotalHandlersFound = TotalHandlersFound + handlersFound,
			CacheHits = CacheHits + (isCacheHit ? 1 : 0),
			CacheMisses = CacheMisses + (isCacheHit ? 0 : 1),
		};

		public HandlerRegistryMetrics ToHandlerRegistryMetrics()
		{
			var averageLookupTime = TotalLookups > 0 ? TotalLookupTime / TotalLookups : TimeSpan.Zero;
			var averageHandlersPerLookup = TotalLookups > 0 ? (double)TotalHandlersFound / TotalLookups : 0.0;
			var cacheHitRate = (CacheHits + CacheMisses) > 0 ? (double)CacheHits / (CacheHits + CacheMisses) : 0.0;

			return new HandlerRegistryMetrics
			{
				TotalLookups = TotalLookups,
				TotalLookupTime = TotalLookupTime,
				AverageLookupTime = averageLookupTime,
				AverageHandlersPerLookup = averageHandlersPerLookup,
				CacheHits = CacheHits,
				CacheMisses = CacheMisses,
				CacheHitRate = cacheHitRate,
			};
		}
	}

	/// <summary>
	/// Immutable metrics for queue activity.
	/// </summary>
	private sealed record QueueMetricsData
	{
		public QueueMetricsData()
		{
		}

		[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Item count reserved for future metrics.")]
		public QueueMetricsData(string operation, int itemCount, TimeSpan duration, int queueDepth)
		{
			OperationMetrics = new ConcurrentDictionary<string, ComponentMetricsData>(StringComparer.OrdinalIgnoreCase)
			{
				[operation] = new ComponentMetricsData(duration, success: true),
			};

			CurrentDepth = queueDepth;
			MaxDepthReached = queueDepth;
			TotalDepthSamples = 1;
			TotalDepthSum = queueDepth;
			TotalOperations = 1;
			TotalOperationTime = duration;
		}

		public ConcurrentDictionary<string, ComponentMetricsData> OperationMetrics { get; init; } = new(StringComparer.OrdinalIgnoreCase);

		public int CurrentDepth { get; init; }

		public int MaxDepthReached { get; init; }

		public long TotalDepthSamples { get; init; }

		public long TotalDepthSum { get; init; }

		public int TotalOperations { get; init; }

		public TimeSpan TotalOperationTime { get; init; }

		[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Item count reserved for future metrics.")]
		public QueueMetricsData RecordOperation(string operation, int itemCount, TimeSpan duration, int queueDepth)
		{
			var snapshot = new ConcurrentDictionary<string, ComponentMetricsData>(OperationMetrics, StringComparer.OrdinalIgnoreCase);
			_ = snapshot.AddOrUpdate(
				operation,
				(key, state) =>
				{
					_ = key;
					return new ComponentMetricsData(state, success: true);
				},
				(key, existing, state) =>
				{
					_ = key;
					return existing.RecordExecution(state, success: true);
				},
				duration);

			return this with
			{
				OperationMetrics = snapshot,
				CurrentDepth = queueDepth,
				MaxDepthReached = Math.Max(MaxDepthReached, queueDepth),
				TotalDepthSamples = TotalDepthSamples + 1,
				TotalDepthSum = TotalDepthSum + queueDepth,
				TotalOperations = TotalOperations + 1,
				TotalOperationTime = TotalOperationTime + duration,
			};
		}

		public QueueMetrics ToQueueMetrics()
		{
			var operationSnapshot = OperationMetrics.ToDictionary(static kvp => kvp.Key, static kvp => kvp.Value.ToComponentMetrics(), StringComparer.Ordinal);
			var averageDepth = TotalDepthSamples > 0 ? (double)TotalDepthSum / TotalDepthSamples : 0.0;
			var throughput = TotalOperationTime.TotalSeconds > 0 ? TotalOperations / TotalOperationTime.TotalSeconds : 0.0;

			return new QueueMetrics
			{
				OperationMetrics = operationSnapshot,
				CurrentDepth = CurrentDepth,
				MaxDepthReached = MaxDepthReached,
				AverageDepth = averageDepth,
				ThroughputOperationsPerSecond = throughput,
			};
		}
	}
}
