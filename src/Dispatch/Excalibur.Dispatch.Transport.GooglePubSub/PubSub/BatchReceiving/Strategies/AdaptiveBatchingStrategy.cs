// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Excalibur.Dispatch.Transport.GooglePubSub;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Adaptive batching strategy that adjusts batch size based on system performance and throughput.
/// </summary>
public sealed partial class AdaptiveBatchingStrategy : IBatchingStrategy
{
	private readonly IOptions<BatchConfiguration> _options;
	private readonly ILogger<AdaptiveBatchingStrategy> _logger;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif

	private readonly Queue<BatchResult> _recentResults;
	private readonly int _windowSize = 20;
	private readonly double _targetProcessingTimeMs;
	private int _currentBatchSize;
	private double _aggressiveness;

	/// <summary>
	/// Performance tracking.
	/// </summary>
	private double _ewmaProcessingTime; // Exponentially weighted moving average

	private double _ewmaThroughput;
	private int _consecutiveFlowControlHits;
	private int _stableIterations;

	/// <summary>
	/// Initializes a new instance of the <see cref="AdaptiveBatchingStrategy" /> class.
	/// </summary>
	public AdaptiveBatchingStrategy(
		IOptions<BatchConfiguration> options,
		ILogger<AdaptiveBatchingStrategy> logger)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		var config = _options.Value;
		_currentBatchSize = config.MaxMessagesPerBatch / 2; // Start at 50%
		_targetProcessingTimeMs = config.TargetBatchProcessingTime.TotalMilliseconds;
		_aggressiveness = 0.15; // 15% adjustment per iteration

		_recentResults = new Queue<BatchResult>(_windowSize);
		_ewmaProcessingTime = _targetProcessingTimeMs;
		_ewmaThroughput = 0;
	}

	/// <inheritdoc />
	public int DetermineNextBatchSize(BatchingContext context)
	{
		lock (_lock)
		{
			var config = _options.Value;

			// Apply flow control constraints first
			if (context.FlowControlQuota > 0 && context.FlowControlQuota < _currentBatchSize)
			{
				LogFlowControlLimit(_currentBatchSize, context.FlowControlQuota);
				return context.FlowControlQuota;
			}

			// Consider memory pressure
			if (context.MemoryPressure > 0.8)
			{
				var reducedSize = (int)(_currentBatchSize * (1 - context.MemoryPressure));
				LogMemoryPressure(context.MemoryPressure, reducedSize);

				return Math.Max(config.MinMessagesPerBatch, reducedSize);
			}

			// Optimize based on queue depth
			if (context.QueueDepth > _currentBatchSize * 10)
			{
				// Large backlog, be more aggressive
				_aggressiveness = Math.Min(0.3, _aggressiveness * 1.1);
			}
			else if (context.QueueDepth < _currentBatchSize)
			{
				// Small queue, be less aggressive
				_aggressiveness = Math.Max(0.05, _aggressiveness * 0.9);
			}

			return _currentBatchSize;
		}
	}

	/// <inheritdoc />
	public void RecordBatchResult(BatchResult result)
	{
		lock (_lock)
		{
			_recentResults.Enqueue(result);
			if (_recentResults.Count > _windowSize)
			{
				_ = _recentResults.Dequeue();
			}

			UpdateMetrics(result);
			AdjustBatchSize(result);

			LogBatchResult(result.BatchSize, result.ProcessingDuration.TotalMilliseconds,
				result.BatchSize / result.ProcessingDuration.TotalSeconds, _currentBatchSize, result.WasFlowControlled);
		}
	}

	/// <inheritdoc />
	public void Reset()
	{
		lock (_lock)
		{
			var config = _options.Value;
			_currentBatchSize = config.MaxMessagesPerBatch / 2;
			_recentResults.Clear();
			_ewmaProcessingTime = _targetProcessingTimeMs;
			_ewmaThroughput = 0;
			_consecutiveFlowControlHits = 0;
			_stableIterations = 0;
			_aggressiveness = 0.15;

			LogStrategyReset();
		}
	}

	/// <summary>
	/// Gets current strategy statistics.
	/// </summary>
	public AdaptiveStrategyStatistics GetStatistics()
	{
		lock (_lock)
		{
			return new AdaptiveStrategyStatistics
			{
				CurrentBatchSize = _currentBatchSize,
				AverageProcessingTime = _ewmaProcessingTime,
				AverageThroughput = _ewmaThroughput,
				Aggressiveness = _aggressiveness,
				StableIterations = _stableIterations,
				RecentResults = [.. _recentResults],
			};
		}
	}

	private void UpdateMetrics(BatchResult result)
	{
		const double alpha = 0.2; // EWMA smoothing factor

		var processingTimeMs = result.ProcessingDuration.TotalMilliseconds;
		var throughput = result.SuccessCount / result.ProcessingDuration.TotalSeconds;

		_ewmaProcessingTime = (alpha * processingTimeMs) + ((1 - alpha) * _ewmaProcessingTime);
		_ewmaThroughput = (alpha * throughput) + ((1 - alpha) * _ewmaThroughput);

		if (result.WasFlowControlled)
		{
			_consecutiveFlowControlHits++;
		}
		else
		{
			_consecutiveFlowControlHits = 0;
		}
	}

	private void AdjustBatchSize(BatchResult result)
	{
		var config = _options.Value;
		_ = result.ProcessingDuration.TotalMilliseconds; // Used for EWMA calculation in UpdateMetrics

		// Check if we're meeting our target
		var performanceRatio = _targetProcessingTimeMs / _ewmaProcessingTime;
		var isStable = Math.Abs(performanceRatio - 1.0) < 0.1; // Within 10% of target

		if (isStable)
		{
			_stableIterations++;
			if (_stableIterations > 10)
			{
				// System is stable, try small optimization
				_aggressiveness = Math.Max(0.05, _aggressiveness * 0.95);
			}

			return;
		}

		_stableIterations = 0;

		// Calculate adjustment
		var adjustment = 1.0;

		if (performanceRatio > 1.1)
		{
			// Processing faster than target, increase batch size
			adjustment = 1 + (_aggressiveness * Math.Min(performanceRatio - 1, 0.5));
		}
		else if (performanceRatio < 0.9)
		{
			// Processing slower than target, decrease batch size
			adjustment = 1 - (_aggressiveness * Math.Min(1 - performanceRatio, 0.5));
		}

		// Consider flow control feedback
		if (_consecutiveFlowControlHits > 3)
		{
			// Back off if hitting flow control limits
			adjustment = Math.Min(adjustment, 0.9);
		}

		// Apply adjustment
		var newSize = (int)(_currentBatchSize * adjustment);
		newSize = Math.Max(
			config.MinMessagesPerBatch,
			Math.Min(config.MaxMessagesPerBatch, newSize));

		// Prevent thrashing
		if (Math.Abs(newSize - _currentBatchSize) >= Math.Max(1, _currentBatchSize * 0.02))
		{
			_currentBatchSize = newSize;

			LogBatchSizeAdjusted(_currentBatchSize, newSize, performanceRatio);
		}
	}

	// Source-generated logging methods (Sprint 363 - EventId Migration)
	[LoggerMessage(GooglePubSubEventId.AdaptiveFlowControlLimit, LogLevel.Debug,
		"Flow control limiting batch size from {Current} to {Limited}")]
	private partial void LogFlowControlLimit(int current, int limited);

	[LoggerMessage(GooglePubSubEventId.AdaptiveMemoryPressure, LogLevel.Warning,
		"High memory pressure {Pressure:P}, reducing batch size to {Size}")]
	private partial void LogMemoryPressure(double pressure, int size);

	[LoggerMessage(GooglePubSubEventId.AdaptiveBatchResult, LogLevel.Debug,
		"Batch result: Size={Size}, Duration={Duration}ms, Throughput={Throughput} msg/s, NextSize={NextSize}, FlowControlled={FlowControlled}")]
	private partial void LogBatchResult(int size, double duration, double throughput, int nextSize, bool flowControlled);

	[LoggerMessage(GooglePubSubEventId.AdaptiveStrategyReset, LogLevel.Information,
		"Adaptive batching strategy reset")]
	private partial void LogStrategyReset();

	[LoggerMessage(GooglePubSubEventId.AdaptiveBatchSizeAdjusted, LogLevel.Information,
		"Adjusted batch size: {OldSize} -> {NewSize} (Performance ratio: {Ratio:F2})")]
	private partial void LogBatchSizeAdjusted(int oldSize, int newSize, double ratio);
}
