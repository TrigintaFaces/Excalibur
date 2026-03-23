// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// Extension methods for bounded ConcurrentDictionary access. Implements the established S543 pattern:
/// cap + skip-when-full to prevent unbounded memory growth from type-keyed caches.
/// </summary>
internal static class BoundedConcurrentDictionary
{
	/// <summary>
	/// Maximum number of entries for type-keyed caches. Matches the established convention
	/// from Sprint 543: 1,024 for type-keyed caches.
	/// </summary>
	internal const int MaxTypeCacheEntries = 1024;

	/// <summary>
	/// Attempts to add a key-value pair to the dictionary, but skips the addition when the dictionary
	/// has reached the bounded capacity. This prevents unbounded memory growth while preserving
	/// correctness -- a cache miss simply means the value is recomputed on the next access.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static bool BoundedTryAdd<TKey, TValue>(
		this ConcurrentDictionary<TKey, TValue> dictionary,
		TKey key,
		TValue value,
		int maxEntries = MaxTypeCacheEntries)
		where TKey : notnull
	{
		if (dictionary.Count >= maxEntries)
		{
			return false;
		}

		return dictionary.TryAdd(key, value);
	}

	/// <summary>
	/// Gets or adds a value using the factory, but skips caching when the dictionary has reached
	/// the bounded capacity. Returns the computed value regardless of whether it was cached.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TValue BoundedGetOrAdd<TKey, TValue>(
		this ConcurrentDictionary<TKey, TValue> dictionary,
		TKey key,
		Func<TKey, TValue> valueFactory,
		int maxEntries = MaxTypeCacheEntries)
		where TKey : notnull
	{
		if (dictionary.TryGetValue(key, out var existing))
		{
			return existing;
		}

		var value = valueFactory(key);

		if (dictionary.Count < maxEntries)
		{
			return dictionary.GetOrAdd(key, value);
		}

		return value;
	}

	/// <summary>
	/// Gets or adds a value using the factory with state, but skips caching when the dictionary
	/// has reached the bounded capacity. Returns the computed value regardless of whether it was cached.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	internal static TValue BoundedGetOrAdd<TKey, TValue, TArg>(
		this ConcurrentDictionary<TKey, TValue> dictionary,
		TKey key,
		Func<TKey, TArg, TValue> valueFactory,
		TArg factoryArgument,
		int maxEntries = MaxTypeCacheEntries)
		where TKey : notnull
	{
		if (dictionary.TryGetValue(key, out var existing))
		{
			return existing;
		}

		var value = valueFactory(key, factoryArgument);

		if (dictionary.Count < maxEntries)
		{
			return dictionary.GetOrAdd(key, value);
		}

		return value;
	}
}
