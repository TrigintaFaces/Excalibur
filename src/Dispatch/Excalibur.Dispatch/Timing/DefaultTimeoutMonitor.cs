// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Timing;

/// <summary>
/// Default implementation of timeout monitoring with statistical tracking. R7.4: Timeout monitoring and adaptive timeout management.
/// </summary>
public sealed class DefaultTimeoutMonitor : ITimeoutMonitor
{
	private readonly ConcurrentDictionary<TimeoutOperationType, OperationStatistics> _statistics = new();
#if NET9_0_OR_GREATER

	private readonly Lock _lockObject = new();

#else

	private readonly object _lockObject = new();

#endif

	/// <inheritdoc />
	public ITimeoutOperationToken StartOperation(TimeoutOperationType operationType, TimeoutContext? context = null) =>
		new TimeoutOperationToken(operationType, context);

	/// <inheritdoc />
	public void CompleteOperation(ITimeoutOperationToken token, bool success, bool timedOut)
	{
		ArgumentNullException.ThrowIfNull(token);

		if (token is not TimeoutOperationToken internalToken)
		{
			return;
		}

		// Complete the token
		internalToken.Complete(success, timedOut);

		// Update statistics
		var operationType = token.OperationType;
		var duration = token.Elapsed;

		_ = _statistics.AddOrUpdate(
			operationType,
			(key, state) => new OperationStatistics(key, state.duration, state.success, state.timedOut),
			(key, existing, state) =>
			{
				_ = key;
				return existing.AddSample(state.duration, state.success, state.timedOut);
			},
			(duration, success, timedOut));
	}

	/// <inheritdoc />
	public TimeoutStatistics GetStatistics(TimeoutOperationType operationType)
	{
		if (_statistics.TryGetValue(operationType, out var stats))
		{
			return stats.ToTimeoutStatistics();
		}

		return new TimeoutStatistics
		{
			OperationType = operationType,
			TotalOperations = 0,
			SuccessfulOperations = 0,
			TimedOutOperations = 0,
			AverageDuration = TimeSpan.Zero,
			MedianDuration = TimeSpan.Zero,
			P95Duration = TimeSpan.Zero,
			P99Duration = TimeSpan.Zero,
			MinDuration = TimeSpan.Zero,
			MaxDuration = TimeSpan.Zero,
			LastUpdated = DateTimeOffset.UtcNow,
		};
	}

	/// <inheritdoc />
	public TimeSpan GetRecommendedTimeout(TimeoutOperationType operationType, int percentile = 95, TimeoutContext? context = null)
	{
		if (!_statistics.TryGetValue(operationType, out var stats))
		{
			// No data available, return a default
			return TimeSpan.FromSeconds(30);
		}

		var timeoutStats = stats.ToTimeoutStatistics();
		return timeoutStats.GetPercentileDuration(percentile);
	}

	/// <inheritdoc />
	public void ClearStatistics(TimeoutOperationType? operationType = null)
	{
		lock (_lockObject)
		{
			if (operationType.HasValue)
			{
				_ = _statistics.TryRemove(operationType.Value, out _);
			}
			else
			{
				_statistics.Clear();
			}
		}
	}

	/// <inheritdoc />
	public int GetSampleCount(TimeoutOperationType operationType) =>
		_statistics.TryGetValue(operationType, out var stats) ? stats.TotalOperations : 0;

	/// <inheritdoc />
	public bool HasSufficientSamples(TimeoutOperationType operationType, int minimumSamples = 100) =>
		GetSampleCount(operationType) >= minimumSamples;

	/// <summary>
	/// Internal class for tracking operation statistics with high performance.
	/// </summary>
	private sealed class OperationStatistics
	{
		private readonly List<TimeSpan> _durations = [];
#if NET9_0_OR_GREATER

		private readonly Lock _lock = new();

#else

		private readonly object _lock = new();

#endif
		private TimeSpan _totalDuration = TimeSpan.Zero;

		public OperationStatistics(TimeoutOperationType operationType, TimeSpan duration, bool success, bool timedOut)
		{
			OperationType = operationType;
			TotalOperations = 1;
			SuccessfulOperations = success ? 1 : 0;
			TimedOutOperations = timedOut ? 1 : 0;
			MinDuration = duration;
			MaxDuration = duration;
			_totalDuration = duration;
			LastUpdated = DateTimeOffset.UtcNow;

			_durations.Add(duration);
		}

		public TimeoutOperationType OperationType { get; }

		public int TotalOperations { get; private set; }

		public int SuccessfulOperations { get; private set; }

		public int TimedOutOperations { get; private set; }

		public TimeSpan MinDuration { get; private set; }

		public TimeSpan MaxDuration { get; private set; }

		public DateTimeOffset LastUpdated { get; private set; }

		public OperationStatistics AddSample(TimeSpan duration, bool success, bool timedOut)
		{
			lock (_lock)
			{
				TotalOperations++;
				if (success)
				{
					SuccessfulOperations++;
				}

				if (timedOut)
				{
					TimedOutOperations++;
				}

				_totalDuration = _totalDuration.Add(duration);

				if (duration < MinDuration)
				{
					MinDuration = duration;
				}

				if (duration > MaxDuration)
				{
					MaxDuration = duration;
				}

				_durations.Add(duration);
				LastUpdated = DateTimeOffset.UtcNow;

				// Keep only recent samples to prevent memory growth
				if (_durations.Count > 10000)
				{
					_durations.RemoveRange(0, 1000);
				}
			}

			return this;
		}

		public TimeoutStatistics ToTimeoutStatistics()
		{
			lock (_lock)
			{
				var sortedDurations = _durations.OrderBy(static d => d.Ticks).ToArray();
				var count = sortedDurations.Length;

				var median = count > 0 ? sortedDurations[count / 2] : TimeSpan.Zero;
				var p95 = count > 0 ? sortedDurations[(int)(count * 0.95)] : TimeSpan.Zero;
				var p99 = count > 0 ? sortedDurations[(int)(count * 0.99)] : TimeSpan.Zero;
				var average = TotalOperations > 0 ? TimeSpan.FromTicks(_totalDuration.Ticks / TotalOperations) : TimeSpan.Zero;

				return new TimeoutStatistics
				{
					OperationType = OperationType,
					TotalOperations = TotalOperations,
					SuccessfulOperations = SuccessfulOperations,
					TimedOutOperations = TimedOutOperations,
					AverageDuration = average,
					MedianDuration = median,
					P95Duration = p95,
					P99Duration = p99,
					MinDuration = MinDuration,
					MaxDuration = MaxDuration,
					LastUpdated = LastUpdated,
				};
			}
		}
	}
}
