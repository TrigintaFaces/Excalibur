// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Postgres implementation of the SQL persistence provider that focuses on DataRequest execution while providing Postgres-specific
/// optimizations and infrastructure concerns like retry policies, metrics collection, and connection management.
/// </summary>
public class PostgresPersistenceProvider : ISqlPersistenceProvider
{
	private readonly PostgresPersistenceOptions _options;
	private readonly ILogger<PostgresPersistenceProvider> _logger;
	private readonly PostgresPersistenceMetrics _metrics;
	private readonly AsyncRetryPolicy _retryPolicy;
	private bool _initialized;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The persistence options. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	/// <param name="metrics"> The metrics collector. </param>
	/// <param name="retryPolicy"> The DataRequest retry policy (optional). </param>
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public PostgresPersistenceProvider(
		IOptions<PostgresPersistenceOptions> options,
		ILogger<PostgresPersistenceProvider> logger,
		PostgresPersistenceMetrics? metrics = null,
		IDataRequestRetryPolicy? retryPolicy = null)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_metrics = metrics ?? new PostgresPersistenceMetrics();

		// Validate options on construction
		_options.Validate();

		// Configure retry policy for transient failures
		_retryPolicy = Policy
			.Handle<NpgsqlException>(ex => ex.IsTransient)
			.Or<DbException>()
			.Or<TimeoutException>()
			.WaitAndRetryAsync(
				_options.MaxRetryAttempts,
				retryAttempt => TimeSpan.FromMilliseconds(_options.RetryDelayMilliseconds * Math.Pow(2, retryAttempt - 1)),
				onRetry: (exception, timespan, retryCount, context) => _logger.LogWarning(
					exception,
					"Transient failure during database operation. Retry {RetryCount}/{MaxRetries} after {Delay}ms",
					retryCount, _options.MaxRetryAttempts, timespan.TotalMilliseconds));

		// Create or use provided DataRequest retry policy
		RetryPolicy = retryPolicy ?? new PostgresDataRequestRetryPolicy(_options, _logger);

		_logger.LogInformation(
			"Postgres persistence provider initialized with connection pooling={PoolingEnabled}, max pool size={MaxPoolSize}",
			_options.EnableConnectionPooling, _options.MaxPoolSize);
	}

	/// <inheritdoc />
	public string Name => "Postgres";

	/// <inheritdoc />
	public string ProviderType => "SQL";

	/// <inheritdoc />
	public string DatabaseType => "Postgres";

	/// <inheritdoc />
	public string DatabaseVersion { get; private set; } = "Unknown";

	/// <inheritdoc />
	public bool IsAvailable { get; private set; }

	/// <inheritdoc />
	public string ConnectionString => _options.ConnectionString;

	/// <inheritdoc />
	public IDataRequestRetryPolicy RetryPolicy { get; }

	/// <inheritdoc />
	public bool SupportsBulkOperations => true;

	/// <inheritdoc />
	public bool SupportsStoredProcedures => true;

	/// <inheritdoc />
	public IDbConnection CreateConnection()
	{
		ThrowIfDisposed();

		var connectionString = _options.BuildConnectionString();
		var connection = new NpgsqlConnection(connectionString);

		_logger.LogDebug("Created new Postgres connection");
		return connection;
	}

	/// <inheritdoc />
	public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		var stopwatch = ValueStopwatch.StartNew();

		NpgsqlConnection? connection = null;
		try
		{
			var connectionString = _options.BuildConnectionString();
			connection = new NpgsqlConnection(connectionString);

			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			var elapsed = stopwatch.Elapsed.TotalMilliseconds;
			_metrics.RecordConnectionAcquisition(elapsed);

			_logger.LogDebug("Created and opened Postgres connection in {ElapsedMs}ms", elapsed);

			return connection;
		}
		catch (Exception ex)
		{
			connection?.Dispose();
			_metrics.RecordConnectionError(ex.GetType().Name);
			_logger.LogError(ex, "Failed to create Postgres connection");
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(requests);

		var requestList = requests.ToList();

		return await _retryPolicy.ExecuteAsync(async () => await PostgresPersistenceMetrics.MeasureOperationAsync(
			async () =>
			{
				await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false) as NpgsqlConnection
											 ?? throw new InvalidOperationException("Connection must be NpgsqlConnection");

				await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

				try
				{
					var results = new List<object>();

					foreach (var request in requestList)
					{
						// Execute the DataRequest within the transaction context
						var result = await request.ResolveAsync(connection).ConfigureAwait(false);
						results.Add(result);
					}

					await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

					_logger.LogDebug(
						"Executed batch of {RequestCount} DataRequests successfully",
						requestList.Count);

					return (IEnumerable<object>)results;
				}
				catch
				{
					await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
					throw;
				}
			},
			duration => _metrics.RecordCommand(duration, requestList.Count,
				new KeyValuePair<string, object?>("operation", "execute_batch"),
				new KeyValuePair<string, object?>("batch_size", requestList.Count))).ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(requests);
		ArgumentNullException.ThrowIfNull(transactionScope);

		var requestList = requests.ToList();

		// Enlist this provider in the transaction
		await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

		return await _retryPolicy.ExecuteAsync(async () => await PostgresPersistenceMetrics.MeasureOperationAsync(
			async () =>
			{
				using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

				// Enlist the connection in the transaction
				await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

				var results = new List<object>();

				foreach (var request in requestList)
				{
					var result = await request.ResolveAsync(connection).ConfigureAwait(false);
					results.Add(result);
				}

				_logger.LogDebug(
					"Executed batch of {RequestCount} DataRequests in transaction successfully",
					requestList.Count);

				return (IEnumerable<object>)results;
			},
			duration => _metrics.RecordCommand(duration, requestList.Count,
				new KeyValuePair<string, object?>("operation", "execute_batch_transaction"),
				new KeyValuePair<string, object?>("batch_size", requestList.Count))).ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteBulkAsync<TResult>(
		IDataRequest<IDbConnection, TResult> bulkRequest,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(bulkRequest);

		return await _retryPolicy.ExecuteAsync(async () => await PostgresPersistenceMetrics.MeasureOperationAsync(
			async () =>
			{
				await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false) as NpgsqlConnection
											 ?? throw new InvalidOperationException("Connection must be NpgsqlConnection");

				// Execute the bulk DataRequest with Postgres-specific optimizations
				var result = await bulkRequest.ResolveAsync(connection).ConfigureAwait(false);

				_logger.LogDebug("Executed bulk DataRequest successfully");

				return result;
			},
			duration => _metrics.RecordCommand(duration, 1,
				new KeyValuePair<string, object?>("operation", "execute_bulk"))).ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteStoredProcedureAsync<TResult>(
		IDataRequest<IDbConnection, TResult> storedProcedureRequest,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(storedProcedureRequest);

		return await _retryPolicy.ExecuteAsync(async () => await PostgresPersistenceMetrics.MeasureOperationAsync(
			async () =>
			{
				using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

				// Execute the stored procedure DataRequest
				var result = await storedProcedureRequest.ResolveAsync(connection).ConfigureAwait(false);

				_logger.LogDebug("Executed stored procedure DataRequest successfully");

				return result;
			},
			duration => _metrics.RecordCommand(duration, 1,
				new KeyValuePair<string, object?>("operation", "execute_stored_procedure"))).ConfigureAwait(false)).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		var stats = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false) as NpgsqlConnection
										 ?? throw new InvalidOperationException("Connection must be NpgsqlConnection");

			// Get version
			stats["version"] = connection.PostgreSqlVersion.ToString();
			stats["server_version"] = connection.ServerVersion;

			// Get database size
			const string sizeQuery = "SELECT pg_database_size(current_database())";
			var dbSize = await connection.ExecuteScalarAsync<long>(sizeQuery).ConfigureAwait(false);
			stats["database_size_bytes"] = dbSize;

			// Get connection stats
			const string connStatsQuery = """
			                               SELECT
			                               COUNT(*) as total,
			                               COUNT(*) FILTER (WHERE state = 'active') as active,
			                               COUNT(*) FILTER (WHERE state = 'idle') as idle
			                               FROM pg_stat_activity
			                               WHERE datname = current_database()
			                              """;

			var connStats = await connection.QuerySingleAsync<dynamic>(connStatsQuery).ConfigureAwait(false);
			stats["total_connections"] = connStats.total;
			stats["active_connections"] = connStats.active;
			stats["idle_connections"] = connStats.idle;

			// Get cache hit ratio
			const string cacheQuery = """
			                           SELECT
			                           ROUND(100.0 * sum(heap_blks_hit) / NULLIF(sum(heap_blks_hit) + sum(heap_blks_read), 0), 2) as cache_hit_ratio
			                           FROM pg_statio_user_tables
			                          """;

			var cacheHitRatio = await connection.ExecuteScalarAsync<decimal?>(cacheQuery).ConfigureAwait(false) ?? 0;
			stats["cache_hit_ratio"] = cacheHitRatio;

			// Add metrics if available
			if (_metrics != null)
			{
				foreach (var metric in _metrics.GetCurrentMetrics())
				{
					stats[$"metric_{metric.Key}"] = metric.Value;
				}
			}

			// Note: NpgsqlConnection.GetPoolStatistics() was removed in Npgsql 7+ Pool statistics are now available through different mechanisms
			if (_options.EnableConnectionPooling)
			{
				stats["pool_enabled"] = true;
				stats["pool_max_size"] = _options.MaxPoolSize;
				stats["pool_min_size"] = _options.MinPoolSize;
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get database statistics");
			stats["error"] = ex.Message;
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetSchemaInfoAsync(
		string tableName,
		string? schemaName,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var schemaInfo = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

			// Use default schema if not specified
			var effectiveSchema = schemaName ?? "public";

			// Get table information
			const string tableInfoQuery = """
			                               SELECT
			                               table_type,
			                               is_insertable_into,
			                               is_typed
			                               FROM information_schema.tables
			                               WHERE table_name = @TableName
			                               AND table_schema = @SchemaName
			                              """;

			var tableInfo = await connection.QuerySingleOrDefaultAsync<dynamic>(
				tableInfoQuery,
				new { TableName = tableName, SchemaName = effectiveSchema }).ConfigureAwait(false);

			if (tableInfo == null)
			{
				schemaInfo["error"] = $"Table '{effectiveSchema}.{tableName}' not found";
				return schemaInfo;
			}

			schemaInfo["table_name"] = tableName;
			schemaInfo["schema_name"] = effectiveSchema;
			schemaInfo["table_type"] = tableInfo.table_type;
			schemaInfo["is_insertable"] = tableInfo.is_insertable_into;

			// Get column information
			const string columnsQuery = """
			                             SELECT
			                             column_name,
			                             data_type,
			                             is_nullable,
			                             column_default,
			                             character_maximum_length,
			                             numeric_precision,
			                             numeric_scale,
			                             ordinal_position
			                             FROM information_schema.columns
			                             WHERE table_name = @TableName
			                             AND table_schema = @SchemaName
			                             ORDER BY ordinal_position
			                            """;

			var columns = await connection.QueryAsync<dynamic>(
				columnsQuery,
				new { TableName = tableName, SchemaName = effectiveSchema }).ConfigureAwait(false);

			schemaInfo["columns"] = columns.ToList();

			// Get index information
			const string indexQuery = """
			                           SELECT
			                           i.relname as index_name,
			                           ix.indisunique as is_unique,
			                           ix.indisprimary as is_primary,
			                           array_agg(a.attname ORDER BY a.attnum) as column_names
			                           FROM pg_class t
			                           JOIN pg_index ix ON t.oid = ix.indrelid
			                           JOIN pg_class i ON i.oid = ix.indexrelid
			                           JOIN pg_attribute a ON t.oid = a.attrelid
			                           JOIN pg_namespace n ON n.oid = t.relnamespace
			                           WHERE t.relname = @TableName
			                           AND n.nspname = @SchemaName
			                           AND a.attnum = ANY(ix.indkey)
			                           GROUP BY i.relname, ix.indisunique, ix.indisprimary
			                          """;

			var indexes = await connection.QueryAsync<dynamic>(
				indexQuery,
				new { TableName = tableName, SchemaName = effectiveSchema }).ConfigureAwait(false);

			schemaInfo["indexes"] = indexes.ToList();

			// Get constraints
			const string constraintsQuery = """
			                                 SELECT
			                                 tc.constraint_name,
			                                 tc.constraint_type,
			                                 kcu.column_name,
			                                 ccu.table_name as foreign_table_name,
			                                 ccu.column_name as foreign_column_name
			                                 FROM information_schema.table_constraints tc
			                                 LEFT JOIN information_schema.key_column_usage kcu
			                                 ON tc.constraint_name = kcu.constraint_name
			                                 AND tc.table_schema = kcu.table_schema
			                                 LEFT JOIN information_schema.constraint_column_usage ccu
			                                 ON tc.constraint_name = ccu.constraint_name
			                                 AND tc.table_schema = ccu.table_schema
			                                 WHERE tc.table_name = @TableName
			                                 AND tc.table_schema = @SchemaName
			                                """;

			var constraints = await connection.QueryAsync<dynamic>(
				constraintsQuery,
				new { TableName = tableName, SchemaName = effectiveSchema }).ConfigureAwait(false);

			schemaInfo["constraints"] = constraints.ToList();

			_logger.LogDebug(
				"Retrieved schema information for table {Schema}.{Table}",
				effectiveSchema, tableName);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to get schema information for table {Schema}.{Table}",
				schemaName ?? "public", tableName);
			schemaInfo["error"] = ex.Message;
		}

		return schemaInfo;
	}

	/// <inheritdoc />
	public bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request)
	{
		ArgumentNullException.ThrowIfNull(request);

		try
		{
			// Basic validation checks for Postgres compatibility
			var command = request.Command;

			// Check if command text is provided
			if (string.IsNullOrWhiteSpace(command.CommandText))
			{
				_logger.LogWarning("DataRequest validation failed: CommandText is null or empty");
				return false;
			}

			// Check for Postgres-specific command types that might not be supported
			var commandText = command.CommandText.Trim().ToUpperInvariant();

			// Postgres specific validations
			if (commandText.Contains("EXEC ", StringComparison.Ordinal) || commandText.Contains("EXECUTE ", StringComparison.Ordinal))
			{
				// This might be SQL Server syntax - warn but don't fail
				_logger.LogWarning("DataRequest contains EXEC/EXECUTE which may not be Postgres compatible");
			}

			// Check for SQL Server specific syntax that won't work in Postgres
			if (commandText.Contains('[', StringComparison.Ordinal) && commandText.Contains(']', StringComparison.Ordinal))
			{
				_logger.LogWarning("DataRequest contains SQL Server bracket notation which is not Postgres compatible");
				return false;
			}

			// Check if parameters are compatible
			if (request.Parameters != null)
			{
				foreach (var param in request.Parameters.ParameterNames)
				{
					var paramValue = request.Parameters.Get<object>(param);
					if (paramValue != null && !IsPostgresCompatibleType(paramValue.GetType()))
					{
						_logger.LogWarning(
							"DataRequest parameter '{ParameterName}' has type '{Type}' which may not be Postgres compatible",
							param, paramValue.GetType().Name);
					}
				}
			}

			// Check if ResolveAsync function is provided
			if (request.ResolveAsync == null)
			{
				_logger.LogWarning("DataRequest validation failed: ResolveAsync function is null");
				return false;
			}

			_logger.LogDebug(
				"DataRequest validation passed for command starting with: {CommandPrefix}",
				commandText.Length > 50 ? commandText[..50] + "..." : commandText);

			return true;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error during DataRequest validation");
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(request);

		return await _retryPolicy.ExecuteAsync(async () =>
		{
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			return await request.ResolveAsync(connection).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(request);

		// Use the DataRequest retry policy for execution
		return await RetryPolicy.ResolveAsync(
			request,
			async () => (TConnection)await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteInTransactionAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(transactionScope);

		// Enlist this provider in the transaction
		await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

		return await _retryPolicy.ExecuteAsync(async () =>
		{
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

			// Enlist the connection in the transaction
			await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

			return await request.ResolveAsync(connection).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ThrowIfDisposed();
		ThrowIfNotInitialized();

		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(transactionScope);

		// Enlist this provider in the transaction
		await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

		return await _retryPolicy.ExecuteAsync(async () =>
		{
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

			// Enlist the connection in the transaction
			await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);

			return await request.ResolveAsync((TConnection)connection).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ThrowIfDisposed();

		var transactionScope = new PostgresTransactionScope(
			isolationLevel,
			_logger as ILogger<PostgresTransactionScope> ??
			new NullLogger<PostgresTransactionScope>());

		// Set timeout if provided
		if (timeout.HasValue)
		{
			transactionScope.Timeout = timeout.Value;
		}

		return transactionScope;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (!_options.EnableConnectionPooling)
		{
			return null;
		}

		var stats = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["pool_enabled"] = true,
			["pool_max_size"] = _options.MaxPoolSize,
			["pool_min_size"] = _options.MinPoolSize,
			["connection_idle_lifetime"] = _options.Connection.ConnectionIdleLifetime,
			["connection_pruning_interval"] = _options.Connection.ConnectionPruningInterval,
		};

		// Note: Npgsql 7+ doesn't provide runtime pool statistics via the API but we can include configuration information
		try
		{
			// Get basic connection statistics from the database itself
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

			const string activeConnectionsQuery = """
			                                       SELECT
			                                       COUNT(*) as total_connections,
			                                       COUNT(*) FILTER (WHERE state = 'active') as active_connections,
			                                       COUNT(*) FILTER (WHERE state = 'idle') as idle_connections
			                                       FROM pg_stat_activity
			                                       WHERE datname = current_database()
			                                      """;

			var connectionStats = await connection.QuerySingleAsync<dynamic>(activeConnectionsQuery).ConfigureAwait(false);

			stats["active_connections"] = connectionStats.active_connections;
			stats["idle_connections"] = connectionStats.idle_connections;
			stats["total_connections"] = connectionStats.total_connections;
		}
		catch (Exception ex)
		{
			_logger.LogWarning(ex, "Failed to get connection statistics");
			stats["error"] = ex.Message;
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		try
		{
			await using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false) as NpgsqlConnection;

			if (connection == null)
			{
				return false;
			}

			// Execute a simple query to verify connectivity
			var result = await connection.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(false);

			IsAvailable = result == 1;

			if (IsAvailable)
			{
				// Update database version while we have a connection
				DatabaseVersion = $"{connection.PostgreSqlVersion} ({connection.ServerVersion})";
				_logger.LogInformation("Postgres connection test successful. Version: {Version}", DatabaseVersion);
			}

			return IsAvailable;
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Postgres connection test failed");
			IsAvailable = false;
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["provider"] = Name,
			["type"] = ProviderType,
			["database_type"] = DatabaseType,
			["database_version"] = DatabaseVersion,
			["available"] = IsAvailable,
			["initialized"] = _initialized,
		};

		if (_metrics != null)
		{
			foreach (var metric in _metrics.GetCurrentMetrics())
			{
				metrics[metric.Key] = metric.Value;
			}
		}

		// Get real-time statistics if available
		if (IsAvailable)
		{
			try
			{
				var dbStats = await GetDatabaseStatisticsAsync(cancellationToken).ConfigureAwait(false);
				foreach (var stat in dbStats)
				{
					metrics[$"db_{stat.Key}"] = stat.Value;
				}
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Failed to get database statistics for metrics");
			}
		}

		return metrics;
	}

	/// <inheritdoc />
	[RequiresUnreferencedCode("This method uses reflection and may not work correctly with trimming")]
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (_initialized)
		{
			_logger.LogWarning("Postgres persistence provider already initialized");
			return;
		}

		if (options is PostgresPersistenceOptions postgresOptions)
		{
			// Update options if different instance provided
			if (!ReferenceEquals(_options, postgresOptions))
			{
				postgresOptions.Validate();

				// Copy properties to existing options instance This maintains the reference used in DI container
				CopyOptions(postgresOptions, _options);
			}
		}

		// Test the connection
		var isConnected = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);

		if (!isConnected)
		{
			throw new InvalidOperationException("Failed to initialize Postgres persistence provider: connection test failed");
		}

		_initialized = true;

		var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString);
		_logger.LogInformation(
			"Postgres persistence provider initialized successfully. Database: {Database}, Version: {Version}",
			builder.Database,
			DatabaseVersion);
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		await DisposeAsyncCore().ConfigureAwait(false);
		Dispose(disposing: false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Disposes the provider.
	/// </summary>
	/// <param name="disposing"> True if disposing managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (_disposed)
		{
			return;
		}

		if (disposing)
		{
			_metrics?.Dispose();

			// Clear connection pools if pooling is enabled
			if (_options.EnableConnectionPooling)
			{
				try
				{
					NpgsqlConnection.ClearAllPools();
					_logger.LogDebug("Cleared all Postgres connection pools");
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error clearing connection pools");
				}
			}
		}

		_disposed = true;
	}

	/// <summary>
	/// Asynchronously disposes the provider.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (_disposed)
		{
			return;
		}

		_metrics?.Dispose();

			// Clear connection pools if pooling is enabled
			if (_options.EnableConnectionPooling)
			{
				try
				{
					NpgsqlConnection.ClearAllPools();
					_logger.LogDebug("Cleared all Postgres connection pools");
				}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error clearing connection pools");
			}
		}

		_disposed = true;
	}

	/// <summary>
	/// Checks if a type is compatible with Postgres data types.
	/// </summary>
	/// <param name="type"> The type to check. </param>
	/// <returns> True if the type is compatible with Postgres; otherwise, false. </returns>
	private static bool IsPostgresCompatibleType(Type type)
	{
		// Handle nullable types
		var underlyingType = Nullable.GetUnderlyingType(type) ?? type;

		// Basic .NET types that map well to Postgres
		if (underlyingType.IsPrimitive ||
			underlyingType == typeof(string) ||
			underlyingType == typeof(decimal) ||
			underlyingType == typeof(DateTime) ||
			underlyingType == typeof(DateTimeOffset) ||
			underlyingType == typeof(TimeSpan) ||
			underlyingType == typeof(Guid) ||
			underlyingType == typeof(byte[]))
		{
			return true;
		}

		// Postgres specific types
		if (underlyingType.Namespace?.StartsWith("NpgsqlTypes", StringComparison.Ordinal) == true)
		{
			return true;
		}

		// Arrays of compatible types
		if (underlyingType.IsArray)
		{
			var elementType = underlyingType.GetElementType();
			return elementType != null && IsPostgresCompatibleType(elementType);
		}

		// Collections and other complex types might need special handling
		if (underlyingType is { IsClass: true, IsSealed: false })
		{
			// Dynamic objects and custom classes might work but warn
			return true;
		}

		return false;
	}

	private static void CopyOptions(PostgresPersistenceOptions source, PostgresPersistenceOptions target)
	{
		// IPersistenceOptions-mandated properties
		target.ConnectionString = source.ConnectionString;
		target.ConnectionTimeout = source.ConnectionTimeout;
		target.CommandTimeout = source.CommandTimeout;
		target.MaxRetryAttempts = source.MaxRetryAttempts;
		target.RetryDelayMilliseconds = source.RetryDelayMilliseconds;
		target.EnableConnectionPooling = source.EnableConnectionPooling;
		target.MaxPoolSize = source.MaxPoolSize;
		target.MinPoolSize = source.MinPoolSize;
		target.EnableDetailedLogging = source.EnableDetailedLogging;
		target.EnableMetrics = source.EnableMetrics;
		target.ProviderSpecificOptions = new Dictionary<string, object>(source.ProviderSpecificOptions, StringComparer.Ordinal);

		// Connection sub-options
		target.Connection.ApplicationName = source.Connection.ApplicationName;
		target.Connection.DefaultDatabase = source.Connection.DefaultDatabase;
		target.Connection.ConnectionIdleLifetime = source.Connection.ConnectionIdleLifetime;
		target.Connection.ConnectionPruningInterval = source.Connection.ConnectionPruningInterval;
		target.Connection.EnableTcpKeepAlive = source.Connection.EnableTcpKeepAlive;
		target.Connection.TcpKeepAliveTime = source.Connection.TcpKeepAliveTime;
		target.Connection.TcpKeepAliveInterval = source.Connection.TcpKeepAliveInterval;
		target.Connection.IncludeErrorDetail = source.Connection.IncludeErrorDetail;
		target.Connection.SocketReceiveBufferSize = source.Connection.SocketReceiveBufferSize;
		target.Connection.SocketSendBufferSize = source.Connection.SocketSendBufferSize;

		// Statement sub-options
		target.Statements.EnablePreparedStatementCaching = source.Statements.EnablePreparedStatementCaching;
		target.Statements.MaxPreparedStatements = source.Statements.MaxPreparedStatements;
		target.Statements.EnableAutoPrepare = source.Statements.EnableAutoPrepare;
		target.Statements.AutoPrepareMinUsages = source.Statements.AutoPrepareMinUsages;
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	private void ThrowIfNotInitialized()
	{
		if (!_initialized)
		{
			throw new InvalidOperationException("Postgres persistence provider not initialized. Call InitializeAsync first.");
		}
	}
}
