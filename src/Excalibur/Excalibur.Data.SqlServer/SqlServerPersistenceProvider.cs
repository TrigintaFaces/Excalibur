// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Data.SqlServer.Persistence;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.SqlServer;

/// <summary>
/// SQL Server implementation of the persistence provider.
/// </summary>
public sealed partial class SqlServerPersistenceProvider : ISqlPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly SqlServerProviderOptions _options;
	private readonly ILogger<SqlServerPersistenceProvider> _logger;
	private readonly AsyncRetryPolicy _retryPolicy;
	private readonly SqlServerRetryPolicy _dataRequestRetryPolicy;
	private volatile bool _disposed;
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The SQL Server provider options. </param>
	/// <param name="logger"> The logger instance. </param>
	public SqlServerPersistenceProvider(
		IOptions<SqlServerProviderOptions> options,
		ILogger<SqlServerPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			throw new ArgumentException("Connection string is required.", nameof(options));
		}

		// Build connection string with additional options
		var builder = new SqlConnectionStringBuilder(_options.ConnectionString)
		{
			ConnectTimeout = _options.ConnectTimeout,
			CommandTimeout = _options.CommandTimeout,
			MultipleActiveResultSets = _options.EnableMars,
			Encrypt = _options.Encrypt,
			TrustServerCertificate = _options.TrustServerCertificate,
			ApplicationName = _options.ApplicationName ?? "Excalibur.Data",
			MinPoolSize = _options.MinPoolSize,
			MaxPoolSize = _options.MaxPoolSize,
			Pooling = _options.EnablePooling,
			LoadBalanceTimeout = _options.LoadBalanceTimeout,
		};

		ConnectionString = builder.ConnectionString;
		Name = _options.Name ?? "SqlServer";
		ProviderType = "SQL";

		// Initialize retry policy for DataRequests
		_dataRequestRetryPolicy = new SqlServerRetryPolicy(_options, _logger);

		// Setup retry policy for transient failures
		_retryPolicy = Policy
			.Handle<SqlException>(IsTransientError)
			.WaitAndRetryAsync(
				_options.RetryCount,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, _) =>
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
	public string DatabaseType => "SqlServer";

	/// <inheritdoc />
	public string DatabaseVersion => "Unknown"; // Will be populated during initialization

	/// <inheritdoc />
	public bool SupportsBulkOperations => true;

	/// <inheritdoc />
	public bool SupportsStoredProcedures => true;

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

		LogExecutingInTransaction(request.GetType().Name, transactionScope.TransactionId);

		try
		{
			// Enlist this provider in the transaction
			await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

			// Execute the request with retry policy
			return await _dataRequestRetryPolicy.ResolveAsync(
				request,
				async () => (TConnection)await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);
		}
		catch (Exception ex)
		{
			LogFailedToExecuteInTransaction(transactionScope.TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		return new SqlServerTransactionScope(
			isolationLevel,
			timeout ?? TimeSpan.FromSeconds(30),
			_logger as ILogger<SqlServerTransactionScope> ??
			NullLogger<SqlServerTransactionScope>.Instance);
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _retryPolicy.ExecuteAsync(
				async ct =>
				{
					var connection = new SqlConnection(ConnectionString);
					await using (connection.ConfigureAwait(false))
					{
						await connection.OpenAsync(ct).ConfigureAwait(false);

						await using var command = connection.CreateCommand();
						command.CommandText = "SELECT 1";
						command.CommandTimeout = 5;

						_ = await command.ExecuteScalarAsync(ct).ConfigureAwait(false);
					}
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
		var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "SqlServer",
			["Name"] = Name,
			["IsAvailable"] = IsAvailable,
			["EnableMars"] = _options.EnableMars,
			["Encrypt"] = _options.Encrypt,
			["MinPoolSize"] = _options.MinPoolSize,
			["MaxPoolSize"] = _options.MaxPoolSize,
			["EnablePooling"] = _options.EnablePooling,
			["CommandTimeout"] = _options.CommandTimeout,
			["ConnectTimeout"] = _options.ConnectTimeout,
		};

		try
		{
			var connection = new SqlConnection(ConnectionString);
			await using (connection.ConfigureAwait(false))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				metrics["ServerVersion"] = connection.ServerVersion;
				metrics["Database"] = connection.Database;
				metrics["DataSource"] = connection.DataSource;
				metrics["WorkstationId"] = connection.WorkstationId;

				// Get additional server info
				await using var command = connection.CreateCommand();
				command.CommandText = """
				                      				SELECT
				                      					SERVERPROPERTY('ProductVersion') AS Version,
				                      					SERVERPROPERTY('ProductLevel') AS Level,
				                      					SERVERPROPERTY('Edition') AS Edition,
				                      					SERVERPROPERTY('EngineEdition') AS EngineEdition,
				                      					@@TOTAL_READ AS TotalReads,
				                      					@@TOTAL_WRITE AS TotalWrites,
				                      					@@CONNECTIONS AS Connections
				                      """;

				await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					metrics["ProductVersion"] = reader["Version"]?.ToString() ?? "Unknown";
					metrics["ProductLevel"] = reader["Level"]?.ToString() ?? "Unknown";
					metrics["Edition"] = reader["Edition"]?.ToString() ?? "Unknown";
					metrics["EngineEdition"] = reader["EngineEdition"]?.ToString() ?? "Unknown";
					metrics["TotalReads"] = reader["TotalReads"];
					metrics["TotalWrites"] = reader["TotalWrites"];
					metrics["Connections"] = reader["Connections"];
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
			throw new InvalidOperationException($"Failed to initialize SQL Server provider '{Name}': Connection test failed");
		}

		_initialized = true;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<object>();
		var requestList = requests.ToList();

		LogExecutingBatch(requestList.Count);

		foreach (var request in requestList)
		{
			var result = await _dataRequestRetryPolicy.ResolveAsync(
				request,
				async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var results = new List<object>();
		var requestList = requests.ToList();

		LogExecutingBatchInTransaction(requestList.Count, transactionScope.TransactionId);

		await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

		foreach (var request in requestList)
		{
			var result = await _dataRequestRetryPolicy.ResolveAsync(
				request,
				async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
				cancellationToken).ConfigureAwait(false);
			results.Add(result);
		}

		return results;
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteBulkAsync<TResult>(
		IDataRequest<IDbConnection, TResult> bulkRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(bulkRequest);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingBulkDataRequest(bulkRequest.GetType().Name);

		return await _dataRequestRetryPolicy.ResolveAsync(
			bulkRequest,
			async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteStoredProcedureAsync<TResult>(
		IDataRequest<IDbConnection, TResult> storedProcedureRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(storedProcedureRequest);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingStoredProcedureRequest(storedProcedureRequest.GetType().Name);

		return await _dataRequestRetryPolicy.ResolveAsync(
			storedProcedureRequest,
			async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken)
	{
		var stats = new Dictionary<string, object>(StringComparer.Ordinal);

		try
		{
			var connection = new SqlConnection(ConnectionString);
			await using (connection.ConfigureAwait(false))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				await using var command = connection.CreateCommand();
				command.CommandText = """
				                      				SELECT
				                      					@@SERVERNAME AS ServerName,
				                      					@@VERSION AS Version,
				                      					DB_NAME() AS DatabaseName,
				                      					(SELECT COUNT(*) FROM sys.databases) AS DatabaseCount,
				                      					(SELECT COUNT(*) FROM sys.dm_exec_connections) AS ConnectionCount
				                      """;

				await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					stats["ServerName"] = reader["ServerName"]?.ToString() ?? "Unknown";
					stats["Version"] = reader["Version"]?.ToString() ?? "Unknown";
					stats["DatabaseName"] = reader["DatabaseName"]?.ToString() ?? "Unknown";
					stats["DatabaseCount"] = reader["DatabaseCount"];
					stats["ConnectionCount"] = reader["ConnectionCount"];
				}
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveDatabaseStatistics(ex);
		}

		return stats;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetSchemaInfoAsync(string tableName, string? schemaName,
		CancellationToken cancellationToken)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(tableName);

		var schemaInfo = new Dictionary<string, object>(StringComparer.Ordinal);
		schemaName ??= "dbo";

		try
		{
			var connection = new SqlConnection(ConnectionString);
			await using (connection.ConfigureAwait(false))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				await using var command = connection.CreateCommand();
				command.CommandText = """
				                      				SELECT
				                      					COLUMN_NAME,
				                      					DATA_TYPE,
				                      					IS_NULLABLE,
				                      					COLUMN_DEFAULT,
				                      					CHARACTER_MAXIMUM_LENGTH,
				                      					NUMERIC_PRECISION,
				                      					NUMERIC_SCALE
				                      				FROM INFORMATION_SCHEMA.COLUMNS
				                      				WHERE TABLE_NAME = @TableName AND TABLE_SCHEMA = @SchemaName
				                      				ORDER BY ORDINAL_POSITION
				                      """;

				_ = command.Parameters.Add(new SqlParameter("@TableName", tableName));
				_ = command.Parameters.Add(new SqlParameter("@SchemaName", schemaName));

				var columns = new List<Dictionary<string, object>>();
				await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
				while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
				{
					columns.Add(new Dictionary<string, object>(StringComparer.Ordinal)
					{
						["ColumnName"] = reader["COLUMN_NAME"]?.ToString() ?? string.Empty,
						["DataType"] = reader["DATA_TYPE"]?.ToString() ?? string.Empty,
						["IsNullable"] = reader["IS_NULLABLE"]?.ToString() ?? "NO",
						["DefaultValue"] = reader["COLUMN_DEFAULT"]?.ToString() ?? string.Empty,
						["MaxLength"] = reader["CHARACTER_MAXIMUM_LENGTH"],
						["NumericPrecision"] = reader["NUMERIC_PRECISION"],
						["NumericScale"] = reader["NUMERIC_SCALE"],
					});
				}

				schemaInfo["Columns"] = columns;
				schemaInfo["TableName"] = tableName;
				schemaInfo["SchemaName"] = schemaName;
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveSchemaInfo(tableName, ex);
		}

		return schemaInfo;
	}

	/// <inheritdoc />
	public bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request) => request != null;

	/// <inheritdoc />
	public async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stats = new Dictionary<string, object>(StringComparer.Ordinal);

			// Get SQL Server connection pool statistics
			var connection = new SqlConnection(ConnectionString);

			// Get SQL Server connection pool statistics
			await using (connection.ConfigureAwait(false))
			{
				await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

				var command = connection.CreateCommand();
				await using (command.ConfigureAwait(false))
				{
					command.CommandText = """
					                      				SELECT
					                      					COUNT(*) AS TotalConnections,
					                      					SUM(CASE WHEN status = 'sleeping' THEN 1 ELSE 0 END) AS IdleConnections,
					                      					SUM(CASE WHEN status != 'sleeping' THEN 1 ELSE 0 END) AS ActiveConnections,
					                      					MAX(connect_time) AS OldestConnection,
					                      					MIN(connect_time) AS NewestConnection
					                      				FROM sys.dm_exec_connections
					                      				WHERE client_net_address IS NOT NULL
					                      """;

					await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
					if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
					{
						stats["TotalConnections"] = reader["TotalConnections"];
						stats["IdleConnections"] = reader["IdleConnections"];
						stats["ActiveConnections"] = reader["ActiveConnections"];
						stats["OldestConnection"] = reader["OldestConnection"];
						stats["NewestConnection"] = reader["NewestConnection"];
					}

					// Add configuration stats
					stats["MinPoolSize"] = _options.MinPoolSize;
					stats["MaxPoolSize"] = _options.MaxPoolSize;
					stats["PoolingEnabled"] = _options.EnablePooling;

					return stats;
				}
			}
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

		var connection = new SqlConnection(ConnectionString);

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
	public async ValueTask<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var connection = new SqlConnection(ConnectionString);

		if (_options.OpenConnectionImmediately)
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
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

		// Clear connection pool if configured
		if (_options.ClearPoolOnDispose)
		{
			try
			{
				SqlConnection.ClearAllPools();
				LogClearedConnectionPools();
			}
			catch (Exception ex)
			{
				LogFailedToClearConnectionPools(ex);
			}
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}

	/// <summary>
	/// Determines if a SQL exception is transient.
	/// </summary>
	/// <param name="exception"> The SQL exception. </param>
	/// <returns> True if the error is transient; otherwise, false. </returns>
	private static bool IsTransientError(SqlException exception)
	{
		// List of SQL error numbers that are considered transient https://docs.microsoft.com/en-us/azure/azure-sql/database/troubleshoot-common-errors-issues
		int[] transientErrors =
		[
			49918, 49919, 49920, // Resource governance errors
			4060, 4221, // Login failures
			40143, 40613, 40501, 40540, 40197, // Service errors
			10928, 10929, // Resource limit errors
			20, 64, 233, // Connection errors
			8645, 8651, 8657, // Memory errors
			1204, 1205, 1222, // Lock/deadlock errors
			-2, 2, 53, // Network errors
			701, 802, 8645, 8651, // Memory pressure
			617, 669, 671, // Other resource errors
		];

		return transientErrors.Contains(exception.Number);
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.RootRetryAttempt, LogLevel.Warning,
		"SQL Server operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogRetryAttempt(int retryCount, double timeSpan, Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootExecutingDataRequest, LogLevel.Debug,
		"Executing data request of type {RequestType}")]
	private partial void LogExecutingDataRequest(string requestType);

	[LoggerMessage(DataSqlServerEventId.RootExecutingInTransaction, LogLevel.Debug,
		"Executing data request of type {RequestType} in transaction {TransactionId}")]
	private partial void LogExecutingInTransaction(string requestType, string transactionId);

	[LoggerMessage(DataSqlServerEventId.RootConnectionTestSuccessful, LogLevel.Information,
		"SQL Server connection test successful for '{Name}'")]
	private partial void LogConnectionTestSuccessful(string name);

	[LoggerMessage(DataSqlServerEventId.RootConnectionTestFailed, LogLevel.Error,
		"SQL Server connection test failed for '{Name}'")]
	private partial void LogConnectionTestFailed(string name, Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootExecutingBatchInTransaction, LogLevel.Debug,
		"Executing batch of {Count} data requests in transaction {TransactionId}")]
	private partial void LogExecutingBatchInTransaction(int count, string transactionId);

	[LoggerMessage(DataSqlServerEventId.RootDisposingProvider, LogLevel.Debug,
		"Disposing SQL Server provider '{Name}'")]
	private partial void LogDisposingProvider(string name);

	[LoggerMessage(DataSqlServerEventId.RootExecutingBatch, LogLevel.Debug,
		"Executing batch of {Count} data requests")]
	private partial void LogExecutingBatch(int count);

	[LoggerMessage(DataSqlServerEventId.RootExecutingBulkDataRequest, LogLevel.Debug,
		"Executing bulk data request of type {RequestType}")]
	private partial void LogExecutingBulkDataRequest(string requestType);

	[LoggerMessage(DataSqlServerEventId.RootExecutingStoredProcedure, LogLevel.Debug,
		"Executing stored procedure data request of type {RequestType}")]
	private partial void LogExecutingStoredProcedureRequest(string requestType);

	[LoggerMessage(DataSqlServerEventId.RootFailedToRetrieveDatabaseStatistics, LogLevel.Warning,
		"Failed to retrieve database statistics")]
	private partial void LogFailedToRetrieveDatabaseStatistics(Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootFailedToRetrieveSchemaInfo, LogLevel.Warning,
		"Failed to retrieve schema info for table {TableName}")]
	private partial void LogFailedToRetrieveSchemaInfo(string tableName, Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootFailedToRetrieveConnectionPoolStatistics, LogLevel.Warning,
		"Failed to retrieve connection pool statistics")]
	private partial void LogFailedToRetrieveConnectionPoolStatistics(Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootClearedConnectionPools, LogLevel.Information,
		"Cleared SQL Server connection pools")]
	private partial void LogClearedConnectionPools();

	[LoggerMessage(DataSqlServerEventId.RootFailedToClearConnectionPools, LogLevel.Warning,
		"Failed to clear SQL Server connection pools")]
	private partial void LogFailedToClearConnectionPools(Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootFailedToExecuteInTransaction, LogLevel.Error,
		"Failed to execute data request in transaction {TransactionId}")]
	private partial void LogFailedToExecuteInTransaction(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootFailedToRetrieveMetrics, LogLevel.Warning,
		"Failed to retrieve provider metrics")]
	private partial void LogFailedToRetrieveMetrics(Exception ex);

	[LoggerMessage(DataSqlServerEventId.RootInitializingProvider, LogLevel.Information,
		"Initializing SQL Server provider '{Name}'")]
	private partial void LogInitializingProvider(string name);
}
