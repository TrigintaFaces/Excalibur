// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using SqlClientApplicationIntent = Microsoft.Data.SqlClient.ApplicationIntent;
using SqlClientColumnEncryptionSetting = Microsoft.Data.SqlClient.SqlConnectionColumnEncryptionSetting;

namespace Excalibur.Data.SqlServer.Persistence;

internal sealed partial class SqlServerConnectionManager : IDisposable, IAsyncDisposable
{
	private readonly SqlServerPersistenceOptions _options;
	private readonly SqlServerPersistenceMetrics _metrics;
	private readonly ILogger _logger;
	private readonly ILoggerFactory _loggerFactory;
	private readonly SemaphoreSlim _connectionSemaphore;

	internal SqlServerConnectionManager(
		SqlServerPersistenceOptions options,
		SqlServerPersistenceMetrics metrics,
		ILogger logger,
		ILoggerFactory loggerFactory)
	{
		_options = options ?? throw new ArgumentNullException(nameof(options));
		_metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));
		_loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
		_connectionSemaphore = new SemaphoreSlim(_options.MaxPoolSize, _options.MaxPoolSize);
	}

	public void Dispose() => _connectionSemaphore.Dispose();

	public ValueTask DisposeAsync()
	{
		_connectionSemaphore.Dispose();
		return ValueTask.CompletedTask;
	}

	internal IDbConnection CreateConnection()
	{
		var builder = BuildConnectionString();
		var connection = new SqlConnection(builder.ConnectionString);

		if (_options.EnableDetailedLogging)
		{
			LogConnectionCreated(_logger);
		}

		_metrics.RecordConnectionCreated();
		return connection;
	}

	internal async Task<IDbConnection> CreateConnectionAsync(CancellationToken cancellationToken)
	{
		await _connectionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			var builder = BuildConnectionString();
			var connection = new SqlConnection(builder.ConnectionString);

			await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

			if (_options.EnableDetailedLogging)
			{
				LogConnectionOpened(_logger);
			}

			_metrics.RecordConnectionCreated();
			return connection;
		}
		finally
		{
			_ = _connectionSemaphore.Release();
		}
	}

	internal SqlServerTransactionScope CreateTransactionScope(IsolationLevel isolationLevel, TimeSpan? timeout)
	{
		var actualTimeout = timeout ?? TimeSpan.FromMinutes(_options.CommandTimeout);
		var scope = new SqlServerTransactionScope(
			isolationLevel,
			actualTimeout,
			_loggerFactory.CreateLogger<SqlServerTransactionScope>());

		if (_options.EnableDetailedLogging)
		{
			LogTransactionScopeCreated(_logger, scope.TransactionId, isolationLevel);
		}

		return scope;
	}

	internal async Task<IDbConnection> GetOrCreateEnlistedConnectionAsync(
		ITransactionScope transactionScope,
		CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(transactionScope);

		if (transactionScope is not SqlServerTransactionScope sqlScope)
		{
			throw new InvalidOperationException("Transaction scope must be a SqlServerTransactionScope for SQL Server operations.");
		}

		var connection = await CreateConnectionAsync(cancellationToken).ConfigureAwait(false);
		await sqlScope.EnlistConnectionAsync(connection, cancellationToken).ConfigureAwait(false);
		return connection;
	}

	internal SqlConnectionStringBuilder BuildConnectionString()
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
				? SqlClientColumnEncryptionSetting.Enabled
				: SqlClientColumnEncryptionSetting.Disabled;
		}

		builder.ApplicationIntent = _options.Connection.ApplicationIntent == ApplicationIntent.ReadOnly
			? SqlClientApplicationIntent.ReadOnly
			: SqlClientApplicationIntent.ReadWrite;

		if (!string.IsNullOrEmpty(_options.Connection.WorkstationId))
		{
			builder.WorkstationID = _options.Connection.WorkstationId;
		}

		return builder;
	}

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionCreated, LogLevel.Debug, "Created new SQL Server connection")]
	private static partial void LogConnectionCreated(ILogger logger);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionOpened, LogLevel.Debug, "Created and opened new SQL Server connection")]
	private static partial void LogConnectionOpened(ILogger logger);

	[LoggerMessage(DataSqlServerEventId.PersistenceTransactionScopeCreated, LogLevel.Debug, "Created SQL Server transaction scope {TransactionId} with isolation level {IsolationLevel}")]
	private static partial void LogTransactionScopeCreated(ILogger logger, string transactionId, IsolationLevel isolationLevel);
}
