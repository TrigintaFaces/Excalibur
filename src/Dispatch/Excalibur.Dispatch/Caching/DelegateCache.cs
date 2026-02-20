// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// High-performance cache for frequently used delegates to avoid allocations. Implements PROF-003 optimization: Delegate caching (2-3%
/// allocation reduction).
/// </summary>
/// <remarks>
/// Supports both string keys (backward compatibility) and struct-based DelegateCacheKey for zero-allocation lookups.
/// </remarks>
public static class DelegateCache
{
	private static readonly ConcurrentDictionary<string, Delegate> _stringCache = new(StringComparer.Ordinal);
	private static readonly ConcurrentDictionary<DelegateCacheKey, Delegate> _structCache = new();
	private static long _hits;
	private static long _misses;

	/// <summary>
	/// Gets or creates a cached delegate using a string key.
	/// </summary>
	/// <remarks>
	/// This overload is maintained for backward compatibility. For hot paths, prefer the
	/// <see cref="GetOrCreate{TDelegate}(DelegateCacheKey, Func{TDelegate})"/> overload with
	/// <see cref="DelegateCacheKey"/> to avoid string allocations.
	/// </remarks>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TDelegate GetOrCreate<TDelegate>(string key, Func<TDelegate> factory)
		where TDelegate : Delegate
	{
		ArgumentNullException.ThrowIfNull(factory);

		if (_stringCache.TryGetValue(key, out var cached))
		{
			_ = Interlocked.Increment(ref _hits);
			return (TDelegate)cached;
		}

		_ = Interlocked.Increment(ref _misses);
		var newDelegate = factory();
		_ = _stringCache.TryAdd(key, newDelegate);
		return newDelegate;
	}

	/// <summary>
	/// Gets or creates a cached delegate using a zero-allocation struct key.
	/// </summary>
	/// <remarks>
	/// This overload uses <see cref="DelegateCacheKey"/> to eliminate string allocations during cache key
	/// generation. Prefer this overload in hot paths where cache lookups occur frequently.
	/// </remarks>
	/// <typeparam name="TDelegate">The type of delegate to cache.</typeparam>
	/// <param name="key">The struct-based cache key.</param>
	/// <param name="factory">Factory function to create the delegate if not cached.</param>
	/// <returns>The cached or newly created delegate.</returns>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	public static TDelegate GetOrCreate<TDelegate>(DelegateCacheKey key, Func<TDelegate> factory)
		where TDelegate : Delegate
	{
		ArgumentNullException.ThrowIfNull(factory);

		if (_structCache.TryGetValue(key, out var cached))
		{
			_ = Interlocked.Increment(ref _hits);
			return (TDelegate)cached;
		}

		_ = Interlocked.Increment(ref _misses);
		var newDelegate = factory();
		_ = _structCache.TryAdd(key, newDelegate);
		return newDelegate;
	}

	/// <summary>
	/// Creates a cached async delegate.
	/// </summary>
	public static Func<Task> GetAsyncAction(string key, Func<Task> action) => GetOrCreate(key, () => action);

	/// <summary>
	/// Creates a cached async delegate with parameter.
	/// </summary>
	public static Func<T, Task> GetAsyncAction<T>(string key, Func<T, Task> action) => GetOrCreate(key, () => action);

	/// <summary>
	/// Creates a cached value task returning delegate.
	/// </summary>
	public static Func<ValueTask> GetValueTaskAction(string key, Func<ValueTask> action) => GetOrCreate(key, () => action);

	/// <summary>
	/// Gets cache statistics.
	/// </summary>
	/// <returns>A tuple containing hit count, miss count, and total cache size (both string and struct caches).</returns>
	public static (long hits, long misses, int cacheSize) GetStatistics() =>
		(Interlocked.Read(ref _hits), Interlocked.Read(ref _misses), _stringCache.Count + _structCache.Count);

	/// <summary>
	/// Gets cache statistics with detailed breakdown.
	/// </summary>
	/// <returns>A tuple containing hit count, miss count, string cache size, and struct cache size.</returns>
	public static (long hits, long misses, int stringCacheSize, int structCacheSize) GetDetailedStatistics() =>
		(Interlocked.Read(ref _hits), Interlocked.Read(ref _misses), _stringCache.Count, _structCache.Count);

	/// <summary>
	/// Clears all caches.
	/// </summary>
	public static void Clear()
	{
		_stringCache.Clear();
		_structCache.Clear();
	}
}
