// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Delivery.BatchProcessing;

/// <summary>
/// Calculates optimal batch sizes based on throughput and performance metrics.
/// </summary>
public sealed class DynamicBatchSizeCalculator
{
	private const int MeasurementWindowSize = 10;
	private readonly int _minBatchSize;
	private readonly int _maxBatchSize;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	private readonly Queue<ThroughputMeasurement> _measurements;
	private int _currentBatchSize;
	private double _lastThroughput;

	/// <summary>
	/// Initializes a new instance of the <see cref="DynamicBatchSizeCalculator" /> class.
	/// </summary>
	/// <param name="minBatchSize"> The minimum allowed batch size. </param>
	/// <param name="maxBatchSize"> The maximum allowed batch size. </param>
	/// <param name="initialBatchSize"> The initial batch size to start with. </param>
	public DynamicBatchSizeCalculator(int minBatchSize, int maxBatchSize, int initialBatchSize)
	{
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minBatchSize);
		ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxBatchSize);
		ArgumentOutOfRangeException.ThrowIfLessThan(maxBatchSize, minBatchSize);

		_minBatchSize = minBatchSize;
		_maxBatchSize = maxBatchSize;
		_currentBatchSize = Math.Clamp(initialBatchSize, minBatchSize, maxBatchSize);
		_measurements = new Queue<ThroughputMeasurement>(MeasurementWindowSize);
	}

	/// <summary>
	/// Gets the current recommended batch size.
	/// </summary>
	/// <value>
	/// The current recommended batch size.
	/// </value>
	public int CurrentBatchSize
	{
		get
		{
			lock (_lock)
			{
				return _currentBatchSize;
			}
		}
	}

	/// <summary>
	/// Records a batch processing result and adjusts the batch size accordingly.
	/// </summary>
	/// <param name="itemsProcessed"> The number of items processed in the batch. </param>
	/// <param name="duration"> The time taken to process the batch. </param>
	/// <param name="successRate"> The success rate of the batch (0.0 to 1.0). </param>
	public void RecordBatchResult(int itemsProcessed, TimeSpan duration, double successRate)
	{
		if (duration <= TimeSpan.Zero || itemsProcessed <= 0)
		{
			return;
		}

		var throughput = itemsProcessed / duration.TotalSeconds;

		lock (_lock)
		{
			// Add new measurement
			_measurements.Enqueue(new ThroughputMeasurement
			{
				BatchSize = _currentBatchSize,
				Throughput = throughput,
				SuccessRate = successRate,
				Timestamp = DateTimeOffset.UtcNow,
			});

			// Remove old measurements
			while (_measurements.Count > MeasurementWindowSize)
			{
				_ = _measurements.Dequeue();
			}

			// Calculate average throughput and success rate
			var avgThroughput = _measurements.Average(static m => m.Throughput);
			var avgSuccessRate = _measurements.Average(static m => m.SuccessRate);

			// Adjust batch size based on performance
			AdjustBatchSize(avgThroughput, avgSuccessRate);
		}
	}

	private void AdjustBatchSize(double avgThroughput, double avgSuccessRate)
	{
		// If success rate is low, reduce batch size
		if (avgSuccessRate < 0.95)
		{
			_currentBatchSize = Math.Max(_minBatchSize, (int)(_currentBatchSize * 0.8));
			return;
		}

		// If throughput is improving, try increasing batch size
		if (avgThroughput > _lastThroughput * 1.05) // 5% improvement threshold
		{
			_currentBatchSize = Math.Min(_maxBatchSize, (int)(_currentBatchSize * 1.2));
		}

		// If throughput is degrading, reduce batch size
		else if (avgThroughput < _lastThroughput * 0.95) // 5% degradation threshold
		{
			_currentBatchSize = Math.Max(_minBatchSize, (int)(_currentBatchSize * 0.9));
		}

		// Otherwise, make small adjustments based on current performance
		else
		{
			// Slightly increase if we're not at max and performance is stable
			if (_currentBatchSize < _maxBatchSize && avgSuccessRate > 0.98)
			{
				_currentBatchSize = Math.Min(_maxBatchSize, _currentBatchSize + 1);
			}
		}

		_lastThroughput = avgThroughput;
	}

	private sealed class ThroughputMeasurement
	{
		public int BatchSize { get; init; }

		public double Throughput { get; init; }

		public double SuccessRate { get; init; }

		public DateTimeOffset Timestamp { get; init; }
	}
}
