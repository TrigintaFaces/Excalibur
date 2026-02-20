// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using Dapper;

using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed partial class SqlServerDiagnosticsProvider
{
	private readonly SqlServerConnectionManager _connectionManager;
	private readonly SqlServerPersistenceOptions _options;
	private readonly SqlServerPersistenceMetrics _metrics;
	private readonly ILogger<SqlServerPersistenceProvider> _logger;

	internal SqlServerDiagnosticsProvider(
		SqlServerConnectionManager connectionManager,
		SqlServerPersistenceOptions options,
		SqlServerPersistenceMetrics metrics,
		ILogger<SqlServerPersistenceProvider> logger)
	{
		_connectionManager = connectionManager;
		_options = options;
		_metrics = metrics;
		_logger = logger;
	}

	internal async Task<IDictionary<string, object>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken)
	{
		const string StatsQuery = """
		                          SELECT @@VERSION AS Version,
		                          	@@SERVERNAME AS ServerName,
		                          	DB_NAME() AS DatabaseName,
		                          	(SELECT COUNT(*) FROM sys.dm_exec_connections WHERE session_id = @@SPID) AS ActiveConnections,
		                          	(SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id > 0) AS BlockedRequests,
		                          	(SELECT COUNT(*) FROM sys.dm_tran_active_transactions) AS ActiveTransactions,
		                          	(SELECT SUM(size * 8 / 1024) FROM sys.database_files WHERE type = 0) AS DataSizeMB,
		                          	(SELECT SUM(size * 8 / 1024) FROM sys.database_files WHERE type = 1) AS LogSizeMB,
		                          	(SELECT cntr_value FROM sys.dm_os_performance_counters
		                          WHERE object_name LIKE '%:Buffer Manager%' AND counter_name = 'Buffer cache hit ratio') AS BufferCacheHitRatio,
		                          	SERVERPROPERTY('Edition') AS SqlServerEdition,
		                          	SERVERPROPERTY('ProductVersion') AS ProductVersion,
		                          	SERVERPROPERTY('ProductLevel') AS ProductLevel,
		                          	(SELECT value FROM sys.configurations WHERE name = 'max server memory (MB)') AS MaxServerMemoryMB
		                          """;

		using var connection = await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		var stats = await connection.QuerySingleAsync<dynamic>(StatsQuery).ConfigureAwait(false);

		var result = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var property in (IDictionary<string, object>)stats)
		{
			result[property.Key] = property.Value ?? "N/A";
		}

		return result;
	}

	internal async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			const string PoolQuery = """
			                         SELECT 'SqlServer' as ProviderType,
			                         	@@SERVERNAME as ServerName,
			                         	DB_NAME() as DatabaseName,
			                         	(SELECT COUNT(*) FROM sys.dm_exec_connections) as TotalConnections,
			                         	(SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) as UserConnections,
			                         	(SELECT COUNT(*) FROM sys.dm_exec_requests) as ActiveRequests,
			                         	(SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id > 0) as BlockedRequests,
			                         	(SELECT COUNT(*) FROM sys.dm_tran_active_transactions) as ActiveTransactions
			                         """;

			using var connection = await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			var poolStats = await connection.QuerySingleAsync<dynamic>(PoolQuery).ConfigureAwait(false);

			var result = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (var property in (IDictionary<string, object>)poolStats)
			{
				result[property.Key] = property.Value ?? "N/A";
			}

			result["ConfiguredMaxPoolSize"] = _options.MaxPoolSize;
			result["ConfiguredMinPoolSize"] = _options.MinPoolSize;
			result["ConnectionPoolingEnabled"] = _options.EnableConnectionPooling;

			return result;
		}
		catch (Exception ex)
		{
			LogConnectionPoolStatsError(_logger, ex);
			return null;
		}
	}

	internal async Task<IDictionary<string, object>> GetSchemaInfoAsync(
		string tableName,
		string schemaName,
		CancellationToken cancellationToken)
	{
		const string SchemaQuery = """
		                           SELECT c.COLUMN_NAME as ColumnName,
		                           	c.DATA_TYPE as DataType,
		                           	c.IS_NULLABLE as IsNullable,
		                           	c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
		                           	c.COLUMN_DEFAULT as DefaultValue
		                           FROM INFORMATION_SCHEMA.COLUMNS c
		                           WHERE c.TABLE_NAME = @TableName AND c.TABLE_SCHEMA = @SchemaName
		                           """;

		using var connection = await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		var schemaInfo = await connection.QueryAsync(SchemaQuery, new { TableName = tableName, SchemaName = schemaName })
			.ConfigureAwait(false);

		var result = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["TableName"] = tableName,
			["SchemaName"] = schemaName,
			["Columns"] = schemaInfo.Select(column => new Dictionary<string, object?>(
				StringComparer.Ordinal)
			{
				["ColumnName"] = column.ColumnName,
				["DataType"] = column.DataType,
				["IsNullable"] = column.IsNullable,
				["MaxLength"] = column.MaxLength,
				["DefaultValue"] = column.DefaultValue,
			}).ToList(),
		};

		return result;
	}

	internal async Task<SqlServerConnectionTestResult> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var connection = await _connectionManager.CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			var result = await connection.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(false);

			if (result != 1)
			{
				return SqlServerConnectionTestResult.Unavailable;
			}

			var version = await connection.ExecuteScalarAsync<string>("SELECT @@VERSION").ConfigureAwait(false);
			return SqlServerConnectionTestResult.Available(version);
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(_logger, ex);
			return SqlServerConnectionTestResult.Unavailable;
		}
	}

	internal async Task<IDictionary<string, object>> GetMetricsAsync()
	{
		var metrics = await _metrics.GetMetricsAsync().ConfigureAwait(false);
		metrics["ConnectionPoolSize"] = _options.MaxPoolSize;
		metrics["ConnectionPoolMinSize"] = _options.MinPoolSize;
		metrics["ConnectionTimeoutSeconds"] = _options.ConnectionTimeout;
		metrics["CommandTimeoutSeconds"] = _options.CommandTimeout;
		metrics["RetryPolicyEnabled"] = _options.MaxRetryAttempts > 0;
		metrics["MaxRetryAttempts"] = _options.MaxRetryAttempts;
		metrics["AlwaysEncryptedEnabled"] = _options.Security.EnableAlwaysEncrypted;
		metrics["MarsEnabled"] = _options.Connection.EnableMars;
		return metrics;
	}

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionPoolStatsError, LogLevel.Warning, "Failed to retrieve connection pool statistics")]
	private static partial void LogConnectionPoolStatsError(ILogger logger, Exception exception);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionTestFailed, LogLevel.Warning, "Connection test failed, retrying with fallback strategy")]
	private static partial void LogConnectionTestFailed(ILogger logger, Exception exception);

	internal readonly record struct SqlServerConnectionTestResult(bool IsAvailable, string? DatabaseVersion)
	{
		internal static SqlServerConnectionTestResult Available(string version) => new(IsAvailable: true, version);

		internal static SqlServerConnectionTestResult Unavailable => new(IsAvailable: false, DatabaseVersion: null);
	}
}
