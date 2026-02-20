// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Transport.Google;

/// <summary>
/// Work-stealing distribution strategy.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA5394:Do not use insecure randomness",
	Justification = "Random is used for probabilistic worker selection/load balancing, not for security purposes. Cryptographic randomness is unnecessary for work distribution.")]
public sealed class WorkStealingDistributionStrategy : IWorkDistributionStrategy
{
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private readonly Random _random;
	private readonly Dictionary<int, int> _workerQueueDepths;

	/// <summary>
	/// Initializes a new instance of the <see cref="WorkStealingDistributionStrategy" /> class.
	/// </summary>
	public WorkStealingDistributionStrategy()
	{
		_random = new Random();
		_workerQueueDepths = [];
	}

	/// <inheritdoc />
	public int SelectWorker(WorkDistributionContext context)
	{
		ArgumentNullException.ThrowIfNull(context);

		lock (_lock)
		{
			// First, try to find an idle worker
			for (var i = 0; i < context.TotalWorkers; i++)
			{
				if (context.PendingWorkCounts[i] == 0)
				{
					return i;
				}
			}

			// If all workers are busy, use probabilistic selection Workers with less work have higher probability
			var totalInverseLoad = 0.0;
			var inverseLoads = new double[context.TotalWorkers];

			for (var i = 0; i < context.TotalWorkers; i++)
			{
				var load = Math.Max(1, context.PendingWorkCounts[i]);
				inverseLoads[i] = 1.0 / load;
				totalInverseLoad += inverseLoads[i];
			}

			// Select based on probability
			var randomValue = _random.NextDouble() * totalInverseLoad;
			var cumulative = 0.0;

			for (var i = 0; i < context.TotalWorkers; i++)
			{
				cumulative += inverseLoads[i];
				if (randomValue <= cumulative)
				{
					return i;
				}
			}

			// Fallback to first worker
			return 0;
		}
	}

	/// <inheritdoc />
	public void RecordCompletion(int workerId, TimeSpan duration)
	{
		lock (_lock)
		{
			if (_workerQueueDepths.TryGetValue(workerId, out var value))
			{
				_workerQueueDepths[workerId] = Math.Max(0, value - 1);
			}
		}
	}
}
