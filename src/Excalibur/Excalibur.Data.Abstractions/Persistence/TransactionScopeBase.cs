// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Data;

namespace Excalibur.Data.Abstractions.Persistence;

/// <summary>
/// Provides a base implementation for <see cref="ITransactionScope"/> and <see cref="ITransactionScopeCallbacks"/>
/// with common state management, callback registration, and disposal logic.
/// </summary>
/// <remarks>
/// <para>
/// This base class consolidates shared logic that was duplicated across SqlServer,
/// Postgres, and other transaction scope implementations:
/// </para>
/// <list type="bullet">
/// <item>Transaction state (TransactionId, IsolationLevel, Status, StartTime, Timeout)</item>
/// <item>Callback registration (OnCommit, OnRollback, OnComplete)</item>
/// <item>Thread-safe callback/provider collections via lock</item>
/// <item>Disposed/active guard methods</item>
/// </list>
/// <para>
/// Derived classes implement provider-specific commit, rollback, and disposal logic.
/// </para>
/// </remarks>
public abstract class TransactionScopeBase : ITransactionScope, ITransactionScopeCallbacks
{
	private readonly List<IPersistenceProvider> _enlistedProviders = [];
	private readonly List<Func<Task>> _onCommitCallbacks = [];
	private readonly List<Func<Task>> _onRollbackCallbacks = [];
	private readonly List<Func<TransactionStatus, Task>> _onCompleteCallbacks = [];

#if NET9_0_OR_GREATER
	private readonly Lock _syncLock = new();
#else
	private readonly object _syncLock = new();
#endif

	/// <summary>
	/// Gets a value indicating whether this scope has been disposed.
	/// </summary>
	protected bool Disposed { get; set; }

	/// <summary>
	/// Initializes a new instance of the <see cref="TransactionScopeBase"/> class.
	/// </summary>
	/// <param name="isolationLevel"> The transaction isolation level. </param>
	/// <param name="timeout"> The transaction timeout. </param>
	protected TransactionScopeBase(IsolationLevel isolationLevel, TimeSpan timeout)
	{
		TransactionId = Guid.NewGuid().ToString("N");
		IsolationLevel = isolationLevel;
		Timeout = timeout;
		StartTime = DateTimeOffset.UtcNow;
		Status = TransactionStatus.Active;
	}

	/// <inheritdoc />
	public string TransactionId { get; }

	/// <inheritdoc />
	public IsolationLevel IsolationLevel { get; }

	/// <inheritdoc />
	public TransactionStatus Status { get; protected set; }

	/// <inheritdoc />
	public DateTimeOffset StartTime { get; }

	/// <inheritdoc />
	public TimeSpan Timeout { get; set; }

	/// <inheritdoc />
	public abstract Task CommitAsync(CancellationToken cancellationToken);

	/// <inheritdoc />
	public abstract Task RollbackAsync(CancellationToken cancellationToken);

	/// <inheritdoc />
	public abstract Task EnlistProviderAsync(IPersistenceProvider provider, CancellationToken cancellationToken);

	/// <inheritdoc />
	public abstract Task EnlistConnectionAsync(IDbConnection connection, CancellationToken cancellationToken);

	/// <inheritdoc />
	public IEnumerable<IPersistenceProvider> GetEnlistedProviders()
	{
		lock (_syncLock)
		{
			return _enlistedProviders.ToList();
		}
	}

	/// <inheritdoc />
	public void OnCommit(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_syncLock)
		{
			_onCommitCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnRollback(Func<Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_syncLock)
		{
			_onRollbackCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void OnComplete(Func<TransactionStatus, Task> callback)
	{
		ArgumentNullException.ThrowIfNull(callback);
		ThrowIfDisposed();

		lock (_syncLock)
		{
			_onCompleteCallbacks.Add(callback);
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	/// <inheritdoc />
	public abstract ValueTask DisposeAsync();

	/// <summary>
	/// Releases managed and unmanaged resources.
	/// </summary>
	/// <param name="disposing"><see langword="true"/> to release managed resources; <see langword="false"/> for unmanaged only.</param>
	protected abstract void Dispose(bool disposing);

	/// <summary>
	/// Thread-safe enlist of a provider.
	/// </summary>
	/// <param name="provider"> The provider to enlist. </param>
	/// <returns> True if provider was newly enlisted; false if already enlisted. </returns>
	protected bool TryEnlistProvider(IPersistenceProvider provider)
	{
		lock (_syncLock)
		{
			if (_enlistedProviders.Contains(provider))
			{
				return false;
			}

			_enlistedProviders.Add(provider);
			return true;
		}
	}

	/// <summary>
	/// Executes commit callbacks, collecting any exceptions.
	/// </summary>
	/// <returns> List of exceptions from failed callbacks, or null if all succeeded. </returns>
	protected async Task<List<Exception>?> ExecuteCommitCallbacksAsync()
	{
		List<Exception>? errors = null;

		foreach (var callback in _onCommitCallbacks)
		{
			try
			{
				await callback().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				errors ??= [];
				errors.Add(ex);
			}
		}

		return errors;
	}

	/// <summary>
	/// Executes rollback callbacks, collecting any exceptions.
	/// </summary>
	/// <returns> List of exceptions from failed callbacks, or null if all succeeded. </returns>
	protected async Task<List<Exception>?> ExecuteRollbackCallbacksAsync()
	{
		List<Exception>? errors = null;

		foreach (var callback in _onRollbackCallbacks)
		{
			try
			{
				await callback().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				errors ??= [];
				errors.Add(ex);
			}
		}

		return errors;
	}

	/// <summary>
	/// Executes complete callbacks, appending any exceptions to the provided list.
	/// </summary>
	/// <param name="errors"> List to append exceptions to. May be null; will be created if needed. </param>
	/// <returns> The errors list (may be null if no errors occurred). </returns>
	protected async Task<List<Exception>?> ExecuteCompleteCallbacksAsync(List<Exception>? errors)
	{
		foreach (var callback in _onCompleteCallbacks)
		{
			try
			{
				await callback(Status).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				errors ??= [];
				errors.Add(ex);
			}
		}

		return errors;
	}

	/// <summary>
	/// Throws <see cref="ObjectDisposedException"/> if this scope has been disposed.
	/// </summary>
	protected void ThrowIfDisposed()
	{
		ObjectDisposedException.ThrowIf(Disposed, this);
	}

	/// <summary>
	/// Throws <see cref="InvalidOperationException"/> if this scope is not in Active status.
	/// Also checks for timeout.
	/// </summary>
	protected void ThrowIfNotActive()
	{
		if (Status != TransactionStatus.Active)
		{
			throw new InvalidOperationException(
				$"Transaction {TransactionId} is not active (current status: {Status})");
		}

		// Check for timeout
		if (DateTimeOffset.UtcNow - StartTime > Timeout)
		{
			Status = TransactionStatus.TimedOut;
			throw new TimeoutException($"Transaction {TransactionId} has timed out");
		}
	}

	/// <summary>
	/// Throws an <see cref="AggregateException"/> if any callback errors occurred.
	/// </summary>
	/// <param name="errors"> The list of errors to check. </param>
	protected void ThrowIfCallbackErrors(List<Exception>? errors)
	{
		if (errors is { Count: > 0 })
		{
			throw new AggregateException(
				$"One or more transaction callbacks failed for transaction {TransactionId}.",
				errors);
		}
	}
}
