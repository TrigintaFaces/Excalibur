// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.Abstractions;
using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Resilience;
using Excalibur.Data.MySql.Diagnostics;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using MySqlConnector;

using Polly;
using Polly.Retry;

namespace Excalibur.Data.MySql;

/// <summary>
/// MySQL/MariaDB implementation of the persistence provider.
/// </summary>
public sealed partial class MySqlPersistenceProvider : IPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction
{
	private readonly MySqlProviderOptions _options;
	private readonly ILogger<MySqlPersistenceProvider> _logger;
	private readonly AsyncRetryPolicy _retryPolicy;
	private readonly MySqlRetryPolicy _dataRequestRetryPolicy;
	private volatile bool _disposed;
	private bool _initialized;

	/// <summary>
	/// Initializes a new instance of the <see cref="MySqlPersistenceProvider"/> class.
	/// </summary>
	/// <param name="options">The MySQL provider options.</param>
	/// <param name="logger">The logger instance.</param>
	public MySqlPersistenceProvider(
		IOptions<MySqlProviderOptions> options,
		ILogger<MySqlPersistenceProvider> logger)
	{
		_options = options?.Value ?? throw new ArgumentNullException(nameof(options));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		if (string.IsNullOrWhiteSpace(_options.ConnectionString))
		{
			throw new ArgumentException("Connection string is required.", nameof(options));
		}

		var builder = new MySqlConnectionStringBuilder(_options.ConnectionString)
		{
			DefaultCommandTimeout = (uint)_options.CommandTimeout,
			ConnectionTimeout = (uint)_options.ConnectTimeout,
			MaximumPoolSize = (uint)_options.MaxPoolSize,
			MinimumPoolSize = (uint)_options.MinPoolSize,
			Pooling = _options.EnablePooling,
			ApplicationName = _options.ApplicationName ?? "Excalibur.Data",
		};

		if (_options.UseSsl)
		{
			builder.SslMode = MySqlSslMode.Required;
		}

		ConnectionString = builder.ConnectionString;
		Name = _options.Name ?? "mysql";
		ProviderType = "SQL";

		_dataRequestRetryPolicy = new MySqlRetryPolicy(_options, _logger);

		_retryPolicy = Policy
			.Handle<MySqlException>(IsTransientError)
			.WaitAndRetryAsync(
				_options.MaxRetryCount,
				retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
				onRetry: (exception, timeSpan, retryCount, context) =>
					LogRetryAttempt(retryCount, timeSpan.TotalMilliseconds, exception));
	}

	/// <inheritdoc/>
	public string Name { get; }

	/// <inheritdoc/>
	public string ProviderType { get; }

	/// <inheritdoc/>
	public bool IsAvailable => _initialized && !_disposed;

	/// <inheritdoc/>
	public string ConnectionString { get; }

	/// <inheritdoc/>
	public IDataRequestRetryPolicy RetryPolicy => _dataRequestRetryPolicy;

	/// <inheritdoc/>
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

	/// <inheritdoc/>
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
			await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

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

	/// <inheritdoc/>
	public ITransactionScope CreateTransactionScope(
		IsolationLevel isolationLevel = IsolationLevel.ReadCommitted,
		TimeSpan? timeout = null)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);
		var scope = new MySqlTransactionScope(
			isolationLevel,
			_logger as ILogger<MySqlTransactionScope> ??
			NullLogger<MySqlTransactionScope>.Instance);

		if (timeout.HasValue)
		{
			scope.Timeout = timeout.Value;
		}

		return scope;
	}

	/// <inheritdoc/>
	public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
	{
		try
		{
			await _retryPolicy.ExecuteAsync(
				async ct =>
				{
					await using var connection = new MySqlConnection(ConnectionString);
					await connection.OpenAsync(ct).ConfigureAwait(false);

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

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>> GetMetricsAsync(CancellationToken cancellationToken)
	{
		var metrics = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			["Provider"] = "MySQL",
			["Name"] = Name,
			["IsAvailable"] = IsAvailable,
			["MaxPoolSize"] = _options.MaxPoolSize,
			["MinPoolSize"] = _options.MinPoolSize,
			["EnablePooling"] = _options.EnablePooling,
			["UseSsl"] = _options.UseSsl,
			["CommandTimeout"] = _options.CommandTimeout,
			["ConnectTimeout"] = _options.ConnectTimeout,
		};

		try
		{
			await using var connection = new MySqlConnection(ConnectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			metrics["ServerVersion"] = connection.ServerVersion;
			metrics["Database"] = connection.Database;
			metrics["DataSource"] = connection.DataSource;

			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					VERSION() AS version,
					DATABASE() AS db,
					CURRENT_USER() AS current_user,
					@@hostname AS hostname,
					@@port AS port,
					@@read_only AS is_replica
			""";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				metrics["FullVersion"] = reader["version"]?.ToString() ?? "Unknown";
				metrics["CurrentDatabase"] = reader["db"]?.ToString() ?? "Unknown";
				metrics["CurrentUser"] = reader["current_user"]?.ToString() ?? "Unknown";
				metrics["Hostname"] = reader["hostname"]?.ToString() ?? "Unknown";
				metrics["Port"] = reader["port"]?.ToString() ?? "Unknown";
				metrics["IsReplica"] = reader["is_replica"];
			}
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveMetrics(ex);
		}

		return metrics;
	}

	/// <inheritdoc/>
	public async Task InitializeAsync(IPersistenceOptions options, CancellationToken cancellationToken)
	{
		LogInitializingProvider(Name);

		if (!await TestConnectionAsync(cancellationToken).ConfigureAwait(false))
		{
			throw new InvalidOperationException($"Failed to initialize MySQL provider '{Name}': Connection test failed");
		}

		_initialized = true;
	}

	/// <inheritdoc/>
	public async Task<IDictionary<string, object>?> GetConnectionPoolStatsAsync(CancellationToken cancellationToken)
	{
		try
		{
			var stats = new Dictionary<string, object>(StringComparer.Ordinal);

			await using var connection = new MySqlConnection(ConnectionString);
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			await using var command = connection.CreateCommand();
			command.CommandText = """
				SELECT
					(SELECT VARIABLE_VALUE FROM performance_schema.global_status WHERE VARIABLE_NAME = 'Threads_connected') AS threads_connected,
					(SELECT VARIABLE_VALUE FROM performance_schema.global_status WHERE VARIABLE_NAME = 'Threads_running') AS threads_running,
					(SELECT VARIABLE_VALUE FROM performance_schema.global_status WHERE VARIABLE_NAME = 'Max_used_connections') AS max_used_connections,
					@@max_connections AS max_connections
			""";

			await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
			if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
			{
				stats["ThreadsConnected"] = reader["threads_connected"]?.ToString() ?? "0";
				stats["ThreadsRunning"] = reader["threads_running"]?.ToString() ?? "0";
				stats["MaxUsedConnections"] = reader["max_used_connections"]?.ToString() ?? "0";
				stats["MaxConnections"] = reader["max_connections"];
			}

			stats["MinPoolSize"] = _options.MinPoolSize;
			stats["MaxPoolSize"] = _options.MaxPoolSize;
			stats["PoolingEnabled"] = _options.EnablePooling;

			return stats;
		}
		catch (Exception ex)
		{
			LogFailedToRetrieveConnectionPoolStatistics(ex);
			return null;
		}
	}

	/// <inheritdoc/>
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
	/// Creates a database connection asynchronously.
	/// </summary>
	/// <param name="cancellationToken">The cancellation token.</param>
	/// <returns>A new database connection.</returns>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Factory method transfers ownership to caller. Connection is disposed on exception path.")]
	public async ValueTask<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		ObjectDisposedException.ThrowIf(_disposed, this);

		var connection = new MySqlConnection(ConnectionString);
		try
		{
			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			connection.Dispose();
			throw;
		}

		return connection;
	}

	/// <inheritdoc/>
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
			if (_options.ClearPoolOnDispose)
			{
				MySqlConnection.ClearAllPools();
				LogClearedConnectionPools();
			}
		}
		catch (Exception ex)
		{
			LogErrorDisposingProvider(ex);
		}
	}

	/// <inheritdoc/>
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
			if (_options.ClearPoolOnDispose)
			{
				await Task.Run(static () => MySqlConnection.ClearAllPools()).ConfigureAwait(false);
				LogClearedConnectionPools();
			}
		}
		catch (Exception ex)
		{
			LogErrorDisposingProvider(ex);
		}
	}

	private static bool IsTransientError(MySqlException exception)
	{
		int[] transientErrorCodes =
		[
			1040, // Too many connections
			1205, // Lock wait timeout
			1213, // Deadlock
			2002, // Can't connect (socket)
			2003, // Can't connect (TCP)
			2006, // Server has gone away
			2013, // Lost connection
		];

		return transientErrorCodes.Contains(exception.Number);
	}

	[LoggerMessage(DataMySqlEventId.PersistenceRetryAttempt, LogLevel.Warning,
		"MySQL operation failed with transient error. Retry {RetryCount} after {TimeSpan}ms")]
	private partial void LogRetryAttempt(int retryCount, double timeSpan, Exception ex);

	[LoggerMessage(DataMySqlEventId.ExecutingDataRequest, LogLevel.Debug,
		"Executing data request of type {RequestType}")]
	private partial void LogExecutingDataRequest(string requestType);

	[LoggerMessage(DataMySqlEventId.ExecutingDataRequestInTransaction, LogLevel.Debug,
		"Executing data request of type {RequestType} in transaction {TransactionId}")]
	private partial void LogExecutingDataRequestInTransaction(string requestType, string transactionId);

	[LoggerMessage(DataMySqlEventId.FailedToExecuteDataRequest, LogLevel.Error,
		"Failed to execute data request in transaction {TransactionId}")]
	private partial void LogFailedToExecuteDataRequest(string transactionId, Exception ex);

	[LoggerMessage(DataMySqlEventId.ConnectionTestSuccessful, LogLevel.Information,
		"MySQL connection test successful for '{Name}'")]
	private partial void LogConnectionTestSuccessful(string name);

	[LoggerMessage(DataMySqlEventId.ConnectionTestFailed, LogLevel.Error,
		"MySQL connection test failed for '{Name}'")]
	private partial void LogConnectionTestFailed(string name, Exception ex);

	[LoggerMessage(DataMySqlEventId.FailedToRetrieveMetrics, LogLevel.Warning,
		"Failed to retrieve complete MySQL metrics")]
	private partial void LogFailedToRetrieveMetrics(Exception ex);

	[LoggerMessage(DataMySqlEventId.InitializingProvider, LogLevel.Information,
		"Initializing MySQL persistence provider '{Name}'")]
	private partial void LogInitializingProvider(string name);

	[LoggerMessage(DataMySqlEventId.FailedToRetrieveConnectionPoolStatistics, LogLevel.Warning,
		"Failed to retrieve MySQL connection pool statistics")]
	private partial void LogFailedToRetrieveConnectionPoolStatistics(Exception ex);

	[LoggerMessage(DataMySqlEventId.DisposingProvider, LogLevel.Debug,
		"Disposing MySQL provider '{Name}'")]
	private partial void LogDisposingProvider(string name);

	[LoggerMessage(DataMySqlEventId.ClearedConnectionPools, LogLevel.Debug,
		"Cleared MySQL connection pools")]
	private partial void LogClearedConnectionPools();

	[LoggerMessage(DataMySqlEventId.ErrorDisposingProvider, LogLevel.Warning,
		"Error disposing MySQL provider")]
	private partial void LogErrorDisposingProvider(Exception ex);
}
