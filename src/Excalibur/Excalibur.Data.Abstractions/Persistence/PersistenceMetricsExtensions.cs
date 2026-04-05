// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Extension methods for <see cref="IPersistenceMetrics"/>.
/// </summary>
public static class PersistenceMetricsExtensions
{
	/// <summary>Records a connection pool metric.</summary>
	public static void RecordConnectionPoolMetrics(this IPersistenceMetrics metrics, int activeConnections, int idleConnections, string providerName)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IPersistenceMetricsAdmin admin)
		{
			admin.RecordConnectionPoolMetrics(activeConnections, idleConnections, providerName);
		}
	}

	/// <summary>Records a cache hit or miss.</summary>
	public static void RecordCacheMetrics(this IPersistenceMetrics metrics, bool hit, string cacheKey, string providerName)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IPersistenceMetricsAdmin admin)
		{
			admin.RecordCacheMetrics(hit, cacheKey, providerName);
		}
	}

	/// <summary>Records transaction metrics.</summary>
	public static void RecordTransactionMetrics(this IPersistenceMetrics metrics, TimeSpan duration, bool committed, string providerName)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IPersistenceMetricsAdmin admin)
		{
			admin.RecordTransactionMetrics(duration, committed, providerName);
		}
	}

	/// <summary>Gets current metrics snapshot.</summary>
	public static IDictionary<string, object> GetMetricsSnapshot(this IPersistenceMetrics metrics)
	{
		ArgumentNullException.ThrowIfNull(metrics);
		if (metrics is IPersistenceMetricsAdmin admin)
		{
			return admin.GetMetricsSnapshot();
		}

		return new Dictionary<string, object>();
	}
}
