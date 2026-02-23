// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.Persistence;

/// <summary>
/// Default implementation of persistence metrics collection.
/// </summary>
internal sealed class DefaultPersistenceMetrics : IPersistenceMetrics, IDisposable
{
	private readonly Counter<long> _queryCounter;
	private readonly Counter<long> _commandCounter;
	private readonly Counter<long> _errorCounter;
	private readonly Counter<long> _rowsAffectedCounter;
	private readonly Counter<long> _cacheHitCounter;
	private readonly Counter<long> _cacheMissCounter;
	private readonly Histogram<double> _queryDuration;
	private readonly Histogram<double> _transactionDuration;
	// R0.8: Remove unread private members - Gauge fields must be stored to prevent GC
#pragma warning disable IDE0052
	private readonly ObservableGauge<int> _activeConnectionsGauge;
	private readonly ObservableGauge<int> _idleConnectionsGauge;
#pragma warning restore IDE0052
	private readonly ConcurrentDictionary<string, long> _customMetrics = new(StringComparer.Ordinal);
	private readonly ActivitySource _activitySource;
	private int _activeConnections;
	private int _idleConnections;

	/// <summary>
	/// Initializes a new instance of the <see cref="DefaultPersistenceMetrics" /> class.
	/// </summary>
	public DefaultPersistenceMetrics()
	{
		Meter = new Meter(PersistenceTelemetryConstants.SourceName, PersistenceTelemetryConstants.Version);
		_activitySource = new ActivitySource(PersistenceTelemetryConstants.SourceName, PersistenceTelemetryConstants.Version);

		_queryCounter = Meter.CreateCounter<long>(
			"persistence_queries_total",
			description: "Total number of persistence queries executed");

		_commandCounter = Meter.CreateCounter<long>(
			"persistence_commands_total",
			description: "Total number of persistence commands executed");

		_errorCounter = Meter.CreateCounter<long>(
			"persistence_errors_total",
			description: "Total number of persistence errors");

		_rowsAffectedCounter = Meter.CreateCounter<long>(
			"persistence_rows_affected_total",
			description: "Total number of rows affected by commands");

		_cacheHitCounter = Meter.CreateCounter<long>(
			"persistence_cache_hits_total",
			description: "Total number of cache hits");

		_cacheMissCounter = Meter.CreateCounter<long>(
			"persistence_cache_misses_total",
			description: "Total number of cache misses");

		_queryDuration = Meter.CreateHistogram<double>(
			"persistence_query_duration_ms",
			unit: "ms",
			description: "Duration of persistence queries in milliseconds");

		_transactionDuration = Meter.CreateHistogram<double>(
			"persistence_transaction_duration_ms",
			unit: "ms",
			description: "Duration of persistence transactions in milliseconds");

		_activeConnectionsGauge = Meter.CreateObservableGauge(
			"persistence_connections_active",
			() => _activeConnections,
			description: "Number of active database connections");

		_idleConnectionsGauge = Meter.CreateObservableGauge(
			"persistence_connections_idle",
			() => _idleConnections,
			description: "Number of idle database connections");
	}

	/// <inheritdoc />
	public Meter Meter { get; }

	/// <inheritdoc />
	public void RecordQueryDuration(TimeSpan duration, string queryType, bool success, string providerName)
	{
		var tags = new[]
		{
			new KeyValuePair<string, object?>("query_type", queryType), new KeyValuePair<string, object?>("success", success),
			new KeyValuePair<string, object?>("provider", providerName),
		};

		_queryCounter.Add(1, tags);
		_queryDuration.Record(duration.TotalMilliseconds, tags);

		if (!success)
		{
			_errorCounter.Add(1, tags);
		}

		IncrementCustomMetric($"queries_{providerName}");
	}

	/// <inheritdoc />
	public void RecordRowsAffected(int rowCount, string commandType, string providerName)
	{
		var tags = new[]
		{
			new KeyValuePair<string, object?>("command_type", commandType), new KeyValuePair<string, object?>("provider", providerName),
		};

		_rowsAffectedCounter.Add(rowCount, tags);
		_commandCounter.Add(1, tags);

		IncrementCustomMetric($"commands_{providerName}");
	}

	/// <inheritdoc />
	public void RecordConnectionPoolMetrics(int activeConnections, int idleConnections, string providerName)
	{
		_activeConnections = activeConnections;
		_idleConnections = idleConnections;

		_customMetrics[$"connections_active_{providerName}"] = activeConnections;
		_customMetrics[$"connections_idle_{providerName}"] = idleConnections;
	}

	/// <inheritdoc />
	public void RecordCacheMetrics(bool hit, string cacheKey, string providerName)
	{
		var tags = new[]
		{
			new KeyValuePair<string, object?>("cache_key", cacheKey), new KeyValuePair<string, object?>("provider", providerName),
		};

		if (hit)
		{
			_cacheHitCounter.Add(1, tags);
			IncrementCustomMetric("cache_hits");
		}
		else
		{
			_cacheMissCounter.Add(1, tags);
			IncrementCustomMetric("cache_misses");
		}
	}

	/// <inheritdoc />
	public void RecordTransactionMetrics(TimeSpan duration, bool committed, string providerName)
	{
		var tags = new[]
		{
			new KeyValuePair<string, object?>("committed", committed), new KeyValuePair<string, object?>("provider", providerName),
		};

		_transactionDuration.Record(duration.TotalMilliseconds, tags);

		if (!committed)
		{
			_errorCounter.Add(1, new KeyValuePair<string, object?>("error_type", "transaction_rollback"));
		}

		IncrementCustomMetric($"transactions_{providerName}");
	}

	/// <inheritdoc />
	public void RecordError(Exception exception, string operationType, string providerName)
	{
		var tags = new[]
		{
			new KeyValuePair<string, object?>("operation_type", operationType),
			new KeyValuePair<string, object?>("exception_type", exception.GetType().Name),
			new KeyValuePair<string, object?>("provider", providerName),
		};

		_errorCounter.Add(1, tags);
		IncrementCustomMetric($"errors_{providerName}");
	}

	/// <inheritdoc />
	public Activity? StartActivity(string name, IDictionary<string, object?>? tags = null)
	{
		var activity = _activitySource.StartActivity(name);

		if (activity != null && tags != null)
		{
			foreach (var tag in tags)
			{
				_ = activity.SetTag(tag.Key, tag.Value);
			}
		}

		return activity;
	}

	/// <inheritdoc />
	public IDictionary<string, object> GetMetricsSnapshot()
	{
		var snapshot = new Dictionary<string, object>(StringComparer.Ordinal);

		foreach (var metric in _customMetrics)
		{
			snapshot[metric.Key] = metric.Value;
		}

		snapshot["active_connections"] = _activeConnections;
		snapshot["idle_connections"] = _idleConnections;

		return snapshot;
	}

	/// <inheritdoc />
	public void RecordQuery(string queryType, TimeSpan duration, bool success) =>
		RecordQueryDuration(duration, queryType, success, "default");

	/// <inheritdoc />
	// R0.8: Remove unused parameter - interface contract requires duration and success parameters even though this implementation delegates to RecordRowsAffected
#pragma warning disable IDE0060

	public void RecordCommand(string commandType, TimeSpan duration, bool success) => RecordRowsAffected(0, commandType, "default");

#pragma warning restore IDE0060

	/// <inheritdoc />
	public void RecordConnectionOpen(string providerName) => IncrementCustomMetric($"connection_open_{providerName}");

	/// <inheritdoc />
	public void RecordConnectionClose(string providerName) => IncrementCustomMetric($"connection_close_{providerName}");

	/// <inheritdoc />
	public void RecordCacheHit(string cacheKey) => RecordCacheMetrics(hit: true, cacheKey, "default");

	/// <inheritdoc />
	public void RecordCacheMiss(string cacheKey) => RecordCacheMetrics(hit: false, cacheKey, "default");

	/// <inheritdoc />
	public void RecordError(string errorType, Exception exception) => RecordError(exception, errorType, "default");

	/// <inheritdoc />
	public IDisposable BeginTimedOperation(string operationName) => new TimedOperation(this, operationName);

	/// <inheritdoc />
	/// <inheritdoc />
	public PersistenceMetricsSnapshot GetSnapshot()
	{
		var snapshot = new PersistenceMetricsSnapshot
		{
			TotalQueries = GetCustomMetric("queries_default"),
			TotalCommands = GetCustomMetric("commands_default"),
			TotalErrors = GetCustomMetric("errors_default"),
			CacheHits = GetCustomMetric("cache_hits"),
			CacheMisses = GetCustomMetric("cache_misses"),
		};

		// Populate custom metrics from the internal dictionary
		foreach (var kvp in _customMetrics)
		{
			snapshot.CustomMetrics[kvp.Key] = kvp.Value;
		}

		return snapshot;
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Meter?.Dispose();
		_activitySource?.Dispose();
	}

	private void IncrementCustomMetric(string name) => _ = _customMetrics.AddOrUpdate(name, 1, static (_, count) => count + 1);

	private long GetCustomMetric(string name) => _customMetrics.GetValueOrDefault(name, 0);

	/// <summary>
	/// Represents a timed operation for metrics collection.
	/// </summary>
	private sealed class TimedOperation(DefaultPersistenceMetrics metrics, string operationName) : IDisposable
	{
		private readonly long _startTimestamp = Stopwatch.GetTimestamp();

		public void Dispose()
		{
			metrics._queryDuration.Record(
				Stopwatch.GetElapsedTime(_startTimestamp).TotalMilliseconds,
				new KeyValuePair<string, object?>("operation", operationName));
		}
	}
}
