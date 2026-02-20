// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Data;

using Excalibur.Data.Abstractions.Persistence;
using Excalibur.Data.Abstractions.Validation;

using Microsoft.Extensions.Logging;

using Npgsql;

namespace Excalibur.Data.Postgres.Persistence;

/// <summary>
/// Postgres implementation of transaction scope with support for savepoints and distributed transactions.
/// </summary>
public class PostgresTransactionScope : ITransactionScope, ITransactionScopeCallbacks, ITransactionScopeAdvanced
{
	private readonly ILogger<PostgresTransactionScope> _logger;
	private readonly List<IPersistenceProvider> _enlistedProviders = [];
	private readonly List<IDbConnection> _enlistedConnections = [];
	private readonly List<Func<Task>> _onCommitCallbacks = [];
	private readonly List<Func<Task>> _onRollbackCallbacks = [];
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks = [];
	private readonly ConcurrentDictionary<string, NpgsqlTransaction> _transactions = new();
	private readonly HashSet<string> _savepoints = [];
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private volatile bool _disposed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PostgresTransactionScope" /> class.
	/// </summary>
	/// <param name="isolationLevel"> The transaction isolation level. </param>
	/// <param name="logger"> The logger for diagnostic output. </param>
	public PostgresTransactionScope(
		IsolationLevel isolationLevel,
		ILogger<PostgresTransactionScope> logger)
	{
		TransactionId = Guid.NewGuid().ToString("N");
		IsolationLevel = isolationLevel;
		Status = TransactionStatus.Active;
		StartTime = DateTimeOffset.UtcNow;
		Timeout = TimeSpan.FromSeconds(30);
		_logger = logger ?? throw new ArgumentNullException(nameof(logger));

		_logger.LogDebug(
			"Created Postgres transaction scope {TransactionId} with isolation level {IsolationLevel}",
			TransactionId, isolationLevel);
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
		ThrowIfDisposed();
		ThrowIfNotActive();

		lock (_lock)
		{
			if (_enlistedProviders.Contains(provider))
			{
				_logger.LogDebug(
					"Provider {ProviderName} already enlisted in transaction {TransactionId}",
					provider.Name, TransactionId);
				return;
			}

			_enlistedProviders.Add(provider);
		}

		_logger.LogDebug(
			"Enlisted provider {ProviderName} in transaction {TransactionId}",
			provider.Name, TransactionId);

		await Task.CompletedTask.ConfigureAwait(false);
	}

	/// <inheritdoc />
	public async Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		if (connection is not NpgsqlConnection npgsqlConnection)
		{
			throw new ArgumentException("Connection must be a NpgsqlConnection", nameof(connection));
		}

		lock (_lock)
		{
			if (_enlistedConnections.Contains(connection))
			{
				_logger.LogDebug("Connection already enlisted in transaction {TransactionId}", TransactionId);
				return;
			}

			_enlistedConnections.Add(connection);
		}

		// Ensure connection is open and begin transaction — cleanup on failure to prevent leak
		try
		{
			if (npgsqlConnection.State != ConnectionState.Open)
			{
				await npgsqlConnection.OpenAsync(cancellationToken).ConfigureAwait(false);
			}

			var transaction = await npgsqlConnection.BeginTransactionAsync(IsolationLevel, cancellationToken).ConfigureAwait(false);
			_transactions[npgsqlConnection.ConnectionString] = transaction;
		}
		catch
		{
			// Remove from enlisted connections to prevent leak — the caller owns the connection
			lock (_lock)
			{
				_enlistedConnections.Remove(connection);
			}

			throw;
		}

		_logger.LogDebug(
			"Enlisted connection in transaction {TransactionId} with isolation level {IsolationLevel}",
			TransactionId, IsolationLevel);
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();
		ThrowIfNotActive();

		try
		{
			Status = TransactionStatus.Committing;
			_logger.LogDebug("Committing transaction {TransactionId}", TransactionId);

			// Check for timeout
			if (DateTimeOffset.UtcNow - StartTime > Timeout)
			{
				Status = TransactionStatus.TimedOut;
				throw new TimeoutException($"Transaction {TransactionId} exceeded timeout of {Timeout}");
			}

			// Commit all enlisted transactions
			foreach (var transaction in _transactions.Values)
			{
				await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
			}

			Status = TransactionStatus.Committed;
			_logger.LogInformation("Transaction {TransactionId} committed successfully", TransactionId);

			// Execute commit callbacks — collect errors and propagate
			List<Exception>? callbackErrors = null;
			await ExecuteCallbacksAsync(_onCommitCallbacks, callbackErrors).ConfigureAwait(false);

			// Execute complete callbacks
			await ExecuteCallbacksAsync(_onCompleteCallbacks, Status, callbackErrors).ConfigureAwait(false);

			if (callbackErrors is { Count: > 0 })
			{
				throw new AggregateException(
					$"One or more transaction callbacks failed for transaction {TransactionId}.",
					callbackErrors);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to commit transaction {TransactionId}", TransactionId);
			Status = TransactionStatus.Failed;

			// Try to rollback
			try
			{
				await RollbackInternalAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (Exception rollbackEx)
			{
				_logger.LogError(rollbackEx, "Failed to rollback transaction {TransactionId} after commit failure", TransactionId);
			}

			throw;
		}
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		ThrowIfDisposed();

		if (Status is not TransactionStatus.Active and not TransactionStatus.Committing)
		{
			_logger.LogWarning("Cannot rollback transaction {TransactionId} in status {Status}", TransactionId, Status);
			return;
		}

		await RollbackInternalAsync(cancellationToken).ConfigureAwait(false);
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

		lock (_lock)
		{
			if (_savepoints.Contains(savepointName))
			{
				throw new InvalidOperationException($"Savepoint {savepointName} already exists");
			}

			_ = _savepoints.Add(savepointName);
		}

		// Create savepoint in all transactions
		foreach (var transaction in _transactions.Values)
		{
			await transaction.SaveAsync(savepointName, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogDebug("Created savepoint {SavepointName} in transaction {TransactionId}", savepointName, TransactionId);
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

		lock (_lock)
		{
			if (!_savepoints.Contains(savepointName))
			{
				throw new InvalidOperationException($"Savepoint {savepointName} does not exist");
			}
		}

		// Rollback to savepoint in all transactions
		foreach (var transaction in _transactions.Values)
		{
			await transaction.RollbackAsync(savepointName, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogDebug("Rolled back to savepoint {SavepointName} in transaction {TransactionId}", savepointName, TransactionId);
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

		lock (_lock)
		{
			if (!_savepoints.Remove(savepointName))
			{
				throw new InvalidOperationException($"Savepoint {savepointName} does not exist");
			}
		}

		// Release savepoint in all transactions
		foreach (var transaction in _transactions.Values)
		{
			await transaction.ReleaseAsync(savepointName, cancellationToken).ConfigureAwait(false);
		}

		_logger.LogDebug("Released savepoint {SavepointName} in transaction {TransactionId}", savepointName, TransactionId);
	}

	/// <inheritdoc />
	public void OnCommit(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		lock (_lock)
		{
			_onCommitCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnRollback(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		lock (_lock)
		{
			_onRollbackCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnComplete(Func<TransactionStatus, Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
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

		_logger.LogDebug("Creating nested transaction scope with isolation level {IsolationLevel}", isolationLevel);

		// Postgres doesn't support true nested transactions, but we can use savepoints
		var nestedScope = new PostgresTransactionScope(isolationLevel, _logger);

		// Copy enlisted providers and connections to the nested scope
		foreach (var provider in _enlistedProviders)
		{
			nestedScope._enlistedProviders.Add(provider);
		}

		foreach (var connection in _enlistedConnections)
		{
			nestedScope._enlistedConnections.Add(connection);
		}

		// Share the same transactions
		foreach (var kvp in _transactions)
		{
			nestedScope._transactions[kvp.Key] = kvp.Value;
		}

		return nestedScope;
	}

	/// <summary>
	/// Gets the NpgsqlTransaction for a specific connection.
	/// </summary>
	/// <param name="connection"> The connection to get the transaction for. </param>
	/// <returns> The NpgsqlTransaction if found; otherwise, null. </returns>
	public NpgsqlTransaction? GetTransaction(NpgsqlConnection connection)
	{
		ArgumentNullException.ThrowIfNull(connection);
		return _transactions.GetValueOrDefault(connection.ConnectionString);
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
	/// Disposes the transaction scope.
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
			if (Status == TransactionStatus.Active)
			{
				try
				{
					// Use synchronous Rollback to avoid thread pool starvation during shutdown (AD-540.2)
					foreach (var transaction in _transactions.Values)
					{
						if (transaction.Connection?.State == ConnectionState.Open)
						{
							try
							{
								transaction.Rollback();
							}
							catch (Exception ex)
							{
								_logger.LogError(ex, "Error rolling back transaction in Dispose for {TransactionId}", TransactionId);
							}
						}
					}

					Status = TransactionStatus.RolledBack;
				}
				catch (Exception ex)
				{
					_logger.LogError(ex, "Error during automatic rollback of transaction {TransactionId}", TransactionId);
				}
			}

			// Dispose all transactions
			foreach (var transaction in _transactions.Values)
			{
				transaction.Dispose();
			}

			_transactions.Clear();
			_enlistedProviders.Clear();
			_enlistedConnections.Clear();
			_savepoints.Clear();

			Status = TransactionStatus.Disposed;
		}

		_disposed = true;
	}

	/// <summary>
	/// Asynchronously disposes the transaction scope.
	/// </summary>
	protected virtual async ValueTask DisposeAsyncCore()
	{
		if (_disposed)
		{
			return;
		}

		if (Status == TransactionStatus.Active)
		{
			try
			{
				// Automatically rollback if not committed
				using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
				await RollbackAsync(timeoutCts.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error during automatic rollback of transaction {TransactionId}", TransactionId);
			}
		}

		// Dispose all transactions
		foreach (var transaction in _transactions.Values)
		{
			await transaction.DisposeAsync().ConfigureAwait(false);
		}

		_transactions.Clear();
		_enlistedProviders.Clear();
		_enlistedConnections.Clear();
		_savepoints.Clear();

		Status = TransactionStatus.Disposed;
		_disposed = true;
	}

	private async Task RollbackInternalAsync(CancellationToken cancellationToken)
	{
		try
		{
			Status = TransactionStatus.RollingBack;
			_logger.LogDebug("Rolling back transaction {TransactionId}", TransactionId);

			// Rollback all enlisted transactions
			foreach (var transaction in _transactions.Values)
			{
				if (transaction.Connection?.State == ConnectionState.Open)
				{
					await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
				}
			}

			Status = TransactionStatus.RolledBack;
			_logger.LogInformation("Transaction {TransactionId} rolled back successfully", TransactionId);

			// Execute rollback callbacks — collect errors and propagate
			List<Exception>? callbackErrors = null;
			await ExecuteCallbacksAsync(_onRollbackCallbacks, callbackErrors).ConfigureAwait(false);

			// Execute complete callbacks
			await ExecuteCallbacksAsync(_onCompleteCallbacks, Status, callbackErrors).ConfigureAwait(false);

			if (callbackErrors is { Count: > 0 })
			{
				throw new AggregateException(
					$"One or more transaction callbacks failed for transaction {TransactionId}.",
					callbackErrors);
			}
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Failed to rollback transaction {TransactionId}", TransactionId);
			Status = TransactionStatus.Failed;
			throw;
		}
	}

	private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

	private void ThrowIfNotActive()
	{
		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Transaction {TransactionId} is not active. Current status: {Status}");
		}
	}

	private async Task ExecuteCallbacksAsync(IEnumerable<Func<Task>> callbacks, List<Exception>? errors)
	{
		foreach (var callback in callbacks)
		{
			try
			{
				await callback().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error executing callback in transaction {TransactionId}", TransactionId);
				errors ??= [];
				errors.Add(ex);
			}
		}
	}

	private async Task ExecuteCallbacksAsync(IEnumerable<Func<TransactionStatus, Task>> callbacks, TransactionStatus status, List<Exception>? errors)
	{
		foreach (var callback in callbacks)
		{
			try
			{
				await callback(status).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error executing callback in transaction {TransactionId}", TransactionId);
				errors ??= [];
				errors.Add(ex);
			}
		}
	}
}
