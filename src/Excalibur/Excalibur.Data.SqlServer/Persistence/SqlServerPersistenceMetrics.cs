// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Metrics collector for SQL Server persistence operations.
/// </summary>
public class SqlServerPersistenceMetrics : IDisposable
{
	private readonly Meter _meter;
	private readonly SqlServerPersistenceOptions _options;

	/// <summary>
	/// Counters
	/// </summary>
	private readonly Counter<long> _connectionsCreated;

	private readonly Counter<long> _queriesExecuted;
	private readonly Counter<long> _commandsExecuted;
	private readonly Counter<long> _transactionsStarted;
	private readonly Counter<long> _transactionsCommitted;
	private readonly Counter<long> _transactionsRolledBack;
	private readonly Counter<long> _retryCount;
	private readonly Counter<long> _errorCount;
	private readonly Counter<long> _deadlockCount;

	/// <summary>
	/// Histograms
	/// </summary>
	private readonly Histogram<double> _queryDuration;

	private readonly Histogram<double> _commandDuration;
	private readonly Histogram<double> _transactionDuration;
	private readonly Histogram<double> _connectionWaitTime;
	private readonly Histogram<double> _batchSize;

	/// <summary>
	/// Gauges
	/// </summary>
	private readonly ConcurrentDictionary<string, long> _activeConnections;

	private readonly ConcurrentDictionary<string, long> _activeTransactions;

	// IDE0052: ObservableGauge must be stored as field to prevent GC while callbacks are registered
#pragma warning disable IDE0052
	private readonly ObservableGauge<long> _activeConnectionsGauge;
	private readonly ObservableGauge<long> _activeTransactionsGauge;
#pragma warning restore IDE0052

	/// <summary>
	/// CDC Metrics
	/// </summary>
	private readonly Counter<long> _cdcEventsProcessed;

	private readonly Histogram<double> _cdcProcessingDuration;

	// IDE0052: ObservableGauge must be stored as field to prevent GC while callbacks are registered
#pragma warning disable IDE0052
	private readonly ObservableGauge<long> _cdcLagSeconds;
#pragma warning restore IDE0052

	/// <summary>
	/// Cache Metrics
	/// </summary>
	private readonly Counter<long> _cacheHits;

	private readonly Counter<long> _cacheMisses;

	// IDE0052: ObservableGauge must be stored as field to prevent GC while callbacks are registered
#pragma warning disable IDE0052
	private readonly ObservableGauge<double> _cacheHitRatio;
#pragma warning restore IDE0052
	private long _currentCdcLag;
	private long _totalCacheRequests;
	private long _totalCacheHits;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerPersistenceMetrics" /> class.
	/// </summary>
	/// <param name="options"> The persistence options. </param>
	public SqlServerPersistenceMetrics(IOptions<SqlServerPersistenceOptions> options)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));

		_meter = new Meter(SqlServerPersistenceTelemetryConstants.MeterName, SqlServerPersistenceTelemetryConstants.Version);

		_activeConnections = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);
		_activeTransactions = new ConcurrentDictionary<string, long>(StringComparer.Ordinal);

		// Initialize counters
		_connectionsCreated = _meter.CreateCounter<long>(
			"sqlserver.connections.created",
			"connections",
			"Total number of SQL Server connections created");

		_queriesExecuted = _meter.CreateCounter<long>(
			"sqlserver.queries.executed",
			"queries",
			"Total number of queries executed");

		_commandsExecuted = _meter.CreateCounter<long>(
			"sqlserver.commands.executed",
			"commands",
			"Total number of commands executed");

		_transactionsStarted = _meter.CreateCounter<long>(
			"sqlserver.transactions.started",
			"transactions",
			"Total number of transactions started");

		_transactionsCommitted = _meter.CreateCounter<long>(
			"sqlserver.transactions.committed",
			"transactions",
			"Total number of transactions committed");

		_transactionsRolledBack = _meter.CreateCounter<long>(
			"sqlserver.transactions.rolledback",
			"transactions",
			"Total number of transactions rolled back");

		_retryCount = _meter.CreateCounter<long>(
			"sqlserver.retries",
			"retries",
			"Total number of retry attempts");

		_errorCount = _meter.CreateCounter<long>(
			"sqlserver.errors",
			"errors",
			"Total number of errors");

		_deadlockCount = _meter.CreateCounter<long>(
			"sqlserver.deadlocks",
			"deadlocks",
			"Total number of deadlocks detected");

		// Initialize histograms
		_queryDuration = _meter.CreateHistogram<double>(
			"sqlserver.query.duration",
			"milliseconds",
			"Duration of query execution");

		_commandDuration = _meter.CreateHistogram<double>(
			"sqlserver.command.duration",
			"milliseconds",
			"Duration of command execution");

		_transactionDuration = _meter.CreateHistogram<double>(
			"sqlserver.transaction.duration",
			"milliseconds",
			"Duration of transaction execution");

		_connectionWaitTime = _meter.CreateHistogram<double>(
			"sqlserver.connection.wait_time",
			"milliseconds",
			"Time spent waiting for a connection");

		_batchSize = _meter.CreateHistogram<double>(
			"sqlserver.batch.size",
			"commands",
			"Number of commands in a batch");

		// Initialize gauges
		_activeConnectionsGauge = _meter.CreateObservableGauge(
			"sqlserver.connections.active",
			_activeConnections.Values.Sum,
			"connections",
			"Number of active SQL Server connections");

		_activeTransactionsGauge = _meter.CreateObservableGauge(
			"sqlserver.transactions.active",
			_activeTransactions.Values.Sum,
			"transactions",
			"Number of active transactions");

		// CDC metrics
		_cdcEventsProcessed = _meter.CreateCounter<long>(
			"sqlserver.cdc.events.processed",
			"events",
			"Total number of CDC events processed");

		_cdcProcessingDuration = _meter.CreateHistogram<double>(
			"sqlserver.cdc.processing.duration",
			"milliseconds",
			"Duration of CDC event processing");

		_cdcLagSeconds = _meter.CreateObservableGauge(
			"sqlserver.cdc.lag",
			() => _currentCdcLag,
			"seconds",
			"CDC processing lag in seconds");

		// Cache metrics
		_cacheHits = _meter.CreateCounter<long>(
			"sqlserver.cache.hits",
			"hits",
			"Total number of cache hits");

		_cacheMisses = _meter.CreateCounter<long>(
			"sqlserver.cache.misses",
			"misses",
			"Total number of cache misses");

		_cacheHitRatio = _meter.CreateObservableGauge(
			"sqlserver.cache.hit_ratio",
			() => _totalCacheRequests > 0 ? (double)_totalCacheHits / _totalCacheRequests : 0,
			"ratio",
			"Cache hit ratio");
	}

	/// <summary>
	/// Records that a connection was created.
	/// </summary>
	public void RecordConnectionCreated()
	{
		if (_options.EnableMetrics)
		{
			_connectionsCreated.Add(1);
			_ = _activeConnections.AddOrUpdate(Guid.NewGuid().ToString(), 1, static (_, v) => v + 1);
		}
	}

	/// <summary>
	/// Records that a connection was closed.
	/// </summary>
	public void RecordConnectionClosed(string connectionId)
	{
		if (_options.EnableMetrics && !string.IsNullOrEmpty(connectionId))
		{
			_ = _activeConnections.TryRemove(connectionId, out _);
		}
	}

	/// <summary>
	/// Records a query execution.
	/// </summary>
	public void RecordQueryExecution(long durationMs, bool success)
	{
		if (_options.EnableMetrics)
		{
			_queriesExecuted.Add(1, new KeyValuePair<string, object?>("success", success));
			_queryDuration.Record(durationMs);

			if (!success)
			{
				_errorCount.Add(1, new KeyValuePair<string, object?>("type", "query"));
			}
		}
	}

	/// <summary>
	/// Records a command execution.
	/// </summary>
	public void RecordCommandExecution(long durationMs, bool success, int affectedRows)
	{
		if (_options.EnableMetrics)
		{
			_commandsExecuted.Add(
				1,
				new KeyValuePair<string, object?>("success", success),
				new KeyValuePair<string, object?>("affected_rows", affectedRows));
			_commandDuration.Record(durationMs);

			if (!success)
			{
				_errorCount.Add(1, new KeyValuePair<string, object?>("type", "command"));
			}
		}
	}

	/// <summary>
	/// Records a batch execution.
	/// </summary>
	public void RecordBatchExecution(long durationMs, bool success, int commandCount, int affectedRows)
	{
		if (_options.EnableMetrics)
		{
			_batchSize.Record(commandCount);
			_commandsExecuted.Add(
				commandCount,
				new KeyValuePair<string, object?>("success", success),
				new KeyValuePair<string, object?>("batch", value: true),
				new KeyValuePair<string, object?>("affected_rows", affectedRows));
			_commandDuration.Record(durationMs);

			if (!success)
			{
				_errorCount.Add(1, new KeyValuePair<string, object?>("type", "batch"));
			}
		}
	}

	/// <summary>
	/// Records a data request execution.
	/// </summary>
	public void RecordDataRequestExecution(long durationMs, bool success)
	{
		if (_options.EnableMetrics)
		{
			_queriesExecuted.Add(
				1,
				new KeyValuePair<string, object?>("success", success),
				new KeyValuePair<string, object?>("type", "data_request"));
			_queryDuration.Record(durationMs);

			if (!success)
			{
				_errorCount.Add(1, new KeyValuePair<string, object?>("type", "data_request"));
			}
		}
	}

	/// <summary>
	/// Records that a transaction was started.
	/// </summary>
	public void RecordTransactionStarted()
	{
		if (_options.EnableMetrics)
		{
			_transactionsStarted.Add(1);
			_ = _activeTransactions.AddOrUpdate(Guid.NewGuid().ToString(), 1, static (_, v) => v + 1);
		}
	}

	/// <summary>
	/// Records that a transaction was committed.
	/// </summary>
	public void RecordTransactionCommitted(string transactionId, long durationMs)
	{
		if (_options.EnableMetrics)
		{
			_transactionsCommitted.Add(1);
			_transactionDuration.Record(durationMs);

			if (!string.IsNullOrEmpty(transactionId))
			{
				_ = _activeTransactions.TryRemove(transactionId, out _);
			}
		}
	}

	/// <summary>
	/// Records that a transaction was rolled back.
	/// </summary>
	public void RecordTransactionRolledBack(string transactionId, long durationMs)
	{
		if (_options.EnableMetrics)
		{
			_transactionsRolledBack.Add(1);
			_transactionDuration.Record(durationMs);

			if (!string.IsNullOrEmpty(transactionId))
			{
				_ = _activeTransactions.TryRemove(transactionId, out _);
			}
		}
	}

	/// <summary>
	/// Records a retry attempt.
	/// </summary>
	public void RecordRetry()
	{
		if (_options.EnableMetrics)
		{
			_retryCount.Add(1);
		}
	}

	/// <summary>
	/// Records a deadlock.
	/// </summary>
	public void RecordDeadlock()
	{
		if (_options.EnableMetrics)
		{
			_deadlockCount.Add(1);
			_errorCount.Add(1, new KeyValuePair<string, object?>("type", "deadlock"));
		}
	}

	/// <summary>
	/// Records connection wait time.
	/// </summary>
	public void RecordConnectionWaitTime(long waitTimeMs)
	{
		if (_options.EnableMetrics)
		{
			_connectionWaitTime.Record(waitTimeMs);
		}
	}

	/// <summary>
	/// Records CDC event processing.
	/// </summary>
	public void RecordCdcEventProcessed(long durationMs, int eventCount)
	{
		if (_options.EnableMetrics)
		{
			_cdcEventsProcessed.Add(eventCount);
			_cdcProcessingDuration.Record(durationMs);
		}
	}

	/// <summary>
	/// Updates the CDC lag metric.
	/// </summary>
	public void UpdateCdcLag(long lagSeconds)
	{
		if (_options.EnableMetrics)
		{
			_currentCdcLag = lagSeconds;
		}
	}

	/// <summary>
	/// Records a cache hit.
	/// </summary>
	public void RecordCacheHit()
	{
		if (_options.EnableMetrics)
		{
			_cacheHits.Add(1);
			_ = Interlocked.Increment(ref _totalCacheHits);
			_ = Interlocked.Increment(ref _totalCacheRequests);
		}
	}

	/// <summary>
	/// Records a cache miss.
	/// </summary>
	public void RecordCacheMiss()
	{
		if (_options.EnableMetrics)
		{
			_cacheMisses.Add(1);
			_ = Interlocked.Increment(ref _totalCacheRequests);
		}
	}

	/// <summary>
	/// Records a health check execution.
	/// </summary>
	public void RecordHealthCheck(long durationMs, bool healthy)
	{
		if (_options.EnableMetrics)
		{
			var tags = new[]
			{
				new KeyValuePair<string, object?>("check_type", "persistence"), new KeyValuePair<string, object?>("healthy", healthy),
			};

			_queryDuration.Record(durationMs, tags);
		}
	}

	/// <summary>
	/// Gets the current metrics as a dictionary.
	/// </summary>
	/// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
	public async Task<IDictionary<string, object>> GetMetricsAsync()
	{
		var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["connections.active"] = _activeConnections.Values.Sum(),
			["transactions.active"] = _activeTransactions.Values.Sum(),
			["cache.hit_ratio"] = _totalCacheRequests > 0 ? (double)_totalCacheHits / _totalCacheRequests : 0,
			["cdc.lag_seconds"] = _currentCdcLag,

			// Add configuration metrics
			["config.max_pool_size"] = _options.MaxPoolSize,
			["config.command_timeout"] = _options.CommandTimeout,
			["config.max_retry_attempts"] = _options.MaxRetryAttempts,
			["config.pooling_enabled"] = _options.EnableConnectionPooling,
		};

		return await Task.FromResult(metrics).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Releases the unmanaged resources used by the <see cref="SqlServerPersistenceMetrics"/> and optionally releases the managed resources.
	/// </summary>
	/// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
	protected virtual void Dispose(bool disposing)
	{
		if (disposing)
		{
			_meter?.Dispose();
		}
	}
}
