// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Abstractions.Diagnostics;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Metrics collector for Postgres persistence operations.
/// </summary>
public sealed class PostgresPersistenceMetrics : IDisposable
{
	private const string MeterName = "Excalibur.Data.Postgres.Persistence";
	private readonly Meter _meter;
	private readonly Counter<long> _totalQueries;
	private readonly Counter<long> _totalCommands;
	private readonly Counter<long> _totalTransactions;
	private readonly Counter<long> _failedQueries;
	private readonly Counter<long> _failedCommands;
	private readonly Counter<long> _failedTransactions;
	private readonly Counter<long> _connectionErrors;
	private readonly Counter<long> _timeouts;
	private readonly Counter<long> _deadlocks;
	private readonly Counter<long> _cacheHits;
	private readonly Counter<long> _cacheMisses;
	private readonly Histogram<double> _queryDuration;
	private readonly Histogram<double> _commandDuration;
	private readonly Histogram<double> _transactionDuration;
	private readonly Histogram<double> _connectionAcquisitionTime;

	private readonly ObservableGauge<int> _activeConnections;

	private readonly ObservableGauge<int> _idleConnections;

	private readonly ObservableGauge<int> _poolSize;

	private readonly ObservableGauge<double> _poolUtilization;

	private readonly ObservableGauge<long> _preparedStatementCount;

	private int _activeConnectionCount;
	private int _idleConnectionCount;
	private int _currentPoolSize;
	private long _currentPreparedStatements;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresPersistenceMetrics" /> class.
	/// </summary>
	public PostgresPersistenceMetrics()
	{
		_meter = new Meter(MeterName, "1.0.0");

		// Counters for operations
		_totalQueries = _meter.CreateCounter<long>(
			"postgres.queries.total",
			"queries",
			"Total number of queries executed");

		_totalCommands = _meter.CreateCounter<long>(
			"postgres.commands.total",
			"commands",
			"Total number of commands executed");

		_totalTransactions = _meter.CreateCounter<long>(
			"postgres.transactions.total",
			"transactions",
			"Total number of transactions");

		// Error counters
		_failedQueries = _meter.CreateCounter<long>(
			"postgres.queries.failed",
			"queries",
			"Total number of failed queries");

		_failedCommands = _meter.CreateCounter<long>(
			"postgres.commands.failed",
			"commands",
			"Total number of failed commands");

		_failedTransactions = _meter.CreateCounter<long>(
			"postgres.transactions.failed",
			"transactions",
			"Total number of failed transactions");

		_connectionErrors = _meter.CreateCounter<long>(
			"postgres.connections.errors",
			"errors",
			"Total number of connection errors");

		_timeouts = _meter.CreateCounter<long>(
			"postgres.timeouts.total",
			"timeouts",
			"Total number of operation timeouts");

		_deadlocks = _meter.CreateCounter<long>(
			"postgres.deadlocks.total",
			"deadlocks",
			"Total number of deadlocks detected");

		// Cache metrics
		_cacheHits = _meter.CreateCounter<long>(
			"postgres.cache.hits",
			"hits",
			"Total number of query cache hits");

		_cacheMisses = _meter.CreateCounter<long>(
			"postgres.cache.misses",
			"misses",
			"Total number of query cache misses");

		// Duration histograms
		_queryDuration = _meter.CreateHistogram<double>(
			"postgres.query.duration",
			"milliseconds",
			"Query execution duration");

		_commandDuration = _meter.CreateHistogram<double>(
			"postgres.command.duration",
			"milliseconds",
			"Command execution duration");

		_transactionDuration = _meter.CreateHistogram<double>(
			"postgres.transaction.duration",
			"milliseconds",
			"Transaction duration");

		_connectionAcquisitionTime = _meter.CreateHistogram<double>(
			"postgres.connection.acquisition.time",
			"milliseconds",
			"Time to acquire a connection from the pool");

		// Observable gauges for connection pool
		_activeConnections = _meter.CreateObservableGauge(
			"postgres.connections.active",
			() => _activeConnectionCount,
			"connections",
			"Number of active connections");

		_idleConnections = _meter.CreateObservableGauge(
			"postgres.connections.idle",
			() => _idleConnectionCount,
			"connections",
			"Number of idle connections");

		_poolSize = _meter.CreateObservableGauge(
			"postgres.pool.size",
			() => _currentPoolSize,
			"connections",
			"Current connection pool size");

		_poolUtilization = _meter.CreateObservableGauge(
			"postgres.pool.utilization",
			() => _currentPoolSize > 0 ? (double)_activeConnectionCount / _currentPoolSize * 100 : 0,
			"percent",
			"Connection pool utilization percentage");

		_preparedStatementCount = _meter.CreateObservableGauge(
			"postgres.prepared.statements",
			() => _currentPreparedStatements,
			"statements",
			"Number of prepared statements");
	}

	/// <summary>
	/// Measures the duration of an operation.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. </typeparam>
	/// <param name="operation"> The operation to measure. </param>
	/// <param name="recordAction"> Action to record the duration. </param>
	/// <returns> The result of the operation. </returns>
	public static T MeasureOperation<T>(Func<T> operation, Action<double> recordAction)
	{
		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			return operation();
		}
		finally
		{
			recordAction(stopwatch.Elapsed.TotalMilliseconds);
		}
	}

	/// <summary>
	/// Measures the duration of an async operation.
	/// </summary>
	/// <typeparam name="T"> The return type of the operation. </typeparam>
	/// <param name="operation"> The async operation to measure. </param>
	/// <param name="recordAction"> Action to record the duration. </param>
	/// <returns> The result of the operation. </returns>
	public static async Task<T> MeasureOperationAsync<T>(Func<Task<T>> operation, Action<double> recordAction)
	{
		var stopwatch = ValueStopwatch.StartNew();
		try
		{
			return await operation().ConfigureAwait(false);
		}
		finally
		{
			recordAction(stopwatch.Elapsed.TotalMilliseconds);
		}
	}

	/// <summary>
	/// Records a successful query execution.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	public void RecordQuery(double durationMs, params KeyValuePair<string, object?>[] tags)
	{
		_totalQueries.Add(1, tags);
		_queryDuration.Record(durationMs, tags);
	}

	/// <summary>
	/// Records a failed query execution.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	/// <param name="errorType"> The type of error. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	public void RecordFailedQuery(double durationMs, string errorType, params KeyValuePair<string, object?>[] tags)
	{
		var allTags = tags.Concat([new KeyValuePair<string, object?>("error_type", errorType)]).ToArray();
		_failedQueries.Add(1, allTags);
		_queryDuration.Record(durationMs, allTags);
	}

	/// <summary>
	/// Records a successful command execution.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	/// <param name="affectedRows"> The number of affected rows. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	public void RecordCommand(double durationMs, int affectedRows, params KeyValuePair<string, object?>[] tags)
	{
		var allTags = tags.Concat([new KeyValuePair<string, object?>("affected_rows", affectedRows)]).ToArray();
		_totalCommands.Add(1, allTags);
		_commandDuration.Record(durationMs, allTags);
	}

	/// <summary>
	/// Records a failed command execution.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	/// <param name="errorType"> The type of error. </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	public void RecordFailedCommand(double durationMs, string errorType, params KeyValuePair<string, object?>[] tags)
	{
		var allTags = tags.Concat([new KeyValuePair<string, object?>("error_type", errorType)]).ToArray();
		_failedCommands.Add(1, allTags);
		_commandDuration.Record(durationMs, allTags);
	}

	/// <summary>
	/// Records a transaction.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	/// <param name="status"> The transaction status (committed, rolled_back, failed). </param>
	/// <param name="tags"> Optional tags for the metric. </param>
	public void RecordTransaction(double durationMs, string status, params KeyValuePair<string, object?>[] tags)
	{
		var allTags = tags.Concat([new KeyValuePair<string, object?>("status", status)]).ToArray();
		_totalTransactions.Add(1, allTags);
		_transactionDuration.Record(durationMs, allTags);

		if (string.Equals(status, "failed", StringComparison.Ordinal))
		{
			_failedTransactions.Add(1, allTags);
		}
	}

	/// <summary>
	/// Records a connection error.
	/// </summary>
	/// <param name="errorType"> The type of error. </param>
	public void RecordConnectionError(string errorType) =>
		_connectionErrors.Add(1, new KeyValuePair<string, object?>("error_type", errorType));

	/// <summary>
	/// Records a timeout.
	/// </summary>
	/// <param name="operationType"> The type of operation that timed out. </param>
	public void RecordTimeout(string operationType) => _timeouts.Add(1, new KeyValuePair<string, object?>("operation", operationType));

	/// <summary>
	/// Records a deadlock.
	/// </summary>
	public void RecordDeadlock() => _deadlocks.Add(1);

	/// <summary>
	/// Records connection acquisition time.
	/// </summary>
	/// <param name="durationMs"> The duration in milliseconds. </param>
	public void RecordConnectionAcquisition(double durationMs) => _connectionAcquisitionTime.Record(durationMs);

	/// <summary>
	/// Records a cache hit.
	/// </summary>
	/// <param name="cacheType"> The type of cache (query_plan, result, etc.). </param>
	public void RecordCacheHit(string cacheType) => _cacheHits.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));

	/// <summary>
	/// Records a cache miss.
	/// </summary>
	/// <param name="cacheType"> The type of cache (query_plan, result, etc.). </param>
	public void RecordCacheMiss(string cacheType) => _cacheMisses.Add(1, new KeyValuePair<string, object?>("cache_type", cacheType));

	/// <summary>
	/// Updates connection pool statistics.
	/// </summary>
	/// <param name="activeConnections"> Number of active connections. </param>
	/// <param name="idleConnections"> Number of idle connections. </param>
	/// <param name="poolSize"> Total pool size. </param>
	public void UpdateConnectionPoolStats(int activeConnections, int idleConnections, int poolSize)
	{
		_activeConnectionCount = activeConnections;
		_idleConnectionCount = idleConnections;
		_currentPoolSize = poolSize;
	}

	/// <summary>
	/// Updates the prepared statement count.
	/// </summary>
	/// <param name="count"> The number of prepared statements. </param>
	public void UpdatePreparedStatementCount(long count) => _currentPreparedStatements = count;

	/// <summary>
	/// Gets current metrics as a dictionary.
	/// </summary>
	/// <returns> Dictionary of metric names and values. </returns>
	public IDictionary<string, object> GetCurrentMetrics() =>
		new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["active_connections"] = _activeConnectionCount,
			["idle_connections"] = _idleConnectionCount,
			["pool_size"] = _currentPoolSize,
			["pool_utilization"] = _currentPoolSize > 0 ? (double)_activeConnectionCount / _currentPoolSize * 100 : 0,
			["prepared_statements"] = _currentPreparedStatements,
		};

	/// <inheritdoc />
	public void Dispose() => _meter?.Dispose();
}
