// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR
// AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.Postgres.Diagnostics;
using Excalibur.Data.Postgres.Persistence;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Npgsql;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.Postgres;

/// <summary>
/// Postgres implementation of the persistence provider.
/// </summary>
public sealed partial class PostgresPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly PostgresProviderOptions _options;
	private readonly ILogger<PostgresPersistenceProvider> _logger;
	private readonly AsyncRetryPolicy _retryPolicy;
	private readonly PostgresRetryPolicy _dataRequestRetryPolicy;
	private readonly NpgsqlDataSource? _dataSource;
	private volatile bool _disposed;
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The Postgres provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	[RequiresUnreferencedCode(
		"NpgsqlConnectionStringBuilder property reflection may reference types not preserved during trimming. Ensure connection string builder types are annotated with DynamicallyAccessedMembers.")]
	[RequiresDynamicCode(
		"NpgsqlConnectionStringBuilder construction and property reflection require dynamic code generation for configuration parsing and validation.")]
	public PostgresPersistenceProvider(
		IOptions<PostgresProviderOptions> options,
		ILogger<PostgresPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			throw new ArgumentException("Connection string is required.", nameof(options));
		}

		// Build connection string with additional options
		var builder = new NpgsqlConnectionStringBuilder(_options.ConnectionString)
		{
			CommandTimeout = _options.CommandTimeout,
			Timeout = _options.ConnectTimeout,
			MaxPoolSize = _options.MaxPoolSize,
			MinPoolSize = _options.MinPoolSize,
			Pooling = _options.EnablePooling,
			ApplicationName = _options.ApplicationName ?? "Excalibur.Data",
			KeepAlive = _options.KeepAlive,
			ConnectionIdleLifetime = _options.ConnectionIdleLifetime,
			ConnectionPruningInterval = _options.ConnectionPruningInterval,
			IncludeErrorDetail = _options.IncludeErrorDetail,
		};

		if (_options.PrepareStatements)
		{
			builder.MaxAutoPrepare = _options.MaxAutoPrepare;
			builder.AutoPrepareMinUsages = _options.AutoPrepareMinUsages;
		}

		if (_options.UseSsl)
		{
			builder.SslMode = _options.SslMode;

			// TrustServerCertificate is obsolete in newer Npgsql versions SSL mode configuration is sufficient
		}

		ConnectionString = builder.ConnectionString;
		Name = _options.Name ?? "Postgres";
		ProviderType = "SQL";

		// Initialize retry policy for DataRequests
		_dataRequestRetryPolicy = new PostgresRetryPolicy(_options, _logger);

		// Create data source for better connection management
		if (_options.UseDataSource)
		{
			var dataSourceBuilder = new NpgsqlDataSourceBuilder(ConnectionString);

			if (_options.EnableJsonb)
			{
				_ = dataSourceBuilder.EnableDynamicJson();
			}

			_dataSource = dataSourceBuilder.Build();
		}

		// Setup retry policy for transient failures
		_retryPolicy = Policy
			.Handle<NpgsqlException>(IsTransientError)
			.Or<PostgresException>(IsTransientError)
			.WaitAndRetryAsync(
				_options.RetryCount,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryAttempt(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <inheritdoc />
	public string Name { get; }

	/// <inheritdoc />
	public string ProviderType { get; }

	/// <inheritdoc />
	public bool IsAvailable => _initialized && !_disposed;

	/// <inheritdoc />
	public string ConnectionString { get; }

	/// <inheritdoc />
	public IDataRequestRetryPolicy RetryPolicy => _dataRequestRetryPolicy;

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequest(request.GetType().Name);

		return await _dataRequestRetryPolicy.ResolveAsync(
			request,
			async () => (TConnection)await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteInTransactionAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequestInTransaction(request.GetType().Name, transactionScope.TransactionId);

		try
		{
			// Enlist this provider in the transaction
			await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

			// Execute the request with retry policy
			var result = await _dataRequestRetryPolicy.ResolveAsync(
				request,
				async () => (TConnection)await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);

			return result;
		}
		catch (Exception ex)
		{
			LogFailedToExecuteDataRequest(transactionScope.TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		var scope = new PostgresTransactionScope(
			isolationLevel,
			_logger as ILogger<PostgresTransactionScope> ??
			NullLogger<PostgresTransactionScope>.Instance);

		if (timeout.HasValue)
		{
			scope.Timeout = timeout.Value;
		}

		return scope;
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _retryPolicy.ExecuteAsync(
				async ct =>
				{
					await using var connection = _dataSource != null
						? await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false)
						: new NpgsqlConnection(ConnectionString);

					if (_dataSource == null)
					{
						await connection.OpenAsync(ct).ConfigureAwait(false);
					}

					await using var command = connection.CreateCommand();
					command.CommandText = "SELECT 1";
					command.CommandTimeout = 5;

					_ = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
				}, cancellationToken).ConfigureAwait(false);

			LogConnectionTestSuccessful(Name);
			return true;
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(Name, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>
			(StringComparer.Ordinal)
		{
			["Provider"] = "Postgres",
			["Name"] = Name,
			["IsAvailable"] = IsAvailable,
			["MaxPoolSize"] = _options.MaxPoolSize,
			["MinPoolSize"] = _options.MinPoolSize,
			["EnablePooling"] = _options.EnablePooling,
			["PrepareStatements"] = _options.PrepareStatements,
			["UseSsl"] = _options.UseSsl,
			["SslMode"] = _options.SslMode.ToString(),
			["CommandTimeout"] = _options.CommandTimeout,
			["ConnectTimeout"] = _options.ConnectTimeout,
			["UseDataSource"] = _options.UseDataSource,
		};

		try
		{
			NpgsqlConnection connection;
			if (_dataSource != null)
			{
				connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// CA2000: Exception safety is handled - connection is disposed in catch block if OpenAsync throws. Ownership is transferred
				// to await using block immediately after this else block.
#pragma warning disable CA2000 // Dispose objects before losing scope
				connection = new NpgsqlConnection(ConnectionString);
#pragma warning restore CA2000 // Dispose objects before losing scope
				try
				{
					await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					connection.Dispose();
					throw;
				}
			}

			await using (connection)
			{
				metrics["ServerVersion"] = connection.ServerVersion;
				metrics["Database"] = connection.Database;
				metrics["DataSource"] = connection.DataSource;
				metrics["Host"] = connection.Host ?? "Unknown";
				metrics["Port"] = connection.Port;
				metrics["ProcessId"] = connection.ProcessID;

				// Get additional server info
				await using var command = connection.CreateCommand();
				command.CommandText = """
				                      				SELECT
				                      					version() AS version,
				                      					current_database() AS database,
				                      					current_user AS user,
				                      					inet_server_addr() AS server_addr,
				                      					inet_server_port() AS server_port,
				                      					pg_is_in_recovery() AS is_replica,
				                      					(SELECT count(*) FROM pg_stat_activity) AS active_connections,
				                      					(SELECT count(*) FROM pg_stat_activity WHERE state = 'idle') AS idle_connections
				                      """;

				await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					metrics["FullVersion"] = reader["version"]?.ToString() ?? "Unknown";
					metrics["CurrentDatabase"] = reader["database"]?.ToString() ?? "Unknown";
					metrics["CurrentUser"] = reader["user"]?.ToString() ?? "Unknown";
					metrics["ServerAddress"] = reader["server_addr"]?.ToString() ?? "Unknown";
					metrics["ServerPort"] = reader["server_port"]?.ToString() ?? "Unknown";
					metrics["IsReplica"] = reader["is_replica"];
					metrics["ActiveConnections"] = reader["active_connections"];
					metrics["IdleConnections"] = reader["idle_connections"];
				}
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveMetrics(ex);
		}

		return metrics;
	}

	/// <inheritdoc />
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		LogInitializingProvider(Name);

		// Test the connection
		if (!await TestConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException($"Failed to initialize Postgres provider '{Name}': Connection test failed");
		}

		_initialized = true;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stats = new Dictionary<string, object>(StringComparer.Ordinal);

			// Get Postgres connection pool statistics
			NpgsqlConnection connection;
			if (_dataSource != null)
			{
				connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				// CA2000: Exception safety is handled - connection is disposed in catch block if OpenAsync throws. Ownership is transferred
				// to await using block immediately after this else block.
#pragma warning disable CA2000 // Dispose objects before losing scope
				connection = new NpgsqlConnection(ConnectionString);
#pragma warning restore CA2000 // Dispose objects before losing scope
				try
				{
					await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					connection.Dispose();
					throw;
				}
			}

			await using (connection)
			{
				await using var command = connection.CreateCommand();
				command.CommandText = """
				                      				SELECT
				                      					count(*) AS total_connections,
				                      					count(*) FILTER (WHERE state = 'active') AS active_connections,
				                      					count(*) FILTER (WHERE state = 'idle') AS idle_connections,
				                      					count(*) FILTER (WHERE state = 'idle in transaction') AS idle_in_transaction,
				                      					max(backend_start) AS oldest_connection,
				                      					min(backend_start) AS newest_connection,
				                      					max(EXTRACT(EPOCH FROM (now() - backend_start))) AS max_connection_age_seconds
				                      				FROM pg_stat_activity
				                      				WHERE datname = current_database()
				                      """;

				await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					stats["TotalConnections"] = reader["total_connections"];
					stats["ActiveConnections"] = reader["active_connections"];
					stats["IdleConnections"] = reader["idle_connections"];
					stats["IdleInTransaction"] = reader["idle_in_transaction"];
					stats["OldestConnection"] = reader["oldest_connection"];
					stats["NewestConnection"] = reader["newest_connection"];
					stats["MaxConnectionAgeSeconds"] = reader["max_connection_age_seconds"];
				}

				// Add configuration stats
				stats["MinPoolSize"] = _options.MinPoolSize;
				stats["MaxPoolSize"] = _options.MaxPoolSize;
				stats["PoolingEnabled"] = _options.EnablePooling;

				// Add NpgsqlDataSource stats if available
				// Note: Statistics property may not be accessible in newer Npgsql versions We'll rely on pg_stat_activity query above for
				// connection stats
			}

			return stats;
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveConnectionPoolStatistics(ex);
			return null;
		}
	}

	/// <inheritdoc />
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderTransaction))
		{
			return this;
		}

		return null;
	}

	/// <summary>
	/// Creates a database connection.
	/// </summary>
	/// <returns> A new database connection. </returns>
	public IDbConnection CreateConnection()
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		NpgsqlConnection connection;

		if (_dataSource != null)
		{
			connection = _dataSource.CreateConnection();
		}
		else
		{
			connection = new NpgsqlConnection(ConnectionString);
		}

		if (_options.OpenConnectionImmediately)
		{
			connection.Open();
		}

		return connection;
	}

	/// <summary>
	/// Creates a database connection asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A new database connection. </returns>
	// CA2000: Factory method pattern - ownership of created connection is transferred to caller who is responsible for disposal. If
	// OpenAsync throws, the connection is disposed in the catch block before rethrowing.
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Factory method transfers ownership to caller. Connection is disposed on exception path.")]
	public async ValueTask<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		NpgsqlConnection connection;

		if (_dataSource != null)
		{
			connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
		}
		else
		{
			connection = new NpgsqlConnection(ConnectionString);
			if (_options.OpenConnectionImmediately)
			{
				try
				{
					await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
				}
				catch
				{
					connection.Dispose();
					throw;
				}
			}
		}

		return connection;
	}

	/// <summary>
	/// Executes a command with retry policy.
	/// </summary>
	/// <typeparam name="T"> The result type. </typeparam>
	/// <param name="func"> The function to execute. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> The execution result. </returns>
	public async Task<T> ExecuteWithRetryAsync<T>(
		Func<CancellationToken, Task<T>> func,
		CancellationToken cancellationToken) =>
		await _retryPolicy.ExecuteAsync(func, cancellationToken).ConfigureAwait(false);

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposingProvider(Name);

		try
		{
			_dataSource?.Dispose();

			// Clear connection pool if configured
			if (_options.ClearPoolOnDispose)
			{
				NpgsqlConnection.ClearAllPools();
				LogClearedConnectionPools();
			}
		}
		catch (Exception ex)
		{
			LogErrorDisposingProvider(ex);
		}
	}

	/// <inheritdoc />
	public async ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;
		LogDisposingProvider(Name);

		try
		{
			if (_dataSource != null)
			{
				await _dataSource.DisposeAsync().ConfigureAwait(false);
			}

			// Clear connection pool if configured
			if (_options.ClearPoolOnDispose)
			{
				// ClearAllPoolsAsync doesn't exist in newer Npgsql versions Use synchronous version wrapped in Task.Run for async disposal
				await Task.Run(static () => NpgsqlConnection.ClearAllPools()).ConfigureAwait(false);
				LogClearedConnectionPools();
			}
		}
		catch (Exception ex)
		{
			LogErrorDisposingProvider(ex);
		}
	}

	/// <summary>
	/// Determines if a Postgres exception is transient.
	/// </summary>
	/// <param name="exception"> The Postgres exception. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(NpgsqlException exception)
	{
		// Postgres error codes that are considered transient https://www.Postgres.org/docs/current/errcodes-appendix.html
		string[] transientErrorCodes =
		[
			"08000", "08003", "08006", "08001", "08004", // Connection exceptions
			"40001", "40P01", // Serialization failure / Deadlock
			"53000", "53100", "53200", "53300", "53400", // Insufficient resources
			"57P01", "57P02", "57P03", // Admin shutdown / Crash shutdown / Cannot connect
			"58000", "58030", // System error / IO error
			"XX000", // Internal error
		];

		return exception.SqlState != null && transientErrorCodes.Any(exception.SqlState.StartsWith);
	}

	/// <summary>
	/// Determines if a Postgres exception is transient.
	/// </summary>
	/// <param name="exception"> The Postgres exception. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(PostgresException exception)
	{
		// Postgres error codes that are considered transient
		string[] transientErrorCodes =
		[
			"08000", "08003", "08006", "08001", "08004", // Connection exceptions
			"40001", "40P01", // Serialization failure / Deadlock
			"53000", "53100", "53200", "53300", "53400", // Insufficient resources
			"57P01", "57P02", "57P03", // Admin shutdown / Crash shutdown / Cannot connect
			"58000", "58030", // System error / IO error
			"XX000", // Internal error
		];

		return exception.SqlState != null && transientErrorCodes.Any(exception.SqlState.StartsWith);
	}

	// Source-generated logging methods
	[LoggerMessage(DataPostgresEventId.PersistenceRetryAttempt, LogLevel.Warning,
		"Postgres operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogRetryAttempt(int retryCount, double timeSpan, Exception ex);

	[LoggerMessage(DataPostgresEventId.ExecutingDataRequest, LogLevel.Debug,
		"Executing data request of type {RequestType}")]
	private partial void LogExecutingDataRequest(string requestType);

	[LoggerMessage(DataPostgresEventId.ExecutingDataRequestInTransaction, LogLevel.Debug,
		"Executing data request of type {RequestType} in transaction {TransactionId}")]
	private partial void LogExecutingDataRequestInTransaction(string requestType, string transactionId);

	[LoggerMessage(DataPostgresEventId.FailedToExecuteDataRequest, LogLevel.Error,
		"Failed to execute data request in transaction {TransactionId}")]
	private partial void LogFailedToExecuteDataRequest(string transactionId, Exception ex);

	[LoggerMessage(DataPostgresEventId.ConnectionTestSuccessful, LogLevel.Information,
		"Postgres connection test successful for '{Name}'")]
	private partial void LogConnectionTestSuccessful(string name);

	[LoggerMessage(DataPostgresEventId.ConnectionTestFailed, LogLevel.Error,
		"Postgres connection test failed for '{Name}'")]
	private partial void LogConnectionTestFailed(string name, Exception ex);

	[LoggerMessage(DataPostgresEventId.FailedToRetrieveMetrics, LogLevel.Warning,
		"Failed to retrieve complete Postgres metrics")]
	private partial void LogFailedToRetrieveMetrics(Exception ex);

	[LoggerMessage(DataPostgresEventId.InitializingProvider, LogLevel.Information,
		"Initializing Postgres persistence provider '{Name}'")]
	private partial void LogInitializingProvider(string name);

	[LoggerMessage(DataPostgresEventId.FailedToRetrieveConnectionPoolStatistics, LogLevel.Warning,
		"Failed to retrieve Postgres connection pool statistics")]
	private partial void LogFailedToRetrieveConnectionPoolStatistics(Exception ex);

	[LoggerMessage(DataPostgresEventId.DisposingProvider, LogLevel.Debug,
		"Disposing Postgres provider '{Name}'")]
	private partial void LogDisposingProvider(string name);

	[LoggerMessage(DataPostgresEventId.ClearedConnectionPools, LogLevel.Debug,
		"Cleared Postgres connection pools")]
	private partial void LogClearedConnectionPools();

	[LoggerMessage(DataPostgresEventId.ErrorDisposingProvider, LogLevel.Warning,
		"Error disposing Postgres provider")]
	private partial void LogErrorDisposingProvider(Exception ex);
}
