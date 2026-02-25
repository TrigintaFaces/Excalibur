// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

using Dapper;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.SqlServer.Diagnostics;
using Excalibur.Data.SqlServer.TypeHandlers;
using Excalibur.Dispatch.Abstractions.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// SQL Server implementation of the persistence provider focused on DataRequest execution with SQL Server-specific optimizations and
/// resilience features.
/// </summary>
/// <remarks>
/// This provider serves as a thin wrapper around the DataRequest infrastructure, adding value through SQL Server-specific features like
/// bulk operations, stored procedures, and advanced transaction support.
/// </remarks>
[SuppressMessage(
	"Maintainability",
	"CA1506:Avoid excessive class coupling",
	Justification = "SQL persistence providers inherently couple with many SDK, System.Data, and abstraction types.")]
public partial class SqlServerPersistenceProvider : ISqlPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly ILogger<SqlServerPersistenceProvider> _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly SqlServerPersistenceOptions _options;
	private readonly SqlServerPersistenceMetrics _metrics;

	// IDE0052: Reserved for future policy-based authorization enforcement
#pragma warning disable IDE0052
	private readonly SqlDataAccessPolicyFactory _policyFactory;
#pragma warning restore IDE0052
	private readonly SemaphoreSlim _connectionSemaphore;
	private bool _isInitialized;
	private volatile bool _disposed;
	private string? _databaseVersion;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerPersistenceProvider" /> class.
	/// </summary>
	/// <param name="options"> The configuration options. </param>
	/// <param name="logger"> The logger instance. </param>
	/// <param name="loggerFactory"> The logger factory for creating component loggers. </param>
	/// <param name="metrics"> The metrics collector. </param>
	/// <param name="retryPolicy"> The retry policy for DataRequest operations. </param>
	/// <param name="policyFactory"> The SQL-specific policy factory. </param>
	public SqlServerPersistenceProvider(
		IOptions<SqlServerPersistenceOptions> options,
		ILogger<SqlServerPersistenceProvider> logger,
		ILoggerFactory loggerFactory,
		SqlServerPersistenceMetrics metrics,
		IDataRequestRetryPolicy retryPolicy,
		SqlDataAccessPolicyFactory policyFactory)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		RetryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
		_policyFactory = policyFactory ?? throw new ArgumentNullException(nameof(policyFactory));

		_options.Validate();

		// Initialize SQL Server-specific type handlers for Dapper
		SqlMapper.AddTypeHandler(new MoneyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableMoneyTypeHandler());
		SqlMapper.AddTypeHandler(new DateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableDateOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new TimeOnlyTypeHandler());
		SqlMapper.AddTypeHandler(new NullableTimeOnlyTypeHandler());

		if (_options.EnableDetailedLogging)
		{
			LogProviderInitialized(_options.MaxPoolSize, _options.CommandTimeout,
				_options.Security.EnableAlwaysEncrypted, _options.Connection.EnableMars, _options.MaxRetryAttempts);
		}

		_connectionSemaphore = new SemaphoreSlim(_options.MaxPoolSize, _options.MaxPoolSize);
	}

	/// <inheritdoc />
	public string Name => "SqlServerPersistenceProvider";

	/// <inheritdoc />
	public string ProviderType => "SQL";

	/// <inheritdoc />
	public string DatabaseType => "SqlServer";

	/// <inheritdoc />
	public string DatabaseVersion => _databaseVersion ?? "Unknown";

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

	/// <summary>
	/// Creates a new SQL Server database connection without opening it.
	/// </summary>
	/// <returns> A new <see cref="SqlConnection" /> instance. </returns>
	/// <remarks>
	/// The connection is not opened. Call <see cref="CreateConnectionAsync" /> if you need an opened connection, or call
	/// <see cref="IDbConnection.Open" /> manually.
	/// </remarks>
	public IDbConnection CreateConnection()
	{
		var connectionStringBuilder = BuildConnectionString();
		var connection = new SqlConnection(connectionStringBuilder.ConnectionString);

		if (_options.EnableDetailedLogging)
		{
			LogConnectionCreated();
		}

		_metrics.RecordConnectionCreated();
		return connection;
	}

	/// <summary>
	/// Creates and opens a new SQL Server database connection asynchronously.
	/// </summary>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> An opened <see cref="SqlConnection" /> instance. </returns>
	/// <remarks>
	/// This method manages connection pooling through a semaphore and ensures the connection is properly opened before returning it.
	/// </remarks>
	public async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var connectionStringBuilder = BuildConnectionString();
			var connection = new SqlConnection(connectionStringBuilder.ConnectionString);

			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			if (_options.EnableDetailedLogging)
			{
				LogConnectionOpened();
			}

			_metrics.RecordConnectionCreated();
			return connection;
		}
		finally
		{
			_ = _connectionSemaphore.Release();
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteAsync<TConnection, TResult>(
		IDataRequest<TConnection, TResult> request,
		CancellationToken cancellationToken)
		where TConnection : IDisposable
	{
		ArgumentNullException.ThrowIfNull(request);

		// Ensure the request is compatible with IDbConnection
		if (typeof(TConnection) != typeof(IDbConnection))
		{
			throw new ArgumentException(
				$"Request connection type {typeof(TConnection).Name} is not compatible with SQL Server provider. Expected IDbConnection.",
				nameof(request));
		}

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteDataRequest");
		_ = (activity?.SetTag("request.type", request.GetType().Name));

		return await RetryPolicy.ResolveAsync(
			(IDataRequest<IDbConnection, TResult>)request,
			async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
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

		// Ensure the request is compatible with IDbConnection
		if (typeof(TConnection) != typeof(IDbConnection))
		{
			throw new ArgumentException(
				$"Request connection type {typeof(TConnection).Name} is not compatible with SQL Server provider. Expected IDbConnection.",
				nameof(request));
		}

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteInTransaction");
		_ = (activity?.SetTag("request.type", request.GetType().Name));
		_ = (activity?.SetTag("transaction.id", transactionScope.TransactionId));

		// Connection lifecycle is managed by the transaction scope after enlistment - scope owns disposal R0.8: Dispose objects before
		// losing scope
#pragma warning disable CA2000
		var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000

		var stopwatch = ValueStopwatch.StartNew();

		try
		{
			// Cast the request to work with IDbConnection
			var dbRequest = (IDataRequest<IDbConnection, TResult>)request;
			var result = await dbRequest.ResolveAsync(connection).ConfigureAwait(false);

			_metrics.RecordDataRequestExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true);

			if (_options.EnableDetailedLogging)
			{
				LogDataRequestExecuted(request.GetType().Name, transactionScope.TransactionId,
					(long)stopwatch.Elapsed.TotalMilliseconds);
			}

			return result;
		}
		catch (Exception ex)
		{
			_metrics.RecordDataRequestExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false);
			LogDataRequestError(request.GetType().Name, transactionScope.TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		var actualTimeout = timeout ?? TimeSpan.FromMinutes(_options.CommandTimeout);

		var transactionScope = new SqlServerTransactionScope(
			isolationLevel,
			actualTimeout,
			_loggerFactory.CreateLogger<SqlServerTransactionScope>());

		if (_options.EnableDetailedLogging)
		{
			LogTransactionScopeCreated(transactionScope.TransactionId, isolationLevel);
		}

		return transactionScope;
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);

		var requestList = requests.ToList();
		if (requestList.Count == 0)
		{
			return [];
		}

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteBatch");
		_ = (activity?.SetTag("batch.size", requestList.Count));

		using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		if (connection is not SqlConnection sqlConnection)
		{
			throw new InvalidOperationException("Failed to begin batch transaction.");
		}

		var transaction = await sqlConnection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
		await using (transaction.ConfigureAwait(false))
		{
			var stopwatch = ValueStopwatch.StartNew();
			var results = new List<object>();

			try
			{
				foreach (var request in requestList)
				{
					var result = await request.ResolveAsync(connection).ConfigureAwait(false);
					results.Add(result);
				}

				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

				_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true, requestList.Count, results.Count);

				if (_options.EnableDetailedLogging)
				{
					LogBatchExecuted(requestList.Count, (long)stopwatch.Elapsed.TotalMilliseconds);
				}

				return results;
			}
			catch (Exception ex)
			{
				await transaction.RollbackAsync().ConfigureAwait(false);
				_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false, requestList.Count, 0);
				LogBatchError(ex);
				throw;
			}
		}
	}

	/// <inheritdoc />
	public async Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);

		ArgumentNullException.ThrowIfNull(transactionScope);

		var requestList = requests.ToList();
		if (requestList.Count == 0)
		{
			return [];
		}

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteBatchInTransaction");
		_ = (activity?.SetTag("batch.size", requestList.Count));
		_ = (activity?.SetTag("transaction.id", transactionScope.TransactionId));

		// Connection lifecycle is managed by the transaction scope after enlistment - scope owns disposal R0.8: Dispose objects before
		// losing scope
#pragma warning disable CA2000
		var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000

		var stopwatch = ValueStopwatch.StartNew();
		var results = new List<object>();

		try
		{
			foreach (var request in requestList)
			{
				var result = await request.ResolveAsync(connection).ConfigureAwait(false);
				results.Add(result);
			}

			_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: true, requestList.Count, results.Count);

			if (_options.EnableDetailedLogging)
			{
				LogBatchInTransactionExecuted(requestList.Count, transactionScope.TransactionId,
					(long)stopwatch.Elapsed.TotalMilliseconds);
			}

			return results;
		}
		catch (Exception ex)
		{
			_metrics.RecordBatchExecution((long)stopwatch.Elapsed.TotalMilliseconds, success: false, requestList.Count, 0);
			LogBatchInTransactionError(transactionScope.TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task<TResult> ExecuteBulkAsync<TResult>(
		IDataRequest<IDbConnection, TResult> bulkRequest,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(bulkRequest);

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteBulk");
		_ = (activity?.SetTag("request.type", bulkRequest.GetType().Name));

		return await RetryPolicy.ResolveAsync(
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

		using var activity = Activity.Current?.Source.StartActivity("SqlServer.ExecuteStoredProcedure");
		_ = (activity?.SetTag("request.type", storedProcedureRequest.GetType().Name));

		return await RetryPolicy.ResolveAsync(
			storedProcedureRequest,
			async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
			cancellationToken).ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetDatabaseStatisticsAsync(CancellationToken cancellationToken)
	{
		const string statsQuery = """
		                           SELECT
		                           @@VERSION AS Version,
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

		using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		var stats = await connection.QuerySingleAsync<dynamic>(statsQuery).ConfigureAwait(false);

		var result = new Dictionary<string, object>(StringComparer.Ordinal);
		foreach (var property in (IDictionary<string, object>)stats)
		{
			result[property.Key] = property.Value ?? "N/A";
		}

		return result;
	}

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = await _metrics.GetMetricsAsync().ConfigureAwait(false);

		// Add SQL Server-specific metrics
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

	/// <inheritdoc />
	public async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			const string poolQuery = """
			                          SELECT
			                          'SqlServer' as ProviderType,
			                          @@SERVERNAME as ServerName,
			                          DB_NAME() as DatabaseName,
			                          (SELECT COUNT(*) FROM sys.dm_exec_connections) as TotalConnections,
			                          (SELECT COUNT(*) FROM sys.dm_exec_sessions WHERE is_user_process = 1) as UserConnections,
			                          (SELECT COUNT(*) FROM sys.dm_exec_requests) as ActiveRequests,
			                          (SELECT COUNT(*) FROM sys.dm_exec_requests WHERE blocking_session_id > 0) as BlockedRequests,
			                          (SELECT COUNT(*) FROM sys.dm_tran_active_transactions) as ActiveTransactions
			                         """;

			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			var poolStats = await connection.QuerySingleAsync<dynamic>(poolQuery).ConfigureAwait(false);

			var result = new Dictionary<string, object>(StringComparer.Ordinal);
			foreach (var property in (IDictionary<string, object>)poolStats)
			{
				result[property.Key] = property.Value ?? "N/A";
			}

			// Add configured pool settings
			result["ConfiguredMaxPoolSize"] = _options.MaxPoolSize;
			result["ConfiguredMinPoolSize"] = _options.MinPoolSize;
			result["ConnectionPoolingEnabled"] = _options.EnableConnectionPooling;

			return result;
		}
		catch (Exception ex)
		{
			LogConnectionPoolStatsError(ex);
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

	/// <inheritdoc />
	public async Task<IDictionary<string, object>> GetSchemaInfoAsync(
		string tableName,
		string? schemaName,
		CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(tableName))
		{
			throw new ArgumentException("Table name cannot be null or empty.", nameof(tableName));
		}

		schemaName ??= "dbo";

		const string schemaQuery = """
		                            SELECT
		                            c.COLUMN_NAME as ColumnName,
		                            c.DATA_TYPE as DataType,
		                            c.IS_NULLABLE as IsNullable,
		                            c.COLUMN_DEFAULT as DefaultValue,
		                            c.CHARACTER_MAXIMUM_LENGTH as MaxLength,
		                            c.NUMERIC_PRECISION as NumericPrecision,
		                            c.NUMERIC_SCALE as NumericScale,
		                            CASE WHEN pk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsPrimaryKey,
		                            CASE WHEN fk.COLUMN_NAME IS NOT NULL THEN 1 ELSE 0 END as IsForeignKey,
		                            ix.INDEX_NAME as IndexName,
		                            ix.INDEX_TYPE as IndexType
		                            FROM INFORMATION_SCHEMA.COLUMNS c
		                            LEFT JOIN (
		                            SELECT ku.TABLE_NAME, ku.COLUMN_NAME
		                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
		                            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
		                            ON tc.CONSTRAINT_TYPE = 'PRIMARY KEY'
		                            AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
		                            ) pk ON c.TABLE_NAME = pk.TABLE_NAME AND c.COLUMN_NAME = pk.COLUMN_NAME
		                            LEFT JOIN (
		                            SELECT ku.TABLE_NAME, ku.COLUMN_NAME
		                            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS AS tc
		                            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE AS ku
		                            ON tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
		                            AND tc.CONSTRAINT_NAME = ku.CONSTRAINT_NAME
		                            ) fk ON c.TABLE_NAME = fk.TABLE_NAME AND c.COLUMN_NAME = fk.COLUMN_NAME
		                            LEFT JOIN (
		                            SELECT
		                            t.name AS TABLE_NAME,
		                            c.name AS COLUMN_NAME,
		                            i.name AS INDEX_NAME,
		                            i.type_desc AS INDEX_TYPE
		                            FROM sys.indexes i
		                            INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
		                            INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
		                            INNER JOIN sys.tables t ON i.object_id = t.object_id
		                            INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
		                            WHERE s.name = @SchemaName AND t.name = @TableName
		                            ) ix ON c.TABLE_NAME = ix.TABLE_NAME AND c.COLUMN_NAME = ix.COLUMN_NAME
		                            WHERE c.TABLE_SCHEMA = @SchemaName AND c.TABLE_NAME = @TableName
		                            ORDER BY c.ORDINAL_POSITION
		                           """;

		using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		var schemaInfo = await connection.QueryAsync(schemaQuery, new { SchemaName = schemaName, TableName = tableName })
			.ConfigureAwait(false);

		return new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["SchemaName"] = schemaName,
			["TableName"] = tableName,
			["Columns"] = schemaInfo.ToList(),
		};
	}

	/// <inheritdoc />
	public bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request)
	{
		if (request == null)
		{
			return false;
		}

		try
		{
			// Basic validation - check if command is properly formed
			var command = request.Command;
			if (string.IsNullOrWhiteSpace(command.CommandText))
			{
				return false;
			}

			// Check for SQL injection patterns (basic validation)
			var sql = command.CommandText.ToUpperInvariant();

			// Allow these patterns only if they're in proper context (not simple injection)
			foreach (var pattern in new[] { "--", "/*", "*/", "XP_", "SP_", "EXEC(", "EXECUTE(" })
			{
				if (sql.Contains(pattern, StringComparison.Ordinal) && !IsValidSqlPattern(sql, pattern))
				{
					LogUnsafeSqlPattern(pattern);
					return false;
				}
			}

			return true;
		}
		catch (Exception ex)
		{
			LogValidationError(request.GetType().Name, ex);
			return false;
		}
	}

	/// <inheritdoc />
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
			var result = await connection.ExecuteScalarAsync<int>("SELECT 1").ConfigureAwait(false);

			IsAvailable = result == 1;

			if (IsAvailable && string.IsNullOrEmpty(_databaseVersion))
			{
				_databaseVersion = await connection.ExecuteScalarAsync<string>("SELECT @@VERSION").ConfigureAwait(false);
			}

			return IsAvailable;
		}
		catch (Exception ex)
		{
			LogConnectionTestFailed(ex);
			IsAvailable = false;
			return false;
		}
	}

	/// <inheritdoc />
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		if (_isInitialized)
		{
			LogProviderAlreadyInitialized();
			return;
		}

		if (options is SqlServerPersistenceOptions sqlOptions)
		{
			// Update options if provided
			_options.ConnectionString = sqlOptions.ConnectionString;
			_options.CommandTimeout = sqlOptions.CommandTimeout;
			_options.ConnectionTimeout = sqlOptions.ConnectionTimeout;
			_options.MaxRetryAttempts = sqlOptions.MaxRetryAttempts;
			_options.RetryDelayMilliseconds = sqlOptions.RetryDelayMilliseconds;
		}

		_options.Validate();

		// Test the connection
		var isConnected = await TestConnectionAsync(cancellationToken).ConfigureAwait(false);
		if (!isConnected)
		{
			throw new InvalidOperationException("Connection test failed.");
		}

		_isInitialized = true;
		LogProviderInitializedSuccess();
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
		await DisposeCoreAsync().ConfigureAwait(false);
		Dispose(disposing: false);
		GC.SuppressFinalize(this);
	}

	/// <summary>
	/// Creates a SqlBulkCopy instance configured for optimal performance.
	/// </summary>
	/// <param name="connection"> The SQL connection to use. </param>
	/// <param name="transaction"> The transaction to enlist in (optional). </param>
	/// <returns> A configured <see cref="SqlBulkCopy" /> instance. </returns>
	/// <remarks>
	/// This method is used internally by bulk DataRequest operations to provide high-performance bulk insert capabilities specific to SQL Server.
	/// </remarks>
	internal SqlBulkCopy CreateBulkCopy(SqlConnection connection, SqlTransaction? transaction = null)
	{
		var bulkCopyOptions = SqlBulkCopyOptions.Default;

		if (transaction != null)
		{
			bulkCopyOptions |= SqlBulkCopyOptions.CheckConstraints;
		}

		var bulkCopy = transaction != null
			? new SqlBulkCopy(connection, bulkCopyOptions, transaction)
			: new SqlBulkCopy(connection, bulkCopyOptions, externalTransaction: null);

		bulkCopy.BulkCopyTimeout = _options.CommandTimeout;
		bulkCopy.BatchSize = 1000; // Optimize for moderate batch sizes

		return bulkCopy;
	}

	/// <summary>
	/// Creates a table-valued parameter for efficient bulk operations.
	/// </summary>
	/// <param name="typeName"> The SQL Server user-defined table type name. </param>
	/// <param name="data"> The data to include in the parameter. </param>
	/// <returns> A configured <see cref="SqlParameter" /> for the table-valued parameter. </returns>
	/// <remarks> Table-valued parameters provide an efficient way to pass structured data to SQL Server stored procedures and functions. </remarks>
	internal static SqlParameter CreateTableValuedParameter(string typeName, DataTable data) =>
		new() { ParameterName = "@TableParam", SqlDbType = SqlDbType.Structured, TypeName = typeName, Value = data };

	/// <summary>
	/// Performs the async dispose.
	/// </summary>
	protected virtual async ValueTask DisposeCoreAsync()
	{
		if (!_disposed)
		{
			_connectionSemaphore?.Dispose();
			await Task.CompletedTask.ConfigureAwait(false);
			_disposed = true;
		}
	}

	/// <summary>
	/// Performs the dispose.
	/// </summary>
	/// <param name="disposing"> Whether disposing managed resources. </param>
	protected virtual void Dispose(bool disposing)
	{
		if (!_disposed)
		{
			if (disposing)
			{
				_connectionSemaphore?.Dispose();
			}

			_disposed = true;
		}
	}

	/// <summary>
	/// Validates if a SQL pattern is used in a safe context.
	/// </summary>
	/// <param name="sql"> The SQL text to validate. </param>
	/// <param name="pattern"> The pattern to check. </param>
	/// <returns> True if the pattern is safe; otherwise, false. </returns>
	private static bool IsValidSqlPattern(string sql, string pattern) =>
		pattern switch
		{
			"--" => sql.Contains("-- ", StringComparison.Ordinal) ||
					sql.EndsWith("--", StringComparison.Ordinal), // Allow comments with space or at end
			"/*" => sql.Contains("/*", StringComparison.Ordinal) &&
					sql.Contains("*/", StringComparison.Ordinal), // Allow block comments if properly closed
			"*/" => sql.Contains("/*", StringComparison.Ordinal) &&
					sql.Contains("*/", StringComparison.Ordinal), // Allow block comments if properly opened
			"xp_" => false, // Extended procedures are generally not allowed
			"sp_" => sql.Contains("sp_executesql", StringComparison.Ordinal) ||
					 sql.Contains("sp_helpdb", StringComparison.Ordinal), // Allow specific system procedures
			"exec(" => false, // Dynamic SQL execution not allowed
			"execute(" => false, // Dynamic SQL execution not allowed
			_ => true,
		};

	private SqlConnectionStringBuilder BuildConnectionString()
	{
		var builder = new SqlConnectionStringBuilder(_options.ConnectionString)
		{
			ApplicationName = _options.Connection.ApplicationName,
			ConnectTimeout = _options.ConnectionTimeout,
			CommandTimeout = _options.CommandTimeout,
			MultiSubnetFailover = _options.Connection.MultiSubnetFailover,
			TrustServerCertificate = _options.Security.TrustServerCertificate,
			Encrypt = _options.Security.EncryptConnection,
			LoadBalanceTimeout = _options.Connection.LoadBalanceTimeout,
			PacketSize = _options.Connection.PacketSize,
			MultipleActiveResultSets = _options.Connection.EnableMars,
			ConnectRetryCount = _options.Resiliency.ConnectRetryCount,
			ConnectRetryInterval = _options.Resiliency.ConnectRetryInterval,
		};

		if (_options.EnableConnectionPooling)
		{
			builder.Pooling = true;
			builder.MaxPoolSize = _options.MaxPoolSize;
			builder.MinPoolSize = _options.MinPoolSize;
		}
		else
		{
			builder.Pooling = false;
		}

		if (_options.Security.EnableAlwaysEncrypted)
		{
			builder.ColumnEncryptionSetting = _options.Security.ColumnEncryptionSetting == SqlConnectionColumnEncryptionSetting.Enabled
				? Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting.Enabled
				: Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting.Disabled;
		}

		if (_options.Connection.ApplicationIntent == ApplicationIntent.ReadOnly)
		{
			builder.ApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent.ReadOnly;
		}

		if (!string.IsNullOrEmpty(_options.Connection.WorkstationId))
		{
			builder.WorkstationID = _options.Connection.WorkstationId;
		}

		return builder;
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.PersistenceProviderInitialized, LogLevel.Debug,
		"Initialized SQL Server persistence provider with options: MaxPoolSize={MaxPoolSize}, CommandTimeout={CommandTimeout}, EnableAlwaysEncrypted={EnableAlwaysEncrypted}, EnableMars={EnableMars}, MaxRetryAttempts={MaxRetryAttempts}")]
	private partial void LogProviderInitialized(int maxPoolSize, int commandTimeout, bool enableAlwaysEncrypted, bool enableMars, int maxRetryAttempts);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionCreated, LogLevel.Debug,
		"Created new SQL Server connection")]
	private partial void LogConnectionCreated();

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionOpened, LogLevel.Debug,
		"Created and opened new SQL Server connection")]
	private partial void LogConnectionOpened();

	[LoggerMessage(DataSqlServerEventId.PersistenceDataRequestExecuted, LogLevel.Debug,
		"Executed DataRequest {RequestType} in transaction {TransactionId} in {ElapsedMs}ms")]
	private partial void LogDataRequestExecuted(string requestType, string transactionId, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceDataRequestError, LogLevel.Error,
		"Error executing DataRequest {RequestType} in transaction {TransactionId}")]
	private partial void LogDataRequestError(string requestType, string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceTransactionScopeCreated, LogLevel.Debug,
		"Created SQL Server transaction scope {TransactionId} with isolation level {IsolationLevel}")]
	private partial void LogTransactionScopeCreated(string transactionId, IsolationLevel isolationLevel);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchExecuted, LogLevel.Debug,
		"Executed batch of {RequestCount} DataRequests in {ElapsedMs}ms")]
	private partial void LogBatchExecuted(int requestCount, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchError, LogLevel.Error,
		"Error executing batch of DataRequests")]
	private partial void LogBatchError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchInTransactionExecuted, LogLevel.Debug,
		"Executed batch of {RequestCount} DataRequests in transaction {TransactionId} in {ElapsedMs}ms")]
	private partial void LogBatchInTransactionExecuted(int requestCount, string transactionId, long elapsedMs);

	[LoggerMessage(DataSqlServerEventId.PersistenceBatchInTransactionError, LogLevel.Error,
		"Error executing batch of DataRequests in transaction {TransactionId}")]
	private partial void LogBatchInTransactionError(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionPoolStatsError, LogLevel.Warning,
		"Failed to retrieve connection pool statistics")]
	private partial void LogConnectionPoolStatsError(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceUnsafeSqlPattern, LogLevel.Warning,
		"DataRequest contains potentially unsafe SQL pattern: {Pattern}")]
	private partial void LogUnsafeSqlPattern(string pattern);

	[LoggerMessage(DataSqlServerEventId.PersistenceValidationError, LogLevel.Error,
		"Error validating DataRequest {RequestType}")]
	private partial void LogValidationError(string requestType, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionTestFailed, LogLevel.Warning,
		"Connection test failed")]
	private partial void LogConnectionTestFailed(Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceProviderAlreadyInitialized, LogLevel.Warning,
		"Provider is already initialized")]
	private partial void LogProviderAlreadyInitialized();

	[LoggerMessage(DataSqlServerEventId.PersistenceProviderInitializedSuccess, LogLevel.Information,
		"SQL Server persistence provider initialized successfully")]
	private partial void LogProviderInitializedSuccess();

	[LoggerMessage(DataSqlServerEventId.PersistenceTransientRetry, LogLevel.Warning,
		"Transient error occurred. Retry {RetryCount}/{MaxRetries} after {Delay}ms")]
	private partial void LogTransientRetry(int retryCount, int maxRetries, double delay);
}
