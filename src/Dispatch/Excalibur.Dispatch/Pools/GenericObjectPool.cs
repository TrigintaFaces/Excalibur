// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Pools;

/// <summary>
/// Generic object pool implementation.
/// </summary>
public sealed class GenericObjectPool<T>(Func<T> factory, Action<T>? reset, int maxSize)
	where T : class
{
	private readonly Func<T> _factory = factory ?? throw new ArgumentNullException(nameof(factory));
	private readonly Stack<T> _pool = new(maxSize);
#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif

	/// <summary>
	/// Rents an object from the pool, returning a pooled instance if available or creating a new one using the factory.
	/// </summary>
	/// <returns> An object instance, either from the pool or newly created. </returns>
	public T Rent()
	{
		lock (_lock)
		{
			if (_pool.Count > 0)
			{
				return _pool.Pop();
			}
		}

		return _factory();
	}

	/// <summary>
	/// Returns an object to the pool for reuse, subject to pool capacity limits.
	/// </summary>
	/// <param name="item"> The object to return to the pool. Null items are ignored. </param>
	public void Return(T? item)
	{
		if (item == null)
		{
			return;
		}

		reset?.Invoke(item);

		lock (_lock)
		{
			if (_pool.Count < maxSize)
			{
				_pool.Push(item);
			}
		}
	}
}
