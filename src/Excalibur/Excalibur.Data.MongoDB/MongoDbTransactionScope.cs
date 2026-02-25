// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Persistence;

using MongoDB.Driver;

namespace Excalibur.Data.MongoDB;

/// <summary>
/// MongoDB transaction scope implementation.
/// </summary>
internal sealed class MongoDbTransactionScope(MongoDbPersistenceProvider provider, IsolationLevel isolationLevel, TimeSpan? timeout)
	: ITransactionScope, ITransactionScopeCallbacks
{
	private readonly List<IPersistenceProvider> _enlistedProviders = [provider];
	private readonly List<Func<Task>> _onCommitCallbacks = [];
	private readonly List<Func<Task>> _onRollbackCallbacks = [];
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks = [];
	private volatile bool _disposed;

	// Lazy session initialization fields (T392.2)
	private IClientSessionHandle? _session;
	private readonly SemaphoreSlim _sessionLock = new(1, 1);
	private bool _sessionInitialized;

	/// <inheritdoc />
	public string TransactionId { get; } = Guid.NewGuid().ToString();

	/// <inheritdoc />
	public IsolationLevel IsolationLevel { get; } = isolationLevel;

	/// <inheritdoc />
	public TransactionStatus Status { get; private set; } = TransactionStatus.Active;

	/// <inheritdoc />
	public DateTimeOffset StartTime { get; } = DateTimeOffset.UtcNow;

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; } = timeout ?? TimeSpan.FromMinutes(5);

	/// <summary>
	/// Gets the MongoDB database associated with this transaction scope.
	/// </summary>
	/// <value>
	/// The MongoDB database associated with this transaction scope.
	/// </value>
	public IMongoDatabase Database => provider.GetDatabase();

	/// <summary>
	/// Gets the MongoDB session handle for this transaction scope.
	/// Returns null if session has not been initialized yet.
	/// </summary>
	public IClientSessionHandle? Session => _session;

	/// <summary>
	/// Ensures the MongoDB session is initialized using lazy initialization.
	/// Thread-safe via double-checked locking pattern.
	/// </summary>
	private async Task EnsureSessionAsync(CancellationToken cancellationToken)
	{
		// Fast path: already initialized
		if (_sessionInitialized)
		{
			return;
		}

		await _sessionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
		try
		{
			// Double-check after acquiring lock
			if (_sessionInitialized)
			{
				return;
			}

			// Use provider's existing BeginSessionAsync method which handles TransactionOptions
			_session = await provider.BeginSessionAsync(cancellationToken).ConfigureAwait(false);
			_sessionInitialized = true;
		}
		finally
		{
			_ = _sessionLock.Release();
		}
	}

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(MongoDbTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Cannot commit transaction in {Status} state");
		}

		// Initialize session if not already done (lazy initialization)
		await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

		// Commit the MongoDB transaction
		if (_session is not null)
		{
			await _session.CommitTransactionAsync(cancellationToken).ConfigureAwait(false);
		}

		Status = TransactionStatus.Committed;

		// Execute commit callbacks
		foreach (var callback in _onCommitCallbacks)
		{
			try
			{
				await callback().ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Log but don't throw - callbacks shouldn't break the commit
			}
		}

		// Execute complete callbacks
		foreach (var callback in _onCompleteCallbacks)
		{
			try
			{
				await callback(Status).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Log but don't throw - callbacks shouldn't break the commit
			}
		}
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(MongoDbTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Cannot rollback transaction in {Status} state");
		}

		// Initialize session if not already done (lazy initialization)
		await EnsureSessionAsync(cancellationToken).ConfigureAwait(false);

		// Abort the MongoDB transaction
		if (_session is not null)
		{
			await _session.AbortTransactionAsync(cancellationToken).ConfigureAwait(false);
		}

		Status = TransactionStatus.RolledBack;

		// Execute rollback callbacks
		foreach (var callback in _onRollbackCallbacks)
		{
			try
			{
				await callback().ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Log but don't throw - callbacks shouldn't break the rollback
			}
		}

		// Execute complete callbacks
		foreach (var callback in _onCompleteCallbacks)
		{
			try
			{
				await callback(Status).ConfigureAwait(false);
			}
			catch (Exception)
			{
				// Log but don't throw - callbacks shouldn't break the rollback
			}
		}
	}

	/// <inheritdoc />
	public Task EnlistProviderAsync(IPersistenceProvider provider1, CancellationToken cancellationToken)
	{
		// MongoDB transaction scope only supports the creating provider
		if (provider1 != provider)
		{
			throw new NotSupportedException("MongoDB transaction scope only supports the creating provider");
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken) =>

		// MongoDB handles connections through sessions
		Task.CompletedTask;

	/// <inheritdoc />
	public void OnCommit(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		_onCommitCallbacks.Add(callback);
	}

	/// <inheritdoc />
	public void OnRollback(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		_onRollbackCallbacks.Add(callback);
	}

	/// <inheritdoc />
	public void OnComplete(Func<TransactionStatus, Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		_onCompleteCallbacks.Add(callback);
	}

	/// <inheritdoc />
	public IEnumerable<IPersistenceProvider> GetEnlistedProviders() => _enlistedProviders.AsReadOnly();

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_disposed = true;

		// Dispose session if initialized
		_session?.Dispose();

		// Dispose the semaphore
		_sessionLock.Dispose();
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		if (_disposed)
		{
			return ValueTask.CompletedTask;
		}

		_disposed = true;

		// Dispose session if initialized
		_session?.Dispose();

		// Dispose the semaphore
		_sessionLock.Dispose();

		return ValueTask.CompletedTask;
	}
}
