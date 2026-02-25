// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Monitors thread utilization for parallel message processing.
/// </summary>
public sealed class ThreadUtilizationMonitor : IDisposable
{
	private readonly ConcurrentDictionary<int, ThreadMetrics> _threadMetrics;
	private readonly ValueHistogram _activeThreadsHistogram;
	private readonly RateCounter _contextSwitchCount;
	private readonly Timer _samplingTimer;
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private int _maxObservedThreads;
	private readonly long _lastSampleTime;

	/// <summary>
	/// Initializes a new instance of the <see cref="ThreadUtilizationMonitor" /> class.
	/// </summary>
	public ThreadUtilizationMonitor()
	{
		_threadMetrics = new ConcurrentDictionary<int, ThreadMetrics>();
		_activeThreadsHistogram = new ValueHistogram();
		_contextSwitchCount = new RateCounter();
		_lastSampleTime = Stopwatch.GetTimestamp();

		// Sample every 100ms
		_samplingTimer = new Timer(
			_ => SampleThreadUtilization(),
			state: null,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromMilliseconds(100));
	}

	/// <summary>
	/// Records that a thread has started processing work.
	/// </summary>
	/// <param name="threadId"> The thread ID. </param>
	public void RecordThreadActive(int threadId)
	{
		var metrics = _threadMetrics.GetOrAdd(threadId, static _ => new ThreadMetrics());
		metrics.LastActiveTime = Stopwatch.GetTimestamp();
		_ = Interlocked.Increment(ref metrics.ActiveCount);
	}

	/// <summary>
	/// Records that a thread has completed processing work.
	/// </summary>
	/// <param name="threadId"> The thread ID. </param>
	/// <param name="processingTime"> Time spent processing. </param>
	public void RecordThreadIdle(int threadId, TimeSpan processingTime)
	{
		if (_threadMetrics.TryGetValue(threadId, out var metrics))
		{
			_ = Interlocked.Decrement(ref metrics.ActiveCount);
			_ = Interlocked.Add(ref metrics.TotalProcessingTicks, processingTime.Ticks);
			_ = Interlocked.Increment(ref metrics.TaskCount);
		}
	}

	/// <summary>
	/// Records a context switch.
	/// </summary>
	public void RecordContextSwitch() => _contextSwitchCount.Increment();

	/// <summary>
	/// Gets the current utilization report.
	/// </summary>
	/// <returns> Thread utilization report. </returns>
	public UtilizationReport GetUtilizationReport()
	{
		var activeThreads = 0;
		var totalThreads = _threadMetrics.Count;
		var totalProcessingTime = 0L;
		var totalTasks = 0L;

		foreach (var metrics in _threadMetrics.Values)
		{
			if (metrics.ActiveCount > 0)
			{
				activeThreads++;
			}

			totalProcessingTime += Interlocked.Read(ref metrics.TotalProcessingTicks);
			totalTasks += Interlocked.Read(ref metrics.TaskCount);
		}

		var avgUtilization = totalThreads > 0
			? (double)activeThreads / totalThreads * 100
			: 0;

		var avgProcessingTime = totalTasks > 0
			? TimeSpan.FromTicks(totalProcessingTime / totalTasks)
			: TimeSpan.Zero;

		return new UtilizationReport
		{
			TotalThreads = totalThreads,
			ActiveThreads = activeThreads,
			MaxObservedThreads = _maxObservedThreads,
			AverageUtilization = avgUtilization,
			AverageProcessingTime = avgProcessingTime,
			ContextSwitchCount = _contextSwitchCount.Value,
			TotalTasksProcessed = totalTasks,
		};
	}

	/// <summary>
	/// Disposes the monitor.
	/// </summary>
	public void Dispose() => _samplingTimer?.Dispose();

	private void SampleThreadUtilization()
	{
		var activeCount = 0;
		foreach (var metrics in _threadMetrics.Values)
		{
			if (metrics.ActiveCount > 0)
			{
				activeCount++;
			}
		}

		_activeThreadsHistogram.Record(activeCount);

		lock (_lock)
		{
			if (activeCount > _maxObservedThreads)
			{
				_maxObservedThreads = activeCount;
			}
		}
	}

	/// <summary>
	/// Metrics for a single thread.
	/// </summary>
	private sealed class ThreadMetrics
	{
		public int ActiveCount;
		public long LastActiveTime;
		public long TotalProcessingTicks;
		public long TaskCount;
	}
}
