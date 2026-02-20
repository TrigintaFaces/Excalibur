// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Pooling;

/// <summary>
/// High-performance pool for dictionaries to reduce allocations.
/// </summary>
/// <remarks> Initializes a new instance of the DictionaryPool class. </remarks>
/// <param name="maxPoolSize"> Maximum number of dictionaries to keep in the pool. </param>
/// <param name="initialCapacity"> Initial capacity for new dictionaries. </param>
public sealed class DictionaryPool<TKey, TValue>(int maxPoolSize = 64, int initialCapacity = 16)
	where TKey : notnull
{
	private readonly ConcurrentBag<Dictionary<TKey, TValue>> _pool = [];

	/// <summary>
	/// Rents a dictionary from the pool.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public Dictionary<TKey, TValue> Rent() =>
		_pool.TryTake(out var dictionary) ? dictionary : new Dictionary<TKey, TValue>(initialCapacity);

	/// <summary>
	/// Returns a dictionary to the pool.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public void Return(Dictionary<TKey, TValue>? dictionary)
	{
		if (dictionary == null)
		{
			return;
		}

		dictionary.Clear();

		if (_pool.Count < maxPoolSize)
		{
			_pool.Add(dictionary);
		}
	}

	/// <summary>
	/// Creates a pooled dictionary that will automatically return to the pool when disposed.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public PooledMap<TKey, TValue> CreatePooled() => new(this);
}
