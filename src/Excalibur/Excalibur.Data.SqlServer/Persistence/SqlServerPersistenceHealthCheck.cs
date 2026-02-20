// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;
using System.Globalization;

using Dapper;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// Health check implementation for SQL Server persistence provider.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="SqlServerPersistenceHealthCheck" /> class. </remarks>
/// <param name="options"> The persistence options. </param>
/// <param name="logger"> The logger instance. </param>
/// <param name="metrics"> The metrics collector. </param>
public partial class SqlServerPersistenceHealthCheck(
	IOptions<SqlServerPersistenceOptions> options,
	ILogger<SqlServerPersistenceHealthCheck> logger,
	SqlServerPersistenceMetrics metrics) : IHealthCheck
{
	private readonly SqlServerPersistenceOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<SqlServerPersistenceHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));
	private readonly SqlServerPersistenceMetrics _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			var connection = new SqlConnection(_options.ConnectionString);
			await using (connection.ConfigureAwait(false))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				// Basic connectivity check
				var basicCheckResult = await connection.ExecuteScalarAsync<int>(
				"SELECT 1",
				commandTimeout: _options.CommandTimeout).ConfigureAwait(false);

				if (basicCheckResult != 1)
				{
					return HealthCheckResult.Unhealthy("Basic connectivity check failed");
				}

				data["connectivity"] = "OK";
				data["response_time_ms"] = stopwatch.ElapsedMilliseconds;

				// Get server version and database info
				var serverInfo = await connection.QuerySingleAsync<dynamic>("""
			                                                             SELECT
			                                                             @@VERSION AS ServerVersion,
			                                                             @@SERVERNAME AS ServerName,
			                                                             DB_NAME() AS DatabaseName,
			                                                             SUSER_SNAME() AS CurrentUser,
			                                                             @@SPID AS SessionId

			                                                            """).ConfigureAwait(false);

				data["server_name"] = serverInfo.ServerName;
				data["database_name"] = serverInfo.DatabaseName;
				data["current_user"] = serverInfo.CurrentUser;
				data["session_id"] = serverInfo.SessionId;

				// Check connection pool statistics
				var poolStats = await CheckConnectionPoolAsync(connection, cancellationToken).ConfigureAwait(false);
				foreach (var stat in poolStats)
				{
					data[$"pool_{stat.Key}"] = stat.Value;
				}

				// Check for blocking queries
				var blockingCheck = await CheckBlockingQueriesAsync(connection, cancellationToken).ConfigureAwait(false);
				data["blocking_queries_count"] = blockingCheck.BlockingCount;
				data["blocked_queries_count"] = blockingCheck.BlockedCount;

				if (blockingCheck.BlockingCount > 10)
				{
					return HealthCheckResult.Degraded(
						$"High number of blocking queries detected: {blockingCheck.BlockingCount.ToString(CultureInfo.InvariantCulture)}",
						exception: null,
						data);
				}

				// Check for deadlocks
				var deadlockCount = await CheckDeadlocksAsync(connection, cancellationToken).ConfigureAwait(false);
				data["recent_deadlocks"] = deadlockCount;

				if (deadlockCount > 5)
				{
					return HealthCheckResult.Degraded(
						$"Recent deadlocks detected: {deadlockCount.ToString(CultureInfo.InvariantCulture)}",
						exception: null,
						data);
				}

				// Check query performance
				var performanceCheck = await CheckQueryPerformanceAsync(connection, cancellationToken).ConfigureAwait(false);
				data["avg_query_time_ms"] = performanceCheck.AverageQueryTime;
				data["slow_query_count"] = performanceCheck.SlowQueryCount;

				if (performanceCheck.SlowQueryCount > 20)
				{
					return HealthCheckResult.Degraded(
						$"High number of slow queries: {performanceCheck.SlowQueryCount.ToString(CultureInfo.InvariantCulture)}",
						exception: null,
						data);
				}

				// Check database size and growth
				var sizeCheck = await CheckDatabaseSizeAsync(connection, cancellationToken).ConfigureAwait(false);
				data["database_size_mb"] = sizeCheck.DataSizeMB;
				data["log_size_mb"] = sizeCheck.LogSizeMB;
				data["space_used_percent"] = sizeCheck.SpaceUsedPercent;

				if (sizeCheck.SpaceUsedPercent > 90)
				{
					return HealthCheckResult.Degraded(
						$"Database space usage is high: {sizeCheck.SpaceUsedPercent.ToString("F1", CultureInfo.InvariantCulture)}%",
						exception: null,
						data);
				}

				// Check CDC status if applicable
				var cdcStatus = await CheckCdcStatusAsync(connection, cancellationToken).ConfigureAwait(false);
				if (cdcStatus != null)
				{
					data["cdc_enabled"] = cdcStatus.IsEnabled;
					data["cdc_tables_count"] = cdcStatus.TableCount;
					data["cdc_lag_seconds"] = cdcStatus.LagSeconds;

					if (cdcStatus is { IsEnabled: true, LagSeconds: > 300 })
					{
						return HealthCheckResult.Degraded(
							$"CDC lag is high: {cdcStatus.LagSeconds.ToString(CultureInfo.InvariantCulture)} seconds",
							exception: null,
							data);
					}
				}

				// Record metrics
				_metrics.RecordHealthCheck(stopwatch.ElapsedMilliseconds, healthy: true);

				stopwatch.Stop();
				data["total_check_time_ms"] = stopwatch.ElapsedMilliseconds;

				return HealthCheckResult.Healthy("SQL Server persistence provider is healthy", data);
			}
		}
		catch (SqlException sqlEx)
		{
			LogHealthCheckFailed(sqlEx);
			_metrics.RecordHealthCheck(stopwatch.ElapsedMilliseconds, healthy: false);

			data["error"] = sqlEx.Message;
			data["error_number"] = sqlEx.Number;
			data["error_severity"] = sqlEx.Class;

			return HealthCheckResult.Unhealthy(
				$"SQL Server connection failed: {sqlEx.Message}",
				sqlEx,
				data);
		}
		catch (Exception ex)
		{
			LogUnexpectedHealthCheckError(ex);
			_metrics.RecordHealthCheck(stopwatch.ElapsedMilliseconds, healthy: false);

			data["error"] = ex.Message;

			return HealthCheckResult.Unhealthy(
				$"Health check failed: {ex.Message}",
				ex,
				data);
		}
	}

	private async Task<Dictionary<string, object>> CheckConnectionPoolAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		var stats = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			var poolStats = await connection.QuerySingleOrDefaultAsync<dynamic>("""
			                                                                     SELECT
			                                                                     COUNT(*) AS ActiveConnections
			                                                                     FROM sys.dm_exec_connections
			                                                                     WHERE client_net_address IS NOT NULL

			                                                                    """).ConfigureAwait(false);

			stats["active_connections"] = poolStats?.ActiveConnections ?? 0;

			// Get connection pool statistics from SQL Server
			var perfCounters = await connection.QueryAsync<dynamic>("""
			                                                         SELECT
			                                                         counter_name,
			                                                         cntr_value
			                                                         FROM sys.dm_os_performance_counters
			                                                         WHERE object_name LIKE '%:General Statistics%'
			                                                         AND counter_name IN ('User Connections', 'Logins/sec', 'Logouts/sec')

			                                                        """).ConfigureAwait(false);

			foreach (var counter in perfCounters)
			{
				var name = ((string)counter.counter_name).Replace(' ', '_').ToLower(CultureInfo.CurrentCulture);
				stats[name] = counter.cntr_value;
			}
		}
		catch (Exception ex)
		{
			LogConnectionPoolStatsError(ex);
		}

		return stats;
	}

	private async Task<(int BlockingCount, int BlockedCount)> CheckBlockingQueriesAsync(
		SqlConnection connection,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		try
		{
			var blockingInfo = await connection.QuerySingleOrDefaultAsync<dynamic>("""
			                                                                        SELECT
			                                                                        COUNT(DISTINCT blocking_session_id) AS BlockingCount,
			                                                                        COUNT(*) AS BlockedCount
			                                                                        FROM sys.dm_exec_requests
			                                                                        WHERE blocking_session_id > 0

			                                                                       """).ConfigureAwait(false);

			return (blockingInfo?.BlockingCount ?? 0, blockingInfo?.BlockedCount ?? 0);
		}
		catch (Exception ex)
		{
			LogBlockingQueriesError(ex);
			return (0, 0);
		}
	}

	private async Task<int> CheckDeadlocksAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		try
		{
			// Check for recent deadlocks in the last hour
			return await connection.ExecuteScalarAsync<int>("""
			                                                              SELECT COUNT(*)
			                                                              FROM sys.dm_os_performance_counters
			                                                              WHERE object_name LIKE '%:Locks%'
			                                                              AND counter_name = 'Number of Deadlocks/sec'
			                                                              AND cntr_value > 0

			                                                             """).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogDeadlocksError(ex);
			return 0;
		}
	}

	private async Task<(double AverageQueryTime, int SlowQueryCount)> CheckQueryPerformanceAsync(
		SqlConnection connection,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		try
		{
			var perfStats = await connection.QuerySingleOrDefaultAsync<dynamic>("""
			                                                                     SELECT
			                                                                     AVG(total_elapsed_time / 1000.0) AS AvgQueryTimeMs,
			                                                                     COUNT(*) AS SlowQueryCount
			                                                                     FROM sys.dm_exec_query_stats
			                                                                     WHERE last_execution_time > DATEADD(MINUTE, -5, GETDATE())
			                                                                     AND total_elapsed_time / 1000.0 > 1000 -- Queries taking more than 1 second

			                                                                    """).ConfigureAwait(false);

			return (perfStats?.AvgQueryTimeMs ?? 0, perfStats?.SlowQueryCount ?? 0);
		}
		catch (Exception ex)
		{
			LogQueryPerformanceError(ex);
			return (0, 0);
		}
	}

	private async Task<(decimal DataSizeMB, decimal LogSizeMB, decimal SpaceUsedPercent)> CheckDatabaseSizeAsync(
		SqlConnection connection,
		CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		try
		{
			var sizeInfo = await connection.QuerySingleOrDefaultAsync<dynamic>("""
			                                                                    SELECT
			                                                                    SUM(CASE WHEN type = 0 THEN size * 8.0 / 1024 ELSE 0 END) AS DataSizeMB,
			                                                                    SUM(CASE WHEN type = 1 THEN size * 8.0 / 1024 ELSE 0 END) AS LogSizeMB,
			                                                                    (SUM(FILEPROPERTY(name, 'SpaceUsed')) * 100.0) / SUM(size) AS SpaceUsedPercent
			                                                                    FROM sys.database_files

			                                                                   """).ConfigureAwait(false);

			return (
					sizeInfo?.DataSizeMB ?? 0,
					sizeInfo?.LogSizeMB ?? 0,
					sizeInfo?.SpaceUsedPercent ?? 0
				);
		}
		catch (Exception ex)
		{
			LogDatabaseSizeError(ex);
			return (0, 0, 0);
		}
	}

	private async Task<CdcStatus?> CheckCdcStatusAsync(SqlConnection connection, CancellationToken cancellationToken)
	{
		_ = cancellationToken;
		try
		{
			// Check if CDC is enabled for the database
			var cdcEnabled = await connection.ExecuteScalarAsync<int>("""
			                                                           SELECT is_cdc_enabled
			                                                           FROM sys.databases
			                                                           WHERE database_id = DB_ID()

			                                                          """).ConfigureAwait(false);

			if (cdcEnabled != 1)
			{
				return null;
			}

			var cdcInfo = await connection.QuerySingleOrDefaultAsync<dynamic>("""
			                                                                   SELECT
			                                                                   COUNT(*) AS TableCount,
			                                                                   DATEDIFF(SECOND, MIN(tran_begin_time), GETDATE()) AS LagSeconds
			                                                                   FROM cdc.change_tables ct
			                                                                   LEFT JOIN cdc.lsn_time_mapping ltm ON ct.start_lsn = ltm.start_lsn

			                                                                  """).ConfigureAwait(false);

			return new CdcStatus { IsEnabled = true, TableCount = cdcInfo?.TableCount ?? 0, LagSeconds = cdcInfo?.LagSeconds ?? 0 };
		}
		catch (Exception ex)
		{
			LogCdcStatusError(ex);
			return null;
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckFailed, LogLevel.Error,
		"SQL Server health check failed")]
	private partial void LogHealthCheckFailed(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckUnexpectedError, LogLevel.Error,
		"Unexpected error during health check")]
	private partial void LogUnexpectedHealthCheckError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionPoolStatsError, LogLevel.Warning,
		"Failed to get connection pool statistics")]
	private partial void LogConnectionPoolStatsError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckBlockingQueriesError, LogLevel.Warning,
		"Failed to check blocking queries")]
	private partial void LogBlockingQueriesError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckDeadlocksError, LogLevel.Warning,
		"Failed to check deadlocks")]
	private partial void LogDeadlocksError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckQueryPerformanceError, LogLevel.Warning,
		"Failed to check query performance")]
	private partial void LogQueryPerformanceError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckDatabaseSizeError, LogLevel.Warning,
		"Failed to check database size")]
	private partial void LogDatabaseSizeError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceHealthCheckCdcStatusError, LogLevel.Warning,
		"Failed to check CDC status")]
	private partial void LogCdcStatusError(Exception ex);

	private sealed class CdcStatus
	{
		public bool IsEnabled { get; set; }

		public int TableCount { get; set; }

		public int LagSeconds { get; set; }
	}
}
