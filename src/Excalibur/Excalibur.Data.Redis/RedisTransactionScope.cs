// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;
using System.Globalization;
using System.Text;

using Excalibur.Data.Abstractions.Persistence;

using StackExchange.Redis;

namespace Excalibur.Data.Redis;

/// <summary>
/// Redis transaction scope implementation.
/// </summary>
internal sealed class RedisTransactionScope : ITransactionScope, ITransactionScopeCallbacks
{
	private static readonly CompositeFormat CannotCommitTransactionFormat =
			CompositeFormat.Parse(Resources.RedisTransactionScope_CannotCommitTransactionInStateFormat);
	private static readonly CompositeFormat CannotRollbackTransactionFormat =
			CompositeFormat.Parse(Resources.RedisTransactionScope_CannotRollbackTransactionInStateFormat);


	private readonly RedisPersistenceProvider _provider;
	private readonly TimeSpan? _timeout;
	private readonly List<Func<Task>> _onCommitCallbacks = [];
	private readonly List<Func<Task>> _onRollbackCallbacks = [];
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks = [];
	private readonly List<IPersistenceProvider> _enlistedProviders = [];
	private ITransaction? _transaction;
	private volatile bool _disposed;

	public RedisTransactionScope(RedisPersistenceProvider provider, IsolationLevel isolationLevel, TimeSpan? timeout)
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
			throw new ObjectDisposedException(nameof(RedisTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.InvariantCulture,
							CannotCommitTransactionFormat,
							Status));
		}

		try
		{
			// Execute commit callbacks
			foreach (var callback in _onCommitCallbacks)
			{
				await callback().ConfigureAwait(false);
			}

			if (_transaction != null)
			{
				_ = await _transaction.ExecuteAsync().ConfigureAwait(false);
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
			throw new ObjectDisposedException(nameof(RedisTransactionScope));
		}

		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException(
					string.Format(
							CultureInfo.InvariantCulture,
							CannotRollbackTransactionFormat,
							Status));
		}

		try
		{
			// Execute rollback callbacks
			foreach (var callback in _onRollbackCallbacks)
			{
				await callback().ConfigureAwait(false);
			}

			// Redis transactions are atomic by nature - if not executed, they're automatically discarded ITransaction doesn't implement
			// IDisposable in StackExchange.Redis
			_transaction = null;

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

		// Redis transaction scope only supports the creating provider
		if (provider != _provider)
		{
			throw new NotSupportedException(Resources.RedisTransactionScope_OnlySupportsCreatingProvider);
		}

		if (!_enlistedProviders.Contains(provider))
		{
			_enlistedProviders.Add(provider);
		}

		return Task.CompletedTask;
	}

	/// <inheritdoc />
	public Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken) =>

		// Redis handles connections through the multiplexer
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

		// ITransaction doesn't implement IDisposable in StackExchange.Redis
		_transaction = null;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync()
	{
		Dispose();
		return ValueTask.CompletedTask;
	}
}
