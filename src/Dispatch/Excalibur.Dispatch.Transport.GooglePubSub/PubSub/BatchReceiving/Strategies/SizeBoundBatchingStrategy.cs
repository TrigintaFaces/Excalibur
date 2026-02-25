// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Batching strategy that optimizes batch sizes based on message size constraints.
/// </summary>
public sealed class SizeBoundBatchingStrategy : IBatchingStrategy
{
	private const int SampleWindowSize = 100;
	private readonly BatchConfiguration _configuration;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else
	private readonly object _lock = new();

#endif
	private readonly MovingAverage _avgMessageSize;
	private long _currentBatchBytes;
	private int _currentBatchMessages;

	/// <summary>
	/// Initializes a new instance of the <see cref="SizeBoundBatchingStrategy" /> class.
	/// </summary>
	/// <param name="configuration"> Batch configuration. </param>
	public SizeBoundBatchingStrategy(BatchConfiguration configuration)
	{
		_configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
		_avgMessageSize = new MovingAverage(SampleWindowSize);
		Reset();
	}

	/// <inheritdoc />
	public int DetermineNextBatchSize(BatchingContext context)
	{
		lock (_lock)
		{
			// Start with max allowed
			var targetBatchSize = _configuration.MaxMessagesPerBatch;

			// If we have message size history, optimize based on size constraints
			if (_avgMessageSize.Count > 0)
			{
				var avgSize = _avgMessageSize.Average;
				var remainingBytes = _configuration.MaxBatchSizeBytes - _currentBatchBytes;

				// Calculate how many messages fit in remaining byte budget
				var messagesFromByteLimit = remainingBytes > 0
					? (int)(remainingBytes / avgSize)
					: 0;

				// Take the minimum of various constraints
				targetBatchSize = Math.Min(targetBatchSize, messagesFromByteLimit);
			}

			// Apply flow control quota if available
			if (context.FlowControlQuota > 0)
			{
				targetBatchSize = Math.Min(targetBatchSize, context.FlowControlQuota);
			}

			// Consider queue depth - larger batches for deeper queues
			if (context.QueueDepth > 1000)
			{
				// Keep max batch size for high queue depth
			}
			else if (context.QueueDepth < 100)
			{
				// Reduce batch size for low queue depth to improve latency
				targetBatchSize = Math.Min(targetBatchSize, Math.Max(10, context.QueueDepth / 2));
			}

			// Apply minimum constraint
			return Math.Max(targetBatchSize, _configuration.MinMessagesPerBatch);
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
			// Update current batch state
			_currentBatchMessages += result.SuccessCount;
			_currentBatchBytes += result.TotalBytes;

			// Update average message size
			if (result is { SuccessCount: > 0, TotalBytes: > 0 })
			{
				var avgMessageSizeInBatch = (double)result.TotalBytes / result.SuccessCount;
				_avgMessageSize.Add(avgMessageSizeInBatch);
			}

			// Check if we should reset batch accumulation
			if (_currentBatchBytes >= _configuration.MaxBatchSizeBytes * 0.8 ||
				_currentBatchMessages >= _configuration.MaxMessagesPerBatch * 0.8)
			{
				// Getting close to limits, reset for next batch
				_currentBatchBytes = 0;
				_currentBatchMessages = 0;
			}
		}
	}

	/// <inheritdoc />
	public void Reset()
	{
		lock (_lock)
		{
			_currentBatchBytes = 0;
			_currentBatchMessages = 0;
			_avgMessageSize.Clear();
		}
	}

	/// <summary>
	/// Simple moving average calculator.
	/// </summary>
	private sealed class MovingAverage(int windowSize)
	{
		private readonly double[] _values = new double[windowSize];
		private int _index;
		private double _sum;

		public double Average => Count > 0 ? _sum / Count : 0;

		public int Count { get; private set; }

		public void Add(double value)
		{
			// Remove old value from sum if window is full
			if (Count == windowSize)
			{
				_sum -= _values[_index];
			}
			else
			{
				Count++;
			}

			// Add new value
			_values[_index] = value;
			_sum += value;

			// Move to next position
			_index = (_index + 1) % windowSize;
		}

		public void Clear()
		{
			_index = 0;
			Count = 0;
			_sum = 0;
			Array.Clear(_values, 0, windowSize);
		}
	}
}
