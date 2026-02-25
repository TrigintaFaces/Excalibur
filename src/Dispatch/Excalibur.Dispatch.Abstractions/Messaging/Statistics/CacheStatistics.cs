// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0


namespace Excalibur.Dispatch.Abstractions.Messaging;

/// <summary>
/// Comprehensive cache statistics for tracking cache performance and usage across the messaging framework.
/// </summary>
public sealed class CacheStatistics
{
	private long _hits;
	private long _misses;
	private long _evictions;
	private long _expirations;
	private long _itemCount;
	private long _totalSizeBytes;
	private long _totalRequests;

	/// <summary>
	/// Gets or sets the unique identifier for this cache instance (e.g., SagaId).
	/// </summary>
	/// <value>The current <see cref="CacheId"/> value.</value>
	public string? CacheId { get; set; }

	/// <summary>
	/// Gets or sets the number of cache hits.
	/// </summary>
	/// <value>
	/// The number of cache hits.
	/// </value>
	public long Hits
	{
		get => _hits;
		set => _hits = value;
	}

	/// <summary>
	/// Gets or sets the number of cache misses.
	/// </summary>
	/// <value>
	/// The number of cache misses.
	/// </value>
	public long Misses
	{
		get => _misses;
		set => _misses = value;
	}

	/// <summary>
	/// Gets or sets the total number of requests (hits + misses).
	/// </summary>
	/// <value>
	/// The total number of requests (hits + misses).
	/// </value>
	public long TotalRequests
	{
		get => _totalRequests > 0 ? _totalRequests : (_hits + _misses);
		set => _totalRequests = value;
	}

	/// <summary>
	/// Gets or sets the number of items evicted due to capacity constraints.
	/// </summary>
	/// <value>
	/// The number of items evicted due to capacity constraints.
	/// </value>
	public long Evictions
	{
		get => _evictions;
		set => _evictions = value;
	}

	/// <summary>
	/// Gets or sets the number of items that expired based on TTL.
	/// </summary>
	/// <value>
	/// The number of items that expired based on TTL.
	/// </value>
	public long Expirations
	{
		get => _expirations;
		set => _expirations = value;
	}

	/// <summary>
	/// Gets or sets the current number of items in the cache.
	/// </summary>
	/// <value>
	/// The current number of items in the cache.
	/// </value>
	public long ItemCount
	{
		get => _itemCount;
		set => _itemCount = value;
	}

	/// <summary>
	/// Gets or sets the current number of entries in the cache (alias for ItemCount).
	/// </summary>
	/// <value>
	/// The current number of entries in the cache (alias for ItemCount).
	/// </value>
	public long EntryCount
	{
		get => ItemCount;
		set => ItemCount = value;
	}

	/// <summary>
	/// Gets or sets the total size of cached items in bytes.
	/// </summary>
	/// <value>
	/// The total size of cached items in bytes.
	/// </value>
	public long TotalSizeBytes
	{
		get => _totalSizeBytes;
		set => _totalSizeBytes = value;
	}

	/// <summary>
	/// Gets or sets the total size of cached items in bytes (alias for TotalSizeBytes).
	/// </summary>
	/// <value>
	/// The total size of cached items in bytes (alias for TotalSizeBytes).
	/// </value>
	public long SizeInBytes
	{
		get => TotalSizeBytes;
		set => TotalSizeBytes = value;
	}

	/// <summary>
	/// Gets or sets the total size of cached items in bytes (alias for TotalSizeBytes).
	/// </summary>
	/// <value>
	/// The total size of cached items in bytes (alias for TotalSizeBytes).
	/// </value>
	public long TotalSize
	{
		get => TotalSizeBytes;
		set => TotalSizeBytes = value;
	}

	/// <summary>
	/// Gets or sets the total memory usage of cache in bytes (alias for TotalSizeBytes).
	/// </summary>
	/// <value>
	/// The total memory usage of cache in bytes (alias for TotalSizeBytes).
	/// </value>
	public long MemoryUsage
	{
		get => TotalSizeBytes;
		set => TotalSizeBytes = value;
	}

	/// <summary>
	/// Gets or sets the maximum capacity of the cache.
	/// </summary>
	/// <value>The current <see cref="MaxSize"/> value.</value>
	public int MaxSize { get; set; }

	/// <summary>
	/// Gets or sets the current number of items in the cache (alias for ItemCount).
	/// </summary>
	/// <value>
	/// The current number of items in the cache (alias for ItemCount).
	/// </value>
	public int CurrentSize
	{
		get => (int)ItemCount;
		set => ItemCount = value;
	}

	/// <summary>
	/// Gets or sets the current cache size (alias for ItemCount, for backward compatibility).
	/// </summary>
	/// <value>
	/// The current cache size (alias for ItemCount, for backward compatibility).
	/// </value>
	public int CacheSize
	{
		get => (int)ItemCount;
		set => ItemCount = value;
	}

	/// <summary>
	/// Gets or sets the current cache size (alias for ItemCount, for backward compatibility).
	/// </summary>
	/// <value>
	/// The current cache size (alias for ItemCount, for backward compatibility).
	/// </value>
	public long CurrentCacheSize
	{
		get => ItemCount;
		set => ItemCount = value;
	}

	/// <summary>
	/// Gets or sets the cache hit rate as a percentage (0-100).
	/// </summary>
	/// <value>
	/// The cache hit rate as a percentage (0-100).
	/// </value>
	public double HitRate
	{
		get
		{
			var total = TotalRequests;
			return total > 0 ? (double)Hits / total * 100 : 0;
		}

		set
		{
			// Setter for backward compatibility with existing code Ignored as this is a calculated property
		}
	}

	/// <summary>
	/// Gets the cache hit ratio (0-1).
	/// </summary>
	/// <value>
	/// The cache hit ratio (0-1).
	/// </value>
	public double HitRatio
	{
		get
		{
			var total = TotalRequests;
			return total > 0 ? (double)Hits / total : 0;
		}
	}

	/// <summary>
	/// Gets the cache miss rate as a percentage (0-100).
	/// </summary>
	/// <value>
	/// The cache miss rate as a percentage (0-100).
	/// </value>
	public double MissRate
	{
		get
		{
			var total = TotalRequests;
			return total > 0 ? (double)Misses / total * 100 : 0;
		}
	}

	/// <summary>
	/// Gets or sets the average access time.
	/// </summary>
	/// <value>The current <see cref="AverageAccessTime"/> value.</value>
	public TimeSpan AverageAccessTime { get; set; }

	/// <summary>
	/// Gets or sets the average access interval.
	/// </summary>
	/// <value>The current <see cref="AverageAccessInterval"/> value.</value>
	public TimeSpan AverageAccessInterval { get; set; }

	/// <summary>
	/// Gets or sets the average get time in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageGetTimeMs"/> value.</value>
	public double AverageGetTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the average set time in milliseconds.
	/// </summary>
	/// <value>The current <see cref="AverageSetTimeMs"/> value.</value>
	public double AverageSetTimeMs { get; set; }

	/// <summary>
	/// Gets or sets the last access time.
	/// </summary>
	/// <value>The current <see cref="LastAccessTime"/> value.</value>
	public DateTimeOffset LastAccessTime { get; set; }

	/// <summary>
	/// Gets or sets the last reset time.
	/// </summary>
	/// <value>The current <see cref="LastResetTime"/> value.</value>
	public DateTimeOffset LastResetTime { get; set; }

	/// <summary>
	/// Gets or sets the last reset time as DateTimeOffset.
	/// </summary>
	/// <value>The current <see cref="LastReset"/> value.</value>
	public DateTimeOffset LastReset { get; set; }

	/// <summary>
	/// Gets or sets the recommended TTL.
	/// </summary>
	/// <value>The current <see cref="RecommendedTtl"/> value.</value>
	public TimeSpan RecommendedTtl { get; set; }

	/// <summary>
	/// Gets or sets the total number of accesses (alias for TotalRequests).
	/// </summary>
	/// <value>
	/// The total number of accesses (alias for TotalRequests).
	/// </value>
	public int TotalAccesses
	{
		get => (int)TotalRequests;
		set => TotalRequests = value;
	}

	/// <summary>
	/// Gets or sets the hit count (alias for Hits).
	/// </summary>
	/// <value>
	/// The hit count (alias for Hits).
	/// </value>
	public long HitCount
	{
		get => Hits;
		set => Hits = value;
	}

	/// <summary>
	/// Gets or sets the miss count (alias for Misses).
	/// </summary>
	/// <value>
	/// The miss count (alias for Misses).
	/// </value>
	public long MissCount
	{
		get => Misses;
		set => Misses = value;
	}

	/// <summary>
	/// Gets or sets the cache hits (alias for Hits).
	/// </summary>
	/// <value>
	/// The cache hits (alias for Hits).
	/// </value>
	public long CacheHits
	{
		get => Hits;
		set => Hits = value;
	}

	/// <summary>
	/// Gets or sets the cache misses (alias for Misses).
	/// </summary>
	/// <value>
	/// The cache misses (alias for Misses).
	/// </value>
	public long CacheMisses
	{
		get => Misses;
		set => Misses = value;
	}

	/// <summary>
	/// Gets or sets the cache hit rate (alias for HitRate).
	/// </summary>
	/// <value>
	/// The cache hit rate (alias for HitRate).
	/// </value>
	public double CacheHitRate
	{
		get => HitRate;
		set => HitRate = value;
	}

	/// <summary>
	/// Gets or sets the Saga ID (specific to saga caching scenarios).
	/// </summary>
	/// <value>
	/// The Saga ID (specific to saga caching scenarios).
	/// </value>
	public string SagaId
	{
		get => CacheId ?? string.Empty;
		set => CacheId = value;
	}

	/// <summary>
	/// Gets statistics by cache level (for multi-level caches).
	/// </summary>
	/// <value>The current <see cref="LevelStatistics"/> value.</value>
	public Dictionary<string, object>? LevelStatistics { get; init; }

	/// <summary>
	/// Increments the hits counter in a thread-safe manner.
	/// </summary>
	public void IncrementHits() => Interlocked.Increment(ref _hits);

	/// <summary>
	/// Increments the misses counter in a thread-safe manner.
	/// </summary>
	public void IncrementMisses() => Interlocked.Increment(ref _misses);

	/// <summary>
	/// Increments the evictions counter in a thread-safe manner.
	/// </summary>
	public void IncrementEvictions() => Interlocked.Increment(ref _evictions);

	/// <summary>
	/// Increments the expirations counter in a thread-safe manner.
	/// </summary>
	public void IncrementExpirations() => Interlocked.Increment(ref _expirations);

	/// <summary>
	/// Increments the entry count in a thread-safe manner.
	/// </summary>
	public void IncrementEntryCount() => Interlocked.Increment(ref _itemCount);

	/// <summary>
	/// Decrements the entry count in a thread-safe manner.
	/// </summary>
	public void DecrementEntryCount() => Interlocked.Decrement(ref _itemCount);

	/// <summary>
	/// Adds to the total size in bytes in a thread-safe manner.
	/// </summary>
	/// <param name="bytes"> The number of bytes to add. </param>
	public void AddSizeBytes(long bytes) => Interlocked.Add(ref _totalSizeBytes, bytes);

	/// <summary>
	/// Subtracts from the total size in bytes in a thread-safe manner.
	/// </summary>
	/// <param name="bytes"> The number of bytes to subtract. </param>
	public void SubtractSizeBytes(long bytes) => Interlocked.Add(ref _totalSizeBytes, -bytes);

	/// <summary>
	/// Resets all statistics to zero.
	/// </summary>
	public void Reset()
	{
		_hits = 0;
		_misses = 0;
		_evictions = 0;
		_expirations = 0;
		_itemCount = 0;
		_totalSizeBytes = 0;
		_totalRequests = 0;
		AverageAccessTime = TimeSpan.Zero;
		AverageAccessInterval = TimeSpan.Zero;
		AverageGetTimeMs = 0;
		AverageSetTimeMs = 0;
		LastAccessTime = DateTimeOffset.UtcNow;
		LastResetTime = DateTimeOffset.UtcNow;
		LastReset = DateTimeOffset.UtcNow;
		RecommendedTtl = TimeSpan.Zero;
		LevelStatistics?.Clear();
	}

	/// <summary>
	/// Creates a snapshot of the current statistics.
	/// </summary>
	/// <returns> A new CacheStatistics instance with copied values. </returns>
	public CacheStatistics CreateSnapshot() =>
		new()
		{
			CacheId = CacheId,
			Hits = Hits,
			Misses = Misses,
			TotalRequests = TotalRequests,
			Evictions = Evictions,
			Expirations = Expirations,
			ItemCount = ItemCount,
			TotalSizeBytes = TotalSizeBytes,
			MaxSize = MaxSize,
			AverageAccessTime = AverageAccessTime,
			AverageAccessInterval = AverageAccessInterval,
			AverageGetTimeMs = AverageGetTimeMs,
			AverageSetTimeMs = AverageSetTimeMs,
			LastAccessTime = LastAccessTime,
			LastResetTime = LastResetTime,
			LastReset = LastReset,
			RecommendedTtl = RecommendedTtl,
			LevelStatistics = LevelStatistics != null ? new Dictionary<string, object>(LevelStatistics, StringComparer.Ordinal) : null,
		};
}
