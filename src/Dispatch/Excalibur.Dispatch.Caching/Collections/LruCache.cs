// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Abstractions.Messaging;
using Excalibur.Dispatch.Caching.Diagnostics;

namespace Excalibur.Dispatch.Caching;

/// <summary>
/// A thread-safe, high-performance Least Recently Used (LRU) cache implementation with configurable capacity and optional TTL support.
/// Provides O(1) get/set operations with automatic eviction of least recently used items when capacity is reached.
/// </summary>
/// <typeparam name="TKey"> The type of keys in the cache. Must not be null. </typeparam>
/// <typeparam name="TValue"> The type of values in the cache. </typeparam>
public sealed class LruCache<TKey, TValue> : IDisposable
	where TKey : notnull
{
	private readonly Counter<long> _lruHitCounter;
	private readonly Counter<long> _lruMissCounter;
	private readonly Counter<long> _lruEvictionCounter;
	private readonly Counter<long> _lruExpirationCounter;

#if NET9_0_OR_GREATER

	private readonly Lock _lock = new();

#else

	private readonly object _lock = new();

#endif
	private readonly Dictionary<TKey, LinkedListNode<CacheEntry>> _cache;
	private readonly LinkedList<CacheEntry> _lruList;
	private readonly TimeSpan? _defaultTtl;
	private readonly Timer? _cleanupTimer;
	private volatile bool _disposed;

	/// <summary>
	/// Cache hit count for performance metrics.
	/// </summary>
	private long _hits;

	private long _misses;
	private long _evictions;
	private long _expirations;

	/// <summary>
	/// Initializes a new instance of the <see cref="LruCache{TKey,TValue}" /> class with specified capacity and optional TTL.
	/// </summary>
	/// <param name="capacity"> The maximum number of items the cache can hold. Must be greater than 0. </param>
	/// <param name="defaultTtl"> Optional time-to-live for cached items. If null, items don't expire based on time. </param>
	/// <param name="cleanupInterval"> Optional interval for cleaning up expired items. Defaults to 1 minute if TTL is specified. </param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when capacity is less than or equal to 0. </exception>
	public LruCache(int capacity, TimeSpan? defaultTtl = null, TimeSpan? cleanupInterval = null)
		: this(capacity, meterFactory: null, defaultTtl, cleanupInterval)
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="LruCache{TKey,TValue}" /> class with specified capacity, meter factory, and optional TTL.
	/// </summary>
	/// <param name="capacity"> The maximum number of items the cache can hold. Must be greater than 0. </param>
	/// <param name="meterFactory"> Optional meter factory for DI-managed meter lifecycle. If null, creates an unmanaged meter. </param>
	/// <param name="defaultTtl"> Optional time-to-live for cached items. If null, items don't expire based on time. </param>
	/// <param name="cleanupInterval"> Optional interval for cleaning up expired items. Defaults to 1 minute if TTL is specified. </param>
	/// <exception cref="ArgumentOutOfRangeException"> Thrown when capacity is less than or equal to 0. </exception>
	[SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope",
		Justification = "Meter lifecycle is managed by IMeterFactory or lives as long as the LruCache instance")]
	public LruCache(int capacity, IMeterFactory? meterFactory, TimeSpan? defaultTtl = null, TimeSpan? cleanupInterval = null)
	{
		if (capacity <= 0)
		{
			throw new ArgumentOutOfRangeException(
					nameof(capacity),
					Resources.LruCache_CapacityMustBeGreaterThanZero);
		}

		Capacity = capacity;
		_defaultTtl = defaultTtl;
		_cache = new Dictionary<TKey, LinkedListNode<CacheEntry>>(capacity);
		_lruList = [];

		var meter = meterFactory?.Create(DispatchCachingTelemetryConstants.MeterName)
			?? new Meter(DispatchCachingTelemetryConstants.MeterName, DispatchCachingTelemetryConstants.Version);
		_lruHitCounter = meter.CreateCounter<long>("dispatch.cache.lru.hits", "hits", "Number of LRU cache hits");
		_lruMissCounter = meter.CreateCounter<long>("dispatch.cache.lru.misses", "misses", "Number of LRU cache misses");
		_lruEvictionCounter = meter.CreateCounter<long>("dispatch.cache.lru.evictions", "evictions", "Number of LRU cache evictions");
		_lruExpirationCounter = meter.CreateCounter<long>("dispatch.cache.lru.expirations", "expirations", "Number of LRU cache expirations");

		// Set up cleanup timer if TTL is specified
		if (_defaultTtl.HasValue)
		{
			var interval = cleanupInterval ?? TimeSpan.FromMinutes(1);
			_cleanupTimer = new Timer(
				_ => RemoveExpiredItems(),
				state: null,
				interval,
				interval);
		}
	}

	/// <summary>
	/// Gets the current number of items in the cache.
	/// </summary>
	/// <value>The current number of items in the cache.</value>
	public int Count
	{
		get
		{
			lock (_lock)
			{
				return _cache.Count;
			}
		}
	}

	/// <summary>
	/// Gets the maximum capacity of the cache.
	/// </summary>
	/// <value>The maximum capacity of the cache.</value>
	public int Capacity { get; }

	/// <summary>
	/// Gets the cache statistics including hit rate, miss rate, and eviction count.
	/// </summary>
	/// <value>The cache statistics including hit rate, miss rate, and eviction count.</value>
	public CacheStatistics Statistics
	{
		get
		{
			lock (_lock)
			{
				_ = _hits + _misses;
				return new CacheStatistics
				{
					Hits = _hits,
					Misses = _misses,
					Evictions = _evictions,
					Expirations = _expirations,
					CurrentSize = _cache.Count,
					MaxSize = Capacity,
				};
			}
		}
	}

	/// <summary>
	/// Attempts to get a value from the cache.
	/// </summary>
	/// <param name="key"> The key of the value to get. </param>
	/// <param name="value">
	/// When this method returns, contains the value associated with the specified key if found; otherwise, the default value.
	/// </param>
	/// <returns> true if the cache contains an element with the specified key; otherwise, false. </returns>
	public bool TryGetValue(TKey key, out TValue? value)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out var node))
			{
				// Check if expired
				if (IsExpired(node.Value))
				{
					RemoveNode(node);
					_ = Interlocked.Increment(ref _expirations);
					_lruExpirationCounter.Add(1);
					_ = Interlocked.Increment(ref _misses);
					_lruMissCounter.Add(1);
					value = default;
					return false;
				}

				// Move to front (most recently used)
				_lruList.Remove(node);
				_lruList.AddFirst(node);

				// Update access time for TTL
				if (_defaultTtl.HasValue)
				{
					node.Value.LastAccessed = DateTimeOffset.UtcNow;
				}

				_ = Interlocked.Increment(ref _hits);
				_lruHitCounter.Add(1);
				value = node.Value.Value;
				return true;
			}

			_ = Interlocked.Increment(ref _misses);
			_lruMissCounter.Add(1);
			value = default;
			return false;
		}
	}

	/// <summary>
	/// Adds or updates a value in the cache with the specified key.
	/// </summary>
	/// <param name="key"> The key of the element to add or update. </param>
	/// <param name="value"> The value to associate with the key. </param>
	/// <param name="ttl"> Optional time-to-live for this specific item. Overrides default TTL if specified. </param>
	public void Set(TKey key, TValue value, TimeSpan? ttl = null)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out var existingNode))
			{
				// Update existing item
				_lruList.Remove(existingNode);
				existingNode.Value.Value = value;
				existingNode.Value.LastAccessed = DateTimeOffset.UtcNow;
				existingNode.Value.ExpiresAt = CalculateExpiration(ttl);
				_lruList.AddFirst(existingNode);
			}
			else
			{
				// Add new item
				if (_cache.Count >= Capacity)
				{
					// Evict least recently used item
					var lru = _lruList.Last;
					if (lru != null)
					{
						RemoveNode(lru);
						_ = Interlocked.Increment(ref _evictions);
						_lruEvictionCounter.Add(1);
					}
				}

				var entry = new CacheEntry
				{
					Key = key,
					Value = value,
					LastAccessed = DateTimeOffset.UtcNow,
					ExpiresAt = CalculateExpiration(ttl),
				};

				_cache[key] = _lruList.AddFirst(entry);
			}
		}
	}

	/// <summary>
	/// Gets or adds a value to the cache using the specified factory function if the key doesn't exist.
	/// </summary>
	/// <param name="key"> The key of the element to get or add. </param>
	/// <param name="valueFactory"> The function used to generate a value for the key if it doesn't exist. </param>
	/// <param name="ttl"> Optional time-to-live for the new item if created. </param>
	/// <returns> The value associated with the key, either existing or newly created. </returns>
	public TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory, TimeSpan? ttl = null)
	{
		ArgumentNullException.ThrowIfNull(valueFactory);

		// Try to get existing value first
		if (TryGetValue(key, out var existingValue) && existingValue is not null)
		{
			return existingValue;
		}

		// Create new value outside of lock if possible
		var newValue = valueFactory(key);

		lock (_lock)
		{
			// Double-check inside lock
			if (_cache.TryGetValue(key, out var node) && !IsExpired(node.Value))
			{
				// Another thread added it while we were waiting
				_lruList.Remove(node);
				_lruList.AddFirst(node);
				return node.Value.Value;
			}

			// Add the new value
			Set(key, newValue, ttl);
			return newValue;
		}
	}

	/// <summary>
	/// Removes the value with the specified key from the cache.
	/// </summary>
	/// <param name="key"> The key of the element to remove. </param>
	/// <returns> true if the element was successfully found and removed; otherwise, false. </returns>
	public bool Remove(TKey key)
	{
		lock (_lock)
		{
			if (_cache.TryGetValue(key, out var node))
			{
				RemoveNode(node);
				return true;
			}

			return false;
		}
	}

	/// <summary>
	/// Removes all items from the cache.
	/// </summary>
	public void Clear()
	{
		lock (_lock)
		{
			_cache.Clear();
			_lruList.Clear();
			_hits = 0;
			_misses = 0;
			_evictions = 0;
			_expirations = 0;
		}
	}

	/// <summary>
	/// Removes expired items from the cache. Called periodically by the cleanup timer if TTL is configured.
	/// </summary>
	public void RemoveExpiredItems()
	{
		if (!_defaultTtl.HasValue)
		{
			return;
		}

		List<LinkedListNode<CacheEntry>>? toRemove = null;

		lock (_lock)
		{
			var current = _lruList.First;
			while (current != null)
			{
				var next = current.Next;
				if (IsExpired(current.Value))
				{
					toRemove ??= [];
					toRemove.Add(current);
				}

				current = next;
			}

			if (toRemove != null)
			{
				foreach (var node in toRemove)
				{
					RemoveNode(node);
					_ = Interlocked.Increment(ref _expirations);
					_lruExpirationCounter.Add(1);
				}
			}
		}
	}

	/// <inheritdoc />
	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}

		_cleanupTimer?.Dispose();
		Clear();
		_disposed = true;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsExpired(CacheEntry entry) => entry.ExpiresAt.HasValue && DateTimeOffset.UtcNow > entry.ExpiresAt.Value;

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private DateTimeOffset? CalculateExpiration(TimeSpan? ttl)
	{
		var effectiveTtl = ttl ?? _defaultTtl;
		return effectiveTtl.HasValue ? DateTimeOffset.UtcNow.Add(effectiveTtl.Value) : null;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private void RemoveNode(LinkedListNode<CacheEntry> node)
	{
		_ = _cache.Remove(node.Value.Key);
		_lruList.Remove(node);
	}

	private sealed class CacheEntry
	{
		public required TKey Key { get; init; }

		public required TValue Value { get; set; }

		public DateTimeOffset LastAccessed { get; set; }

		public DateTimeOffset? ExpiresAt { get; set; }
	}
}
