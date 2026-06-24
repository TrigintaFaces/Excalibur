// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Default implementation of <see cref="IKeyedLock"/> backed by a per-key <see cref="SemaphoreSlim"/>.
/// </summary>
/// <remarks>
/// Each key maps to an <see cref="Entry"/> holding its semaphore and a reference count of the acquirers
/// and waiters currently using it. The semaphore is removed and disposed only when the last reference is
/// released (reference count returns to zero), which keeps memory bounded without ever disposing a
/// semaphore that a concurrent waiter still references. The reference count is mutated exclusively under
/// <see cref="_lockObj"/>, so "remove a key's semaphore while it is still referenced" is structurally
/// inexpressible — closing the classic keyed-semaphore cleanup race (a waiter observing
/// <see cref="ObjectDisposedException"/>, or two holders for one key).
/// </remarks>
public sealed class KeyedLock : IKeyedLock
{
	private readonly Dictionary<string, Entry> _locks = [];
	private readonly Lock _lockObj = new();

	/// <summary>
	/// Acquires a lock for the specified key asynchronously, creating a new semaphore if one doesn't exist.
	/// </summary>
	/// <param name="key"> The key to lock on. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A disposable lock handle that releases the lock when disposed. </returns>
	public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
	{
		ArgumentNullException.ThrowIfNull(key);

		Entry entry;
		lock (_lockObj)
		{
			if (!_locks.TryGetValue(key, out entry!))
			{
				entry = new Entry();
				_locks[key] = entry;
			}

			// Reserve a reference BEFORE awaiting outside the lock. While the reference count is above
			// zero the entry is guaranteed to stay live, so a concurrent Dispose can never remove and
			// dispose the semaphore out from under this waiter.
			entry.RefCount++;
		}

		try
		{
			await entry.Semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		}
		catch
		{
			// WaitAsync threw (typically cancellation): this caller never became a holder. SemaphoreSlim
			// is atomic on the cancellation path — it did NOT take the semaphore — so we release only the
			// reference we reserved above, never the semaphore itself (avoids a leaked reference).
			ReleaseReference(key, entry);
			throw;
		}

		return new LockHandle(this, key, entry);
	}

	/// <summary>
	/// Releases one reference to <paramref name="key"/>'s entry, removing and disposing the semaphore
	/// only when the final reference is released.
	/// </summary>
	private void ReleaseReference(string key, Entry entry)
	{
		lock (_lockObj)
		{
			// Decrement, zero-test, and removal happen as one atomic step under the lock, so the
			// semaphore is disposed only when no acquirer or waiter still references this entry.
			if (--entry.RefCount == 0)
			{
				_ = _locks.Remove(key);
				entry.Semaphore.Dispose();
			}
		}
	}

	/// <summary>
	/// A per-key holder pairing the gating semaphore with the count of acquirers and waiters that
	/// currently reference it. <see cref="RefCount"/> is guarded exclusively by <see cref="_lockObj"/>.
	/// </summary>
	private sealed class Entry
	{
		public SemaphoreSlim Semaphore { get; } = new(1, 1);

		public int RefCount { get; set; }
	}

	private sealed class LockHandle(KeyedLock owner, string key, Entry entry) : IDisposable
	{
		// volatile: LockHandle.Dispose may be called from any thread, so the guard read/write must be
		// visible across threads (project-wide _disposed conformance rule, S569).
		private volatile bool _disposed;

		public void Dispose()
		{
			// Idempotent: a second Dispose must not release the semaphore twice or drop an extra reference.
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			entry.Semaphore.Release();
			owner.ReleaseReference(key, entry);
		}
	}
}
