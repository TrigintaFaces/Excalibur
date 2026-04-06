// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Transactions;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Validation;
using Excalibur.Data.SqlServer.Diagnostics;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using IsolationLevel = System.Data.IsolationLevel;
using TransactionStatus = Excalibur.Data.Abstractions.Persistence.TransactionStatus;

namespace Excalibur.Data.SqlServer.Persistence;

/// <summary>
/// SQL Server implementation of transaction scope with support for savepoints and distributed transactions.
/// </summary>
public partial class SqlServerTransactionScope : ITransactionScope, ITransactionScopeCallbacks, ITransactionScopeAdvanced
{
	private readonly ILogger<SqlServerTransactionScope> _logger;
	private readonly List<IPersistenceProvider> _enlistedProviders;
	private readonly List<IDbConnection> _enlistedConnections;
	private readonly List<SqlTransaction> _transactions;
	private readonly Dictionary<string, List<string>> _savepoints;
	private readonly List<Func<Task>> _onCommitCallbacks;
	private readonly List<Func<Task>> _onRollbackCallbacks;
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks;
#if NET9_0_OR_GREATER
	private readonly Lock _lock = new();
#else
	private readonly object _lock = new();
#endif
	private TransactionScope? _ambientTransaction;
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="SqlServerTransactionScope" /> class.
	/// </summary>
	/// <param name="isolationLevel"> The transaction isolation level. </param>
	/// <param name="timeout"> The transaction timeout. </param>
	/// <param name="logger"> The logger instance. </param>
	public SqlServerTransactionScope(
		IsolationLevel isolationLevel,
		TimeSpan timeout,
		ILogger<SqlServerTransactionScope> logger)
	{
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		TransactionId = Guid.NewGuid().ToString("N");
		IsolationLevel = isolationLevel;
		Timeout = timeout;
		StartTime = DateTimeOffset.UtcNow;
		Status = TransactionStatus.Active;

		_enlistedProviders = [];
		_enlistedConnections = [];
		_transactions = [];
		_savepoints = new Dictionary<string, List<string>>(StringComparer.Ordinal);
		_onCommitCallbacks = [];
		_onRollbackCallbacks = [];
		_onCompleteCallbacks = [];

		// Create ambient transaction for distributed transaction support
		if (Transaction.Current == null)
		{
			var transactionOptions = new TransactionOptions { IsolationLevel = ConvertIsolationLevel(isolationLevel), Timeout = timeout };
			_ambientTransaction = new TransactionScope(
				TransactionScopeOption.Required,
				transactionOptions,
				TransactionScopeAsyncFlowOption.Enabled);
		}

		LogTransactionCreated(TransactionId, isolationLevel);
	}

	/// <inheritdoc />
	public string TransactionId { get; }

	/// <inheritdoc />
	public IsolationLevel IsolationLevel { get; }

	/// <inheritdoc />
	public TransactionStatus Status { get; private set; }

	/// <inheritdoc />
	public DateTimeOffset StartTime { get; }

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; }

	/// <inheritdoc />
	public async Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);
		ThrowIfDisposed();
		ThrowIfNotActive();

		lock (_lock)
		{
			if (_enlistedProviders.Contains(provider))
			{
				LogProviderAlreadyEnlisted(provider.Name, TransactionId);
				return;
			}

			_enlistedProviders.Add(provider);
		}

		LogProviderEnlisted(provider.Name, TransactionId);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(connection);
		ThrowIfDisposed();
		ThrowIfNotActive();

		if (_enlistedConnections.Contains(connection))
		{
			LogConnectionAlreadyEnlisted(TransactionId);
			return;
		}

		if (connection is SqlConnection sqlConnection)
		{
			if (connection.State != ConnectionState.Open)
			{
				await sqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}

			var transaction = (SqlTransaction)await sqlConnection.BeginTransactionAsync(IsolationLevel, cancellationToken).ConfigureAwait(false);
			_transactions.Add(transaction);
			_enlistedConnections.Add(connection);

			// Initialize savepoints for this connection
			var connectionId = GetConnectionId(connection);
			_savepoints[connectionId] = [];

			LogConnectionEnlisted(TransactionId);
		}
		else
		{
			throw new InvalidOperationException(
				$"Connection type {connection.GetType().Name} is not supported for SQL Server transactions");
		}
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		Status = TransactionStatus.Committing;
		LogCommittingTransaction(TransactionId);

		try
		{
			// Commit all SQL transactions
			foreach (var transaction in _transactions)
			{
				transaction.Commit();
			}

			// Complete the ambient transaction scope
			_ambientTransaction?.Complete();

			Status = TransactionStatus.Committed;
			LogTransactionCommitted(TransactionId);

			// Execute commit callbacks — collect errors and propagate
			List<Exception>? callbackErrors = null;
			foreach (var callback in _onCommitCallbacks)
			{
				try
				{
					await callback().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogCommitCallbackError(TransactionId, ex);
					callbackErrors ??= [];
					callbackErrors.Add(ex);
				}
			}

			// Execute complete callbacks
			await ExecuteCompleteCallbacksAsync(callbackErrors).ConfigureAwait(false);

			if (callbackErrors is { Count: > 0 })
			{
				throw new AggregateException(
					$"One or more transaction callbacks failed for transaction {TransactionId}.",
					callbackErrors);
			}
		}
		catch (Exception ex)
		{
			Status = TransactionStatus.Failed;
			LogCommitFailed(TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (Status is not TransactionStatus.Active and not TransactionStatus.Committing)
		{
			LogCannotRollback(TransactionId, Status);
			return;
		}

		Status = TransactionStatus.RollingBack;
		LogRollingBackTransaction(TransactionId);

		try
		{
			// Rollback all SQL transactions
			foreach (var transaction in _transactions)
			{
				try
				{
					transaction.Rollback();
				}
				catch (Exception ex)
				{
					LogRollbackTransactionError(TransactionId, ex);
				}
			}

			// Dispose the ambient transaction without completing it (implicit rollback)
			_ambientTransaction?.Dispose();
			_ambientTransaction = null;

			Status = TransactionStatus.RolledBack;
			LogTransactionRolledBack(TransactionId);

			// Execute rollback callbacks — collect errors and propagate
			List<Exception>? callbackErrors = null;
			foreach (var callback in _onRollbackCallbacks)
			{
				try
				{
					await callback().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogRollbackCallbackError(TransactionId, ex);
					callbackErrors ??= [];
					callbackErrors.Add(ex);
				}
			}

			// Execute complete callbacks
			await ExecuteCompleteCallbacksAsync(callbackErrors).ConfigureAwait(false);

			if (callbackErrors is { Count: > 0 })
			{
				throw new AggregateException(
					$"One or more transaction callbacks failed for transaction {TransactionId}.",
					callbackErrors);
			}
		}
		catch (Exception ex)
		{
			Status = TransactionStatus.Failed;
			LogRollbackFailed(TransactionId, ex);
			throw;
		}
	}

	/// <inheritdoc />
	public async Task CreateSavepointAsync(string savepointName, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		if (string.IsNullOrWhiteSpace(savepointName))
		{
			throw new ArgumentException("Savepoint name cannot be null or empty", nameof(savepointName));
		}

		SqlIdentifierValidator.ThrowIfInvalid(savepointName, nameof(savepointName));

		// SQL Server savepoints must be created on each connection
		for (var i = 0; i < _transactions.Count; i++)
		{
			var transaction = _transactions[i];
			var connection = _enlistedConnections[i];
			var connectionId = GetConnectionId(connection);

			// Create savepoint using SQL command
			using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"SAVE TRANSACTION {savepointName}";
			if (command is SqlCommand sqlCommand)
			{
				_ = await sqlCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				_ = command.ExecuteNonQuery();
			}

			// Track savepoint for this connection
			if (!_savepoints[connectionId].Contains(savepointName, StringComparer.Ordinal))
			{
				_savepoints[connectionId].Add(savepointName);
			}

			LogSavepointCreated(savepointName, connectionId, TransactionId);
		}
	}

	/// <inheritdoc />
	public async Task RollbackToSavepointAsync(string savepointName, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		if (string.IsNullOrWhiteSpace(savepointName))
		{
			throw new ArgumentException("Savepoint name cannot be null or empty", nameof(savepointName));
		}

		SqlIdentifierValidator.ThrowIfInvalid(savepointName, nameof(savepointName));

		// Rollback to savepoint on each connection
		for (var i = 0; i < _transactions.Count; i++)
		{
			var transaction = _transactions[i];
			var connection = _enlistedConnections[i];
			var connectionId = GetConnectionId(connection);

			if (!_savepoints.TryGetValue(connectionId, out var connectionSavepoints) ||
				!connectionSavepoints.Contains(savepointName, StringComparer.Ordinal))
			{
				LogSavepointNotFound(savepointName, connectionId);
				continue;
			}

			// Rollback to savepoint using SQL command
			using var command = connection.CreateCommand();
			command.Transaction = transaction;
			command.CommandText = $"ROLLBACK TRANSACTION {savepointName}";
			if (command is SqlCommand sqlCommand)
			{
				_ = await sqlCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
			}
			else
			{
				_ = command.ExecuteNonQuery();
			}

			// Remove savepoints created after this one
			var savepointIndex = connectionSavepoints.IndexOf(savepointName);
			if (savepointIndex >= 0)
			{
				connectionSavepoints.RemoveRange(savepointIndex + 1, connectionSavepoints.Count - savepointIndex - 1);
			}

			LogRolledBackToSavepoint(savepointName, connectionId, TransactionId);
		}
	}

	/// <inheritdoc />
	public async Task ReleaseSavepointAsync(string savepointName, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		if (string.IsNullOrWhiteSpace(savepointName))
		{
			throw new ArgumentException("Savepoint name cannot be null or empty", nameof(savepointName));
		}

		SqlIdentifierValidator.ThrowIfInvalid(savepointName, nameof(savepointName));

		// SQL Server doesn't support explicit savepoint release Just remove from tracking
		foreach (var connectionId in _savepoints.Keys)
		{
			_ = _savepoints[connectionId].Remove(savepointName);
		}

		LogSavepointReleased(savepointName, TransactionId);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public void OnCommit(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_lock)
		{
			_onCommitCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnRollback(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_lock)
		{
			_onRollbackCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnComplete(Func<TransactionStatus, Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_lock)
		{
			_onCompleteCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public IEnumerable<IPersistenceProvider> GetEnlistedProviders()
	{
		lock (_lock)
		{
			return _enlistedProviders.ToList();
		}
	}

	/// <inheritdoc />
	public ITransactionScope CreateNestedScope(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		// Create a nested transaction scope
		var nestedScope = new SqlServerTransactionScope(isolationLevel, Timeout, _logger);

		LogNestedScopeCreated(nestedScope.TransactionId, TransactionId);

		return nestedScope;
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
	/// Performs the async dispose.
	/// </summary>
	protected virtual async ValueTask DisposeCoreAsync()
	{
		if (!_disposed)
		{
			if (Status == TransactionStatus.Active)
			{
				try
				{
					using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
					await RollbackAsync(timeoutCts.Token).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					LogAutomaticRollbackError(TransactionId, ex);
				}
			}

			// Dispose transactions
			foreach (var transaction in _transactions)
			{
				if (transaction != null)
				{
					await transaction.DisposeAsync().ConfigureAwait(false);
				}
			}

			// Close connections
			foreach (var connection in _enlistedConnections)
			{
				if (connection?.State == ConnectionState.Open)
				{
					connection.Close();
				}

				connection?.Dispose();
			}

			// Dispose ambient transaction
			_ambientTransaction?.Dispose();

			Status = TransactionStatus.Disposed;
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
				if (Status == TransactionStatus.Active)
				{
					try
					{
						// Use synchronous Rollback to avoid thread pool starvation during shutdown (AD-540.2)
						foreach (var transaction in _transactions)
						{
							try
							{
								transaction.Rollback();
							}
							catch (Exception ex)
							{
								LogRollbackTransactionError(TransactionId, ex);
							}
						}

						Status = TransactionStatus.RolledBack;
					}
					catch (Exception ex)
					{
						LogAutomaticRollbackError(TransactionId, ex);
					}
				}

				// Dispose transactions
				foreach (var transaction in _transactions)
				{
					transaction?.Dispose();
				}

				// Close connections
				foreach (var connection in _enlistedConnections)
				{
					if (connection?.State == ConnectionState.Open)
					{
						connection.Close();
					}

					connection?.Dispose();
				}

				// Dispose ambient transaction
				_ambientTransaction?.Dispose();
			}

			Status = TransactionStatus.Disposed;
			_disposed = true;
		}
	}

	private static string GetConnectionId(IDbConnection connection) => $"{connection.GetHashCode():X8}";

	private static System.Transactions.IsolationLevel ConvertIsolationLevel(IsolationLevel isolationLevel) =>
		isolationLevel switch
		{
			IsolationLevel.Chaos => System.Transactions.IsolationLevel.Chaos,
			IsolationLevel.ReadUncommitted => System.Transactions.IsolationLevel.ReadUncommitted,
			IsolationLevel.ReadCommitted => System.Transactions.IsolationLevel.ReadCommitted,
			IsolationLevel.RepeatableRead => System.Transactions.IsolationLevel.RepeatableRead,
			IsolationLevel.Serializable => System.Transactions.IsolationLevel.Serializable,
			IsolationLevel.Snapshot => System.Transactions.IsolationLevel.Snapshot,
			IsolationLevel.Unspecified => System.Transactions.IsolationLevel.Unspecified,
			_ => throw new NotSupportedException(
				$"Isolation level '{isolationLevel}' is not supported by SQL Server transactions. " +
				$"Supported levels: Chaos, ReadUncommitted, ReadCommitted, RepeatableRead, Serializable, Snapshot, Unspecified."),
		};

	private void ThrowIfDisposed()
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(SqlServerTransactionScope));
		}
	}

	private void ThrowIfNotActive()
	{
		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Transaction {TransactionId} is not active (current status: {Status})");
		}

		// Check for timeout
		if (DateTimeOffset.UtcNow - StartTime > Timeout)
		{
			Status = TransactionStatus.TimedOut;
			throw new TimeoutException($"Transaction {TransactionId} has timed out");
		}
	}

	private async Task ExecuteCompleteCallbacksAsync(List<Exception>? errors)
	{
		foreach (var callback in _onCompleteCallbacks)
		{
			try
			{
				await callback(Status).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				LogCompleteCallbackError(TransactionId, ex);
				errors ??= [];
				errors.Add(ex);
			}
		}
	}

	// Source-generated logging methods
	[LoggerMessage(DataSqlServerEventId.PersistenceTransactionCreated, LogLevel.Debug,
		"Created SQL Server transaction scope {TransactionId} with isolation level {IsolationLevel}")]
	private partial void LogTransactionCreated(string transactionId, IsolationLevel isolationLevel);

	[LoggerMessage(DataSqlServerEventId.PersistenceProviderAlreadyEnlisted, LogLevel.Warning,
		"Provider {ProviderName} is already enlisted in transaction {TransactionId}")]
	private partial void LogProviderAlreadyEnlisted(string providerName, string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceProviderEnlisted, LogLevel.Debug,
		"Enlisted provider {ProviderName} in transaction {TransactionId}")]
	private partial void LogProviderEnlisted(string providerName, string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionAlreadyEnlisted, LogLevel.Warning,
		"Connection is already enlisted in transaction {TransactionId}")]
	private partial void LogConnectionAlreadyEnlisted(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceConnectionEnlisted, LogLevel.Debug,
		"Enlisted SQL connection in transaction {TransactionId}")]
	private partial void LogConnectionEnlisted(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceCommittingTransaction, LogLevel.Debug,
		"Committing transaction {TransactionId}")]
	private partial void LogCommittingTransaction(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceTransactionCommitted, LogLevel.Information,
		"Transaction {TransactionId} committed successfully")]
	private partial void LogTransactionCommitted(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceCommitCallbackError, LogLevel.Error,
		"Error executing commit callback for transaction {TransactionId}")]
	private partial void LogCommitCallbackError(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceCommitFailed, LogLevel.Error,
		"Failed to commit transaction {TransactionId}")]
	private partial void LogCommitFailed(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceCannotRollback, LogLevel.Warning,
		"Cannot rollback transaction {TransactionId} in status {Status}")]
	private partial void LogCannotRollback(string transactionId, TransactionStatus status);

	[LoggerMessage(DataSqlServerEventId.PersistenceRollingBackTransaction, LogLevel.Debug,
		"Rolling back transaction {TransactionId}")]
	private partial void LogRollingBackTransaction(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceRollbackTransactionError, LogLevel.Error,
		"Error rolling back SQL transaction in {TransactionId}")]
	private partial void LogRollbackTransactionError(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceTransactionRolledBack, LogLevel.Information,
		"Transaction {TransactionId} rolled back")]
	private partial void LogTransactionRolledBack(string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceRollbackCallbackError, LogLevel.Error,
		"Error executing rollback callback for transaction {TransactionId}")]
	private partial void LogRollbackCallbackError(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceRollbackFailed, LogLevel.Error,
		"Failed to rollback transaction {TransactionId}")]
	private partial void LogRollbackFailed(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceSavepointCreated, LogLevel.Debug,
		"Created savepoint {SavepointName} on connection {ConnectionId} in transaction {TransactionId}")]
	private partial void LogSavepointCreated(string savepointName, string connectionId, string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceSavepointNotFound, LogLevel.Warning,
		"Savepoint {SavepointName} not found on connection {ConnectionId}")]
	private partial void LogSavepointNotFound(string savepointName, string connectionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceRolledBackToSavepoint, LogLevel.Debug,
		"Rolled back to savepoint {SavepointName} on connection {ConnectionId} in transaction {TransactionId}")]
	private partial void LogRolledBackToSavepoint(string savepointName, string connectionId, string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceSavepointReleased, LogLevel.Debug,
		"Released savepoint {SavepointName} in transaction {TransactionId}")]
	private partial void LogSavepointReleased(string savepointName, string transactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceNestedScopeCreated, LogLevel.Debug,
		"Created nested transaction scope {NestedTransactionId} under {ParentTransactionId}")]
	private partial void LogNestedScopeCreated(string nestedTransactionId, string parentTransactionId);

	[LoggerMessage(DataSqlServerEventId.PersistenceAutomaticRollbackError, LogLevel.Error,
		"Error during automatic rollback of transaction {TransactionId}")]
	private partial void LogAutomaticRollbackError(string transactionId, Exception ex);

	[LoggerMessage(DataSqlServerEventId.PersistenceCompleteCallbackError, LogLevel.Error,
		"Error executing complete callback for transaction {TransactionId}")]
	private partial void LogCompleteCallbackError(string transactionId, Exception ex);
}
