// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Defines metrics collection capabilities for persistence operations.
/// </summary>
public interface IPersistenceMetrics
{
	/// <summary>
	/// Gets the meter for recording persistence metrics.
	/// </summary>
	/// <value>
	/// The meter for recording persistence metrics.
	/// </value>
	Meter Meter { get; }

	/// <summary>
	/// Records the duration of a query execution.
	/// </summary>
	/// <param name="duration"> The duration of the query. </param>
	/// <param name="queryType"> The type of query executed. </param>
	/// <param name="success"> Whether the query was successful. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordQueryDuration(TimeSpan duration, string queryType, bool success, string providerName);

	/// <summary>
	/// Records the number of rows affected by a command.
	/// </summary>
	/// <param name="rowCount"> The number of rows affected. </param>
	/// <param name="commandType"> The type of command executed. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordRowsAffected(int rowCount, string commandType, string providerName);

	/// <summary>
	/// Records a connection pool metric.
	/// </summary>
	/// <param name="activeConnections"> The number of active connections. </param>
	/// <param name="idleConnections"> The number of idle connections. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordConnectionPoolMetrics(int activeConnections, int idleConnections, string providerName);

	/// <summary>
	/// Records a cache hit or miss.
	/// </summary>
	/// <param name="hit"> True if cache hit; false if cache miss. </param>
	/// <param name="cacheKey"> The cache key. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordCacheMetrics(bool hit, string cacheKey, string providerName);

	/// <summary>
	/// Records transaction metrics.
	/// </summary>
	/// <param name="duration"> The duration of the transaction. </param>
	/// <param name="committed"> Whether the transaction was committed. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordTransactionMetrics(TimeSpan duration, bool committed, string providerName);

	/// <summary>
	/// Records an error that occurred during a persistence operation.
	/// </summary>
	/// <param name="exception"> The exception that occurred. </param>
	/// <param name="operationType"> The type of operation that failed. </param>
	/// <param name="providerName"> The name of the persistence provider. </param>
	void RecordError(Exception exception, string operationType, string providerName);

	/// <summary>
	/// Starts an activity for distributed tracing.
	/// </summary>
	/// <param name="name"> The name of the activity. </param>
	/// <param name="tags"> Additional tags for the activity. </param>
	/// <returns> The started activity. </returns>
	Activity? StartActivity(string name, IDictionary<string, object?>? tags = null);

	/// <summary>
	/// Gets current metrics snapshot.
	/// </summary>
	/// <returns> Dictionary of current metric values. </returns>
	IDictionary<string, object> GetMetricsSnapshot();
}
