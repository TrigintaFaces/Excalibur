// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batching strategy that optimizes batch sizes based on time constraints and processing patterns.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TimeBoundBatchingStrategy" /> class.
/// </remarks>
/// <param name="configuration"> Batch configuration. </param>
public sealed class TimeBoundBatchingStrategy(BatchConfiguration configuration) : IBatchingStrategy
{
	private readonly BatchConfiguration _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif
	private readonly Queue<TimestampedBatch> _recentBatches = new();
	private DateTimeOffset _lastBatchTime = DateTimeOffset.UtcNow;
	private double _averageProcessingTime;
	private int _consecutiveTimeouts;

	/// <inheritdoc />
	public int DetermineNextBatchSize(BatchingContext context)
	{
		lock (_lock)
		{
			var timeSinceLastBatch = DateTimeOffset.UtcNow - _lastBatchTime;
			var targetBatchSize = _configuration.MaxMessagesPerBatch;

			// If we haven't processed a batch recently, reduce size to improve responsiveness
			if (timeSinceLastBatch.TotalMilliseconds > 1000)
			{
				targetBatchSize = Math.Max(_configuration.MinMessagesPerBatch, targetBatchSize / 2);
			}

			// If we're processing very frequently, increase batch size for efficiency
			else if (timeSinceLastBatch.TotalMilliseconds < 100 && _averageProcessingTime > 0)
			{
				// Estimate how many more messages we can handle based on processing time
				var remainingTime = 1000 - timeSinceLastBatch.TotalMilliseconds; // Target 1 second batches
				var additionalMessages = (int)(remainingTime / _averageProcessingTime);
				targetBatchSize = Math.Min(targetBatchSize, targetBatchSize + additionalMessages);
			}

			// Apply flow control constraints
			if (context.FlowControlQuota > 0)
			{
				targetBatchSize = Math.Min(targetBatchSize, context.FlowControlQuota);
			}

			// Reduce batch size if we've had consecutive timeouts
			if (_consecutiveTimeouts > 2)
			{
				targetBatchSize = Math.Max(
					_configuration.MinMessagesPerBatch,
					targetBatchSize - (_consecutiveTimeouts * 5));
			}

			// Apply processing rate considerations
			if (context.ProcessingRate is > 0 and < 10) // Low processing rate
			{
				targetBatchSize = Math.Max(_configuration.MinMessagesPerBatch, targetBatchSize / 2);
			}

			return Math.Max(
				_configuration.MinMessagesPerBatch,
				Math.Min(targetBatchSize, _configuration.MaxMessagesPerBatch));
		}
	}

	/// <inheritdoc />
	public void RecordBatchResult(BatchResult result)
	{
		if (result == null)
		{
			return;
		}

		lock (_lock)
		{
			_lastBatchTime = DateTimeOffset.UtcNow;

			// Track processing time
			var processingTimeMs = result.ProcessingDuration.TotalMilliseconds;
			if (result.SuccessCount > 0)
			{
				var timePerMessage = processingTimeMs / result.SuccessCount;
				_averageProcessingTime = _averageProcessingTime == 0
					? timePerMessage
					: (_averageProcessingTime * 0.7) + (timePerMessage * 0.3);
			}

			// Track timeout behavior
			if (result.WasFlowControlled)
			{
				_consecutiveTimeouts++;
			}
			else
			{
				_consecutiveTimeouts = 0;
			}

			// Add to recent batches for pattern analysis
			_recentBatches.Enqueue(new TimestampedBatch
			{
				Timestamp = DateTimeOffset.UtcNow,
				BatchSize = result.BatchSize,
				ProcessingDuration = result.ProcessingDuration,
				SuccessCount = result.SuccessCount,
			});

			// Keep only recent batches (last 50)
			while (_recentBatches.Count > 50)
			{
				_ = _recentBatches.Dequeue();
			}
		}
	}

	/// <inheritdoc />
	public void Reset()
	{
		lock (_lock)
		{
			_recentBatches.Clear();
			_lastBatchTime = DateTimeOffset.UtcNow;
			_averageProcessingTime = 0;
			_consecutiveTimeouts = 0;
		}
	}

	/// <summary>
	/// Represents a timestamped batch for pattern analysis.
	/// </summary>
	private sealed class TimestampedBatch
	{
		public DateTimeOffset Timestamp { get; set; }

		public int BatchSize { get; set; }

		public TimeSpan ProcessingDuration { get; set; }

		public int SuccessCount { get; set; }
	}
}
