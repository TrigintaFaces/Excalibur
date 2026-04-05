// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides administrative persistence metrics operations.
/// Implementations should implement this alongside <see cref="IPersistenceMetrics"/>.
/// </summary>
public interface IPersistenceMetricsAdmin
{
	/// <summary>Records a connection pool metric.</summary>
	void RecordConnectionPoolMetrics(int activeConnections, int idleConnections, string providerName);

	/// <summary>Records a cache hit or miss.</summary>
	void RecordCacheMetrics(bool hit, string cacheKey, string providerName);

	/// <summary>Records transaction metrics.</summary>
	void RecordTransactionMetrics(TimeSpan duration, bool committed, string providerName);

	/// <summary>Gets current metrics snapshot.</summary>
	IDictionary<string, object> GetMetricsSnapshot();
}
