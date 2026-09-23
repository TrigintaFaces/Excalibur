// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Data;
using System.Diagnostics.CodeAnalysis;

using Excalibur.Data.MySql.Diagnostics;
using Excalibur.Data.Persistence;
using Excalibur.Data.Resilience;

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
public sealed partial class MySqlPersistenceProvider : ISqlPersistenceProvider, IPersistenceProviderHealth, IPersistenceProviderTransaction, IPersistenceProviderConnection, IDataRequestExecutor
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
			MaximumPoolSize = (uint)_options.Pooling.MaxPoolSize,
			MinimumPoolSize = (uint)_options.Pooling.MinPoolSize,
			Pooling = _options.Pooling.EnablePooling,
			ApplicationName = _options.ApplicationName ?? "Excalibur.Data",

			// REQUIRED for this framework's data-access shape, not a relaxation chosen for convenience.
			//
			// MySqlConnector validates, by design, that MySqlCommand.Transaction equals the connection's
			// active transaction, and rejects a command whose Transaction is null while one is pending. Its
			// own documentation names the case we are in: a library that "creates the MySqlCommand objects
			// itself". Every IDataRequest here does exactly that -- ResolveAsync is a Func over the
			// CONNECTION, with no transaction parameter -- so there is no seam through which a provider
			// could set the property the check demands. The check can therefore only ever fire as a false
			// alarm in this codebase: it guards against passing the WRONG transaction, and our contract
			// makes passing ANY transaction impossible.
			//
			// Without this, every request that runs SQL inside a transaction-scope batch fails. It is also
			// what makes MySQL behave like the sibling providers rather than differently from them --
			// Npgsql and SqlClient associate a command with the connection's pending transaction on their
			// own, which is why the identical code shape passes on Postgres and SQL Server.
			//
			// The narrower alternative -- giving IDataRequest a transaction seam -- is the real fix and is
			// a contract change across every provider. Until that is ruled, this is the honest local answer
			// and the one the vendor documents for it.
			IgnoreCommandTransaction = true,
		};

		if (_options.UseSsl)
		{
			builder.SslMode = MySqlSslMode.Required;
		}

		ConnectionString = builder.ConnectionString;
		Name = string.IsNullOrWhiteSpace(_options.Name) ? "mysql" : _options.Name;

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
	public string ProviderType => "SQL";

	/// <inheritdoc/>
	public string DatabaseType => "MySQL";

	/// <inheritdoc/>
	/// <remarks>
	/// <para>
	/// The whole batch commits or none of it does: the requests run inside one transaction, which is rolled
	/// back on the first failure. A caller receiving results therefore knows every request in the batch was
	/// applied, and a caller receiving an exception knows none of them was.
	/// </para>
	/// <para>
	/// <b>The transaction is AMBIENT, not an explicit one begun on the connection, and that is load-bearing
	/// rather than stylistic.</b> Each <c>IDataRequest</c> builds its own command from the connection alone
	/// -- <c>ResolveAsync</c> is a <c>Func</c> over the connection and there is no seam through which an
	/// explicit transaction could be handed to it. MySqlConnector refuses a command whose transaction is not
	/// the connection's active one, so a transaction begun here and invisible to the request made EVERY
	/// request that runs SQL fail. Opening the connection inside an ambient scope instead makes it enlist
	/// automatically, and commands on an enlisted connection need no explicit transaction assignment.
	/// </para>
	/// <para>
	/// This is the shape the SQL Server provider already uses for the same reason; it is adopted here rather
	/// than reinvented.
	/// </para>
	/// </remarks>
	public async Task<IEnumerable<object>> ExecuteBatchAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var requestList = requests.ToList();
		if (requestList.Count == 0)
		{
			return [];
		}

		using var ambientTransaction = new System.Transactions.TransactionScope(
			System.Transactions.TransactionScopeOption.Required,
			new System.Transactions.TransactionOptions
			{
				IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted,
				Timeout = TimeSpan.FromSeconds(_options.CommandTimeout),
			},
			System.Transactions.TransactionScopeAsyncFlowOption.Enabled);

		// Opened INSIDE the scope so the connection enlists in it. Moving this line above the scope silently
		// restores the defect: the connection would not be enlisted and every request would run unguarded.
		//
		// Synchronous using, matching the SQL Server provider: the factory's declared return type is
		// IDbConnection, which carries no async disposal, and casting to the concrete type purely to get
		// DisposeAsync would buy nothing here - the scope, not the connection, owns the transaction lifetime.
		using var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);

		var results = new List<object>(requestList.Count);

		foreach (var request in requestList)
		{
			results.Add(await request.ResolveAsync(connection).ConfigureAwait(false));
		}

		// Completing is what commits. An exception leaves the scope uncompleted, and disposing an
		// uncompleted scope rolls the ambient transaction back -- so the rollback needs no catch block of
		// its own, and there is no path that returns results without having completed.
		ambientTransaction.Complete();

		return results;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Unlike <see cref="ExecuteBatchAsync"/> this opens no transaction of its own. The caller's scope owns
	/// the boundary, so the provider and the connection are enlisted into it and the commit decision stays
	/// with the caller -- a nested transaction here would either be ignored or would commit a subset of the
	/// caller's unit of work.
	/// </remarks>
	public async Task<IEnumerable<object>> ExecuteBatchInTransactionAsync(
		IEnumerable<IDataRequest<IDbConnection, object>> requests,
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(requests);
		ArgumentNullException.ThrowIfNull(transactionScope);
		ObjectDisposedException.ThrowIf(_disposed, this);

		var requestList = requests.ToList();

		await transactionScope.EnlistProviderAsync(this, cancellationToken).ConfigureAwait(false);

		// Connection lifecycle passes to the transaction scope at enlistment: the scope BEGAN its
		// transaction on this connection, so disposing it here completes that transaction and discards every
		// write the caller has not committed yet. The scope owns disposal, not this method. Matches
		// SqlServerPersistenceProvider, which has always done it this way.
#pragma warning disable CA2000
		var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		await transactionScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
#pragma warning restore CA2000

		var results = new List<object>(requestList.Count);

		foreach (var request in requestList)
		{
			results.Add(await request.ResolveAsync(connection).ConfigureAwait(false));
		}

		return results;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// Rejects only what MySQL provably cannot execute: an empty command, or a request with no resolver to
	/// run. Bracket-quoted identifiers are rejected as well -- they are SQL Server syntax that MySQL reads
	/// as an array subscript under its default SQL mode, so a request carrying them fails at the server with
	/// a syntax error that names a position rather than a cause.
	/// </remarks>
	public bool ValidateRequest<TResult>(IDataRequest<IDbConnection, TResult> request)
	{
		if (request is null)
		{
			return false;
		}

		try
		{
			var commandText = request.Command.CommandText;

			if (string.IsNullOrWhiteSpace(commandText))
			{
				return false;
			}

			if (commandText.Contains('[', StringComparison.Ordinal) && commandText.Contains(']', StringComparison.Ordinal))
			{
				return false;
			}

			return request.ResolveAsync is not null;
		}
		catch (InvalidOperationException)
		{
			// Building the command is the request's own work and it may refuse: an unbuildable request is
			// an invalid one, which is the answer this method exists to give.
			return false;
		}
	}


	/// <inheritdoc/>
	public bool IsAvailable => _initialized && !_disposed;

	/// <inheritdoc/>
	public string ConnectionString { get; }

	/// <inheritdoc/>
	public IDataRequestRetryPolicy RetryPolicy => _dataRequestRetryPolicy;

	/// <inheritdoc/>
	public async Task<TResult> ExecuteAsync<TResult>(
		IDataRequest<IDbConnection, TResult> request,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(request);
		ObjectDisposedException.ThrowIf(_disposed, this);

		LogExecutingDataRequest(request.GetType().Name);

		return await _dataRequestRetryPolicy.ResolveAsync(
			request,
			async () => await CreateConnectionAsync(cancellationToken).ConfigureAwait(false),
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

			// A verified round-trip IS the readiness this provider reports: latch it here so a provider used
			// the way its own registration builds it -- without an explicit InitializeAsync, which that
			// registration never performs -- does not report itself unavailable while demonstrably working.
			_initialized = true;

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
			["MaxPoolSize"] = _options.Pooling.MaxPoolSize,
			["MinPoolSize"] = _options.Pooling.MinPoolSize,
			["EnablePooling"] = _options.Pooling.EnablePooling,
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
	public object? GetService(Type serviceType)
	{
		ArgumentNullException.ThrowIfNull(serviceType);

		if (serviceType == typeof(IPersistenceProviderHealth))
		{
			return this;
		}

		if (serviceType == typeof(IDataRequestExecutor))
		{
			return this;
		}

		if (serviceType == typeof(IPersistenceProviderConnection))
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
			if (_options.Pooling.ClearPoolOnDispose)
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
			if (_options.Pooling.ClearPoolOnDispose)
			{
				await MySqlConnection.ClearAllPoolsAsync().ConfigureAwait(false);
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
