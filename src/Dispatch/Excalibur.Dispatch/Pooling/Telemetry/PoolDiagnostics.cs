// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Pooling.Telemetry;

/// <summary>
/// Provides thread-safe diagnostic capabilities for tracking pool operation metrics across multiple pools.
/// </summary>
public sealed class PoolDiagnostics
{
	private readonly ConcurrentDictionary<string, PoolMetrics> _metrics = new(StringComparer.Ordinal);

	/// <summary>
	/// Records a rent operation for the specified pool, incrementing the rent counter.
	/// </summary>
	/// <param name="poolName"> The name of the pool for which to record the rent operation. </param>
	public void RecordRent(string poolName)
	{
		var metrics = _metrics.GetOrAdd(poolName, static _ => new PoolMetrics());
		_ = Interlocked.Increment(ref metrics._rents);
	}

	/// <summary>
	/// Records a return operation for the specified pool, incrementing the return counter.
	/// </summary>
	/// <param name="poolName"> The name of the pool for which to record the return operation. </param>
	public void RecordReturn(string poolName)
	{
		var metrics = _metrics.GetOrAdd(poolName, static _ => new PoolMetrics());
		_ = Interlocked.Increment(ref metrics._returns);
	}

	/// <summary>
	/// Records a cache miss operation for the specified pool, incrementing the miss counter.
	/// </summary>
	/// <param name="poolName"> The name of the pool for which to record the miss operation. </param>
	public void RecordMiss(string poolName)
	{
		var metrics = _metrics.GetOrAdd(poolName, static _ => new PoolMetrics());
		_ = Interlocked.Increment(ref metrics._misses);
	}

	/// <summary>
	/// Gets a report of all pool metrics for diagnostics and logging purposes.
	/// </summary>
	/// <returns> A dictionary containing metrics for each pool. </returns>
	public IDictionary<string, object> GetReport()
	{
		var report = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var kvp in _metrics)
		{
			var poolName = kvp.Key;
			var metrics = kvp.Value;

			report[poolName] = new
			{
				metrics.TotalRents,
				metrics.TotalReturns,
				metrics.TotalMisses,
				metrics.ActiveRentals,
			};
		}

		return report;
	}

	private sealed class PoolMetrics
	{
		internal long _rents;
		internal long _returns;
		internal long _misses;

		public long TotalRents => _rents;

		public long TotalReturns => _returns;

		public long TotalMisses => _misses;

		public long ActiveRentals => _rents - _returns;
	}
}
