// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

using Excalibur.Data.Abstractions.Persistence;

namespace Excalibur.Data.InMemory;

/// <summary>
/// In-memory transaction scope implementation.
/// </summary>
internal sealed class InMemoryTransactionScope : ITransactionScope, ITransactionScopeCallbacks
{
	private const string InMemoryTransactionScopeOnlySupportsCreatingProvider = "In-memory transaction scope only supports creating provider.";

	private readonly InMemoryPersistenceProvider _provider;
	private readonly TimeSpan? _timeout;
	private readonly List<Func<Task>> _onCommitCallbacks = [];
	private readonly List<Func<Task>> _onRollbackCallbacks = [];
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks = [];
	private readonly List<IPersistenceProvider> _enlistedProviders = [];
	private volatile bool _disposed;

	public InMemoryTransactionScope(InMemoryPersistenceProvider provider, IsolationLevel isolationLevel, TimeSpan? timeout)
	{
		_provider = provider;
		IsolationLevel = isolationLevel;
		_timeout = timeout;
		TransactionId = Guid.NewGuid().ToString();
		StartTime = DateTimeOffset.UtcNow;
		Timeout = timeout ?? TimeSpan.FromMinutes(1);

		// Enlist the creating provider
		_enlistedProviders.Add(provider);
	}

	/// <inheritdoc />
	public string TransactionId { get; }

	/// <inheritdoc />
	public IsolationLevel IsolationLevel { get; }

	/// <inheritdoc />
	public TransactionStatus Status { get; private set; } = TransactionStatus.Active;

	/// <inheritdoc />
	public DateTimeOffset StartTime { get; }

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; }

	/// <inheritdoc />
	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(InMemoryTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Cannot commit transaction in {Status} state");
		}

		try
		{
			// Execute commit callbacks
			foreach (var callback in _onCommitCallbacks)
			{
				await callback().ConfigureAwait(false);
			}

			Status = TransactionStatus.Committed;

			// Execute completion callbacks
			foreach (var callback in _onCompleteCallbacks)
			{
				await callback(Status).ConfigureAwait(false);
			}
		}
		catch
		{
			Status = TransactionStatus.RolledBack;
			throw;
		}
	}

	/// <inheritdoc />
	public async Task RollbackAsync(CancellationToken cancellationToken)
	{
		if (_disposed)
		{
			throw new ObjectDisposedException(nameof(InMemoryTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException($"Cannot rollback transaction in {Status} state");
		}

		try
		{
			// Execute rollback callbacks
			foreach (var callback in _onRollbackCallbacks)
			{
				await callback().ConfigureAwait(false);
			}

			Status = TransactionStatus.RolledBack;

			// Execute completion callbacks
			foreach (var callback in _onCompleteCallbacks)
			{
				await callback(Status).ConfigureAwait(false);
			}
		}
		catch
		{
			// Ensure status is set even if callbacks fail
			Status = TransactionStatus.RolledBack;
			throw;
		}
	}

	/// <inheritdoc />
	public Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(provider);

		// In-memory provider doesn't support distributed transactions
		if (provider != _provider)
		{
			throw new NotSupportedException(InMemoryTransactionScopeOnlySupportsCreatingProvider);
		}

		if (!_enlistedProviders.Contains(provider))
		{
			_enlistedProviders.Add(provider);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken) =>

		// In-memory provider handles connections internally
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
		if (Status == TransactionStatus.Active)
		{
			Status = TransactionStatus.RolledBack;
		}
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}
}
