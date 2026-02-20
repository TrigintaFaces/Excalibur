// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Npgsql;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Health check implementation for Postgres persistence provider.
/// </summary>
/// <remarks> Initializes a new instance of the <see cref="PostgresPersistenceHealthCheck" /> class. </remarks>
/// <param name="options"> The persistence options. </param>
/// <param name="logger"> The logger for diagnostic output. </param>
/// <param name="metrics"> Optional metrics collector. </param>
public sealed class PostgresPersistenceHealthCheck(
	IOptions<PostgresPersistenceOptions> options,
	ILogger<PostgresPersistenceHealthCheck> logger,
	PostgresPersistenceMetrics? metrics = null) : IHealthCheck
{
	private readonly PostgresPersistenceOptions _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
	private readonly ILogger<PostgresPersistenceHealthCheck> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

	/// <inheritdoc />
	public async Task<HealthCheckResult> CheckHealthAsync(
		HealthCheckContext context,
		CancellationToken cancellationToken)
	{
		var stopwatch = Stopwatch.StartNew();
		var data = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			await using var connection = new NpgsqlConnection(_options.BuildConnectionString());

			// Test basic connectivity
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
			data["connection_time_ms"] = stopwatch.ElapsedMilliseconds;

			// Get server version
			var serverVersion = connection.ServerVersion;
			data["server_version"] = serverVersion;
			data["postgres_version"] = connection.PostgreSqlVersion.ToString();

			// Check database name
			data["database"] = connection.Database;

			// Run a simple query to test responsiveness
			stopwatch.Restart();
			await using (var command = connection.CreateCommand())
			{
				command.CommandText = "SELECT 1";
				var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				data["query_time_ms"] = stopwatch.ElapsedMilliseconds;

				if (result == null || (int)result != 1)
				{
					return HealthCheckResult.Unhealthy("Query test failed", data: data);
				}
			}

			// Note: NpgsqlConnection.GetPoolStatistics() was removed in Npgsql 7+ Pool statistics are now available through different mechanisms
			if (_options.EnableConnectionPooling)
			{
				// Pool statistics can be obtained through performance counters or custom metrics
				data["pool_enabled"] = true;
				data["pool_max_size"] = _options.MaxPoolSize;
				data["pool_min_size"] = _options.MinPoolSize;
			}

			// Check database statistics
			stopwatch.Restart();
			var dbStats = await GetDatabaseStatisticsAsync(connection, cancellationToken).ConfigureAwait(false);
			data["stats_query_time_ms"] = stopwatch.ElapsedMilliseconds;

			foreach (var stat in dbStats)
			{
				data[$"db_{stat.Key}"] = stat.Value;
			}

			// Check for any blocking queries
			var blockingQueries = await CheckBlockingQueriesAsync(connection, cancellationToken).ConfigureAwait(false);
			data["blocking_queries"] = blockingQueries;

			// Performance thresholds
			var connectionTime = (long)data["connection_time_ms"];
			var queryTime = (long)data["query_time_ms"];

			// Determine health status based on performance
			if (connectionTime > 5000 || queryTime > 1000)
			{
				return HealthCheckResult.Degraded(
					$"Performance degraded: connection={connectionTime}ms, query={queryTime}ms",
					data: data);
			}

			if (blockingQueries > 0)
			{
				return HealthCheckResult.Degraded(
					$"Found {blockingQueries} blocking queries",
					data: data);
			}

			// Add metrics if available
			if (metrics != null)
			{
				var currentMetrics = metrics.GetCurrentMetrics();
				foreach (var metric in currentMetrics)
				{
					data[$"metric_{metric.Key}"] = metric.Value;
				}
			}

			_logger.LogDebug("Postgres health check passed in {ElapsedMs}ms", stopwatch.ElapsedMilliseconds);
			return HealthCheckResult.Healthy("Postgres connection is healthy", data);
		}
		catch (NpgsqlException ex) when (ex.IsTransient)
		{
			_logger.LogWarning(ex, "Transient Postgres error during health check");
			data["error"] = ex.Message;
			data["error_code"] = ex.SqlState ?? "Unknown";
			return HealthCheckResult.Degraded("Transient database error", ex, data);
		}
		catch (OperationCanceledException ex)
		{
			_logger.LogWarning(ex, "Health check cancelled");
			data["error"] = "Operation cancelled";
			return HealthCheckResult.Unhealthy("Health check cancelled", ex, data);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Postgres health check failed");
			data["error"] = ex.Message;
			data["error_type"] = ex.GetType().Name;

			metrics?.RecordConnectionError(ex.GetType().Name);

			return HealthCheckResult.Unhealthy("Postgres connection failed", ex, data);
		}
		finally
		{
			stopwatch.Stop();
			data["total_time_ms"] = stopwatch.ElapsedMilliseconds;
		}
	}

	private async Task<Dictionary<string, object>> GetDatabaseStatisticsAsync(
		NpgsqlConnection connection,
		CancellationToken cancellationToken)
	{
		var stats = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			// Get database size
			await using (var sizeCommand = connection.CreateCommand())
			{
				sizeCommand.CommandText = """
				                           SELECT pg_database_size(current_database()) as db_size,
				                           pg_size_pretty(pg_database_size(current_database())) as db_size_pretty
				                          """;

				await using var reader = await sizeCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					stats["size_bytes"] = reader.GetInt64(0);
					stats["size_pretty"] = await reader.IsDBNullAsync(1, cancellationToken).ConfigureAwait(false)
						? "Unknown"
						: reader.GetString(1);
				}
			}

			// Get connection count
			await using (var connCommand = connection.CreateCommand())
			{
				connCommand.CommandText = """
				                           SELECT count(*) as total_connections,
				                           count(*) FILTER (WHERE state = 'active') as active_connections,
				                           count(*) FILTER (WHERE state = 'idle') as idle_connections,
				                           count(*) FILTER (WHERE state = 'idle in transaction') as idle_in_transaction
				                           FROM pg_stat_activity
				                           WHERE datname = current_database()
				                          """;

				await using var reader = await connCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					stats["total_connections"] = reader.GetInt64(0);
					stats["active_connections"] = reader.GetInt64(1);
					stats["idle_connections"] = reader.GetInt64(2);
					stats["idle_in_transaction"] = reader.GetInt64(3);
				}
			}

			// Get cache hit ratio
			await using (var cacheCommand = connection.CreateCommand())
			{
				cacheCommand.CommandText = """
				                            SELECT
				                            sum(heap_blks_hit) / NULLIF(sum(heap_blks_hit) + sum(heap_blks_read), 0) as cache_hit_ratio
				                            FROM pg_statio_user_tables
				                           """;

				var result = await cacheCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
				if (result != null && result != DBNull.Value)
				{
					stats["cache_hit_ratio"] = Convert.ToDouble(result);
				}
			}

			// Get transaction statistics
			await using (var txCommand = connection.CreateCommand())
			{
				txCommand.CommandText = """
				                         SELECT xact_commit, xact_rollback, conflicts, deadlocks
				                         FROM pg_stat_database
				                         WHERE datname = current_database()
				                        """;

				await using var reader = await txCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					stats["committed_transactions"] = reader.GetInt64(0);
					stats["rolled_back_transactions"] = reader.GetInt64(1);
					stats["conflicts"] = reader.GetInt64(2);
					stats["deadlocks"] = reader.GetInt64(3);
				}
			}
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get database statistics");
			stats["error"] = ex.Message;
		}

		return stats;
	}

	private async Task<int> CheckBlockingQueriesAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
	{
		try
		{
			await using var command = connection.CreateCommand();
			command.CommandText = """
			                       SELECT COUNT(*)
			                       FROM pg_stat_activity
			                       WHERE wait_event_type IS NOT NULL
			                       AND state != 'idle'
			                       AND pid != pg_backend_pid()
			                      """;

			var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
			return result != null ? Convert.ToInt32(result) : 0;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to check for blocking queries");
			return 0;
		}
	}
}
