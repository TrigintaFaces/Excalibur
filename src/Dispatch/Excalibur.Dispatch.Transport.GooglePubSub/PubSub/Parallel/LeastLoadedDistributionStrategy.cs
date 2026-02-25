// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Least-loaded work distribution strategy.
/// </summary>
public sealed class LeastLoadedDistributionStrategy : IWorkDistributionStrategy
{
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private readonly Dictionary<int, WorkerLoad> _workerLoads;

	/// <summary>
	/// Initializes a new instance of the <see cref="LeastLoadedDistributionStrategy" /> class.
	/// </summary>
	public LeastLoadedDistributionStrategy() => _workerLoads = [];

	/// <inheritdoc />
	public int SelectWorker(WorkDistributionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock (_lock)
		{
			var selectedWorker = 0;
			var minLoad = int.MaxValue;

			for (var i = 0; i < context.TotalWorkers; i++)
			{
				var load = context.PendingWorkCounts[i];
				if (_workerLoads.TryGetValue(i, out var workerLoad))
				{
					// Factor in average processing time
					load += (int)(workerLoad.AverageProcessingTime.TotalMilliseconds / 100);
				}

				if (load < minLoad)
				{
					minLoad = load;
					selectedWorker = i;
				}
			}

			// Track assignment
			if (!_workerLoads.TryGetValue(selectedWorker, out var value))
			{
				value = new WorkerLoad();
				_workerLoads[selectedWorker] = value;
			}

			value.ActiveCount++;

			return selectedWorker;
		}
	}

	/// <inheritdoc />
	public void RecordCompletion(int workerId, TimeSpan duration)
	{
		lock (_lock)
		{
			if (_workerLoads.TryGetValue(workerId, out var load))
			{
				load.ActiveCount--;
				load.TotalProcessingTime += duration;
				load.CompletedCount++;
			}
		}
	}

	/// <summary>
	/// Worker load information.
	/// </summary>
	private sealed class WorkerLoad
	{
		public int ActiveCount { get; set; }

		public int CompletedCount { get; set; }

		public TimeSpan TotalProcessingTime { get; set; }

		public TimeSpan AverageProcessingTime =>
			CompletedCount > 0
				? TimeSpan.FromTicks(TotalProcessingTime.Ticks / CompletedCount)
				: TimeSpan.Zero;
	}
}
