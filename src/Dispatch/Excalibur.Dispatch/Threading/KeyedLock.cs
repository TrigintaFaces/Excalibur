// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Threading;

/// <summary>
/// Default implementation of IKeyedLock using SemaphoreSlim.
/// </summary>
public sealed class KeyedLock : IKeyedLock
{
	private readonly Dictionary<string, SemaphoreSlim> _locks = [];
#if NET9_0_OR_GREATER

	private readonly Lock _lockObj = new();

#else

	private readonly object _lockObj = new();

#endif

	/// <summary>
	/// Acquires a lock for the specified key asynchronously, creating a new semaphore if one doesn't exist.
	/// </summary>
	/// <param name="key"> The key to lock on. </param>
	/// <param name="cancellationToken"> The cancellation token. </param>
	/// <returns> A disposable lock handle that releases the lock when disposed. </returns>
	public async Task<IDisposable> AcquireAsync(string key, CancellationToken cancellationToken)
	{
		SemaphoreSlim semaphore;
		lock (_lockObj)
		{
			if (!_locks.TryGetValue(key, out semaphore!))
			{
				semaphore = new SemaphoreSlim(1, 1);
				_locks[key] = semaphore;
			}
		}

		await semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
		return new LockHandle(semaphore);
	}

	private sealed class LockHandle(SemaphoreSlim semaphore) : IDisposable
	{
		public void Dispose() => semaphore.Release();
	}
}
