// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Unit tests for <see cref="LruCache{TKey,TValue}"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Caching")]
public sealed class LruCacheShould : IDisposable
{
	private LruCache<string, int>? _cache;

	public void Dispose()
	{
		_cache?.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void ThrowArgumentOutOfRangeException_WhenCapacityIsZero()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
	}

	[Fact]
	public void ThrowArgumentOutOfRangeException_WhenCapacityIsNegative()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
	}

	[Fact]
	public void CreateCache_WithValidCapacity()
	{
		// Act
		_cache = new LruCache<string, int>(100);

		// Assert
		_cache.ShouldNotBeNull();
		_cache.Capacity.ShouldBe(100);
		_cache.Count.ShouldBe(0);
	}

	[Fact]
	public void CreateCache_WithTtl()
	{
		// Act
		_cache = new LruCache<string, int>(100, TimeSpan.FromMinutes(5));

		// Assert
		_cache.ShouldNotBeNull();
		_cache.Capacity.ShouldBe(100);
	}

	[Fact]
	public void CreateCache_WithTtlAndCleanupInterval()
	{
		// Act
		_cache = new LruCache<string, int>(100, TimeSpan.FromMinutes(5), TimeSpan.FromSeconds(30));

		// Assert
		_cache.ShouldNotBeNull();
	}

	#endregion

	#region Set and TryGetValue Tests

	[Fact]
	public void SetAndRetrieveValue()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act
		_cache.Set("key1", 42);
		var found = _cache.TryGetValue("key1", out var value);

		// Assert
		found.ShouldBeTrue();
		value.ShouldBe(42);
	}

	[Fact]
	public void ReturnFalse_WhenKeyNotFound()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act
		var found = _cache.TryGetValue("nonexistent", out var value);

		// Assert
		found.ShouldBeFalse();
		value.ShouldBe(default);
	}

	[Fact]
	public void UpdateExistingKey()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 42);

		// Act
		_cache.Set("key1", 100);
		_cache.TryGetValue("key1", out var value);

		// Assert
		value.ShouldBe(100);
		_cache.Count.ShouldBe(1);
	}

	[Fact]
	public void EvictLeastRecentlyUsed_WhenCapacityExceeded()
	{
		// Arrange
		_cache = new LruCache<string, int>(3);
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);
		_cache.Set("key3", 3);

		// Act - Add fourth item, should evict key1
		_cache.Set("key4", 4);

		// Assert
		_cache.Count.ShouldBe(3);
		_cache.TryGetValue("key1", out _).ShouldBeFalse(); // Evicted
		_cache.TryGetValue("key2", out _).ShouldBeTrue();
		_cache.TryGetValue("key3", out _).ShouldBeTrue();
		_cache.TryGetValue("key4", out _).ShouldBeTrue();
	}

	[Fact]
	public void MoveAccessedItemToFront()
	{
		// Arrange
		_cache = new LruCache<string, int>(3);
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);
		_cache.Set("key3", 3);

		// Act - Access key1, making it most recently used
		_cache.TryGetValue("key1", out _);
		// Add key4, should evict key2 (now least recently used)
		_cache.Set("key4", 4);

		// Assert
		_cache.TryGetValue("key1", out _).ShouldBeTrue(); // Not evicted
		_cache.TryGetValue("key2", out _).ShouldBeFalse(); // Evicted
		_cache.TryGetValue("key3", out _).ShouldBeTrue();
		_cache.TryGetValue("key4", out _).ShouldBeTrue();
	}

	#endregion

	#region GetOrAdd Tests

	[Fact]
	public void GetOrAdd_ReturnsExistingValue()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 42);

		// Act
		var value = _cache.GetOrAdd("key1", _ => 100);

		// Assert
		value.ShouldBe(42);
	}

	[Fact]
	public void GetOrAdd_CreatesNewValue_WhenKeyNotFound()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act
		var value = _cache.GetOrAdd("key1", _ => 42);

		// Assert
		value.ShouldBe(42);
		_cache.TryGetValue("key1", out var cached);
		cached.ShouldBe(42);
	}

	[Fact]
	public void GetOrAdd_ThrowsArgumentNullException_WhenFactoryIsNull()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => _cache.GetOrAdd("key1", null!));
	}

	#endregion

	#region Remove Tests

	[Fact]
	public void Remove_ReturnsTrue_WhenKeyExists()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 42);

		// Act
		var removed = _cache.Remove("key1");

		// Assert
		removed.ShouldBeTrue();
		_cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Remove_ReturnsFalse_WhenKeyNotFound()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act
		var removed = _cache.Remove("nonexistent");

		// Assert
		removed.ShouldBeFalse();
	}

	#endregion

	#region Clear Tests

	[Fact]
	public void Clear_RemovesAllItems()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);
		_cache.Set("key3", 3);

		// Act
		_cache.Clear();

		// Assert
		_cache.Count.ShouldBe(0);
		_cache.TryGetValue("key1", out _).ShouldBeFalse();
	}

	[Fact]
	public void Clear_ResetsStatistics()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 1);
		_cache.TryGetValue("key1", out _); // Hit
		_cache.TryGetValue("missing", out _); // Miss

		// Act
		_cache.Clear();
		var stats = _cache.Statistics;

		// Assert
		stats.Hits.ShouldBe(0);
		stats.Misses.ShouldBe(0);
		stats.Evictions.ShouldBe(0);
	}

	#endregion

	#region Statistics Tests

	[Fact]
	public void Statistics_TracksHits()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 1);

		// Act
		_cache.TryGetValue("key1", out _);
		_cache.TryGetValue("key1", out _);
		_cache.TryGetValue("key1", out _);

		// Assert
		_cache.Statistics.Hits.ShouldBe(3);
	}

	[Fact]
	public void Statistics_TracksMisses()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act
		_cache.TryGetValue("missing1", out _);
		_cache.TryGetValue("missing2", out _);

		// Assert
		_cache.Statistics.Misses.ShouldBe(2);
	}

	[Fact]
	public void Statistics_TracksEvictions()
	{
		// Arrange
		_cache = new LruCache<string, int>(2);
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);

		// Act - This should cause an eviction
		_cache.Set("key3", 3);

		// Assert
		_cache.Statistics.Evictions.ShouldBe(1);
	}

	[Fact]
	public void Statistics_ReturnsCorrectCurrentSize()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);
		_cache.Set("key3", 3);

		// Assert
		_cache.Statistics.CurrentSize.ShouldBe(3);
		_cache.Statistics.MaxSize.ShouldBe(10);
	}

	#endregion

	#region TTL Tests

	[Fact]
	public void ExpiredItem_ReturnsAsMiss()
	{
		// Arrange - Using a very short TTL
		_cache = new LruCache<string, int>(10, TimeSpan.FromMilliseconds(1));
		_cache.Set("key1", 42);

		// Act - Wait for expiration
		WaitForAtLeast(TimeSpan.FromMilliseconds(50));
		var found = _cache.TryGetValue("key1", out _);

		// Assert
		found.ShouldBeFalse();
	}

	[Fact]
	public void RemoveExpiredItems_CleansExpiredEntries()
	{
		// Arrange
		_cache = new LruCache<string, int>(10, TimeSpan.FromMilliseconds(1));
		_cache.Set("key1", 1);
		_cache.Set("key2", 2);

		// Act
		WaitForAtLeast(TimeSpan.FromMilliseconds(50));
		_cache.RemoveExpiredItems();

		// Assert
		_cache.Count.ShouldBe(0);
	}

	[Fact]
	public void SetWithCustomTtl_OverridesDefaultTtl()
	{
		// Arrange
		_cache = new LruCache<string, int>(10, TimeSpan.FromMinutes(5));
		_cache.Set("key1", 42, TimeSpan.FromMilliseconds(1));

		// Act
		WaitForAtLeast(TimeSpan.FromMilliseconds(50));
		var found = _cache.TryGetValue("key1", out _);

		// Assert
		found.ShouldBeFalse();
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		_cache = new LruCache<string, int>(10);

		// Act & Assert - Should not throw
		_cache.Dispose();
		_cache.Dispose();
		_cache.Dispose();

		// Null out so Dispose in test cleanup doesn't try again
		_cache = null;
	}

	[Fact]
	public void Dispose_ClearsCache()
	{
		// Arrange
		var cache = new LruCache<string, int>(10);
		cache.Set("key1", 1);
		cache.Set("key2", 2);

		// Act
		cache.Dispose();

		// Assert
		cache.Count.ShouldBe(0);
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public async Task IsThreadSafe_ForConcurrentAccess()
	{
		// Arrange
		_cache = new LruCache<string, int>(1000);
		var tasks = new List<Task>();

		// Act
		for (int i = 0; i < 10; i++)
		{
			var index = i;
			tasks.Add(Task.Run(() =>
			{
				for (int j = 0; j < 100; j++)
				{
					_cache.Set($"key{index}-{j}", index * 100 + j);
					_cache.TryGetValue($"key{index}-{j}", out _);
				}
			}));
		}

		await Task.WhenAll(tasks);

		// Assert - Should not throw and cache should have items
		_cache.Count.ShouldBeGreaterThan(0);
	}

	#endregion

	private static void WaitForAtLeast(TimeSpan duration)
	{
		var deadline = DateTime.UtcNow + duration;
		SpinWait.SpinUntil(() => DateTime.UtcNow >= deadline, TimeSpan.FromSeconds(1))
			.ShouldBeTrue();
	}
}
