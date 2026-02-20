// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;
using Tests.Shared;

namespace Excalibur.Dispatch.Middleware.Tests.Caching;

/// <summary>
/// Unit tests for LruCache functionality including TTL, expiration, GetOrAdd, statistics, Clear, and Dispose.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LruCacheShould : UnitTestBase
{
	[Fact]
	public void Create_WithValidCapacity_SetsCapacityProperty()
	{
		// Arrange & Act
		using var cache = new LruCache<string, int>(100);

		// Assert
		cache.Capacity.ShouldBe(100);
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Create_WithInvalidCapacity_ThrowsArgumentOutOfRangeException()
	{
		// Arrange & Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
	}

	[Fact]
	public void Set_WithNewKey_AddsItemToCache()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act
		cache.Set("key1", 42);

		// Assert
		cache.Count.ShouldBe(1);
		cache.TryGetValue("key1", out var value).ShouldBeTrue();
		value.ShouldBe(42);
	}

	[Fact]
	public void Set_WithExistingKey_UpdatesValue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);

		// Act
		cache.Set("key1", 100);

		// Assert
		cache.Count.ShouldBe(1);
		cache.TryGetValue("key1", out var value).ShouldBeTrue();
		value.ShouldBe(100);
	}

	[Fact]
	public void Set_WhenAtCapacity_EvictsLeastRecentlyUsedItem()
	{
		// Arrange
		using var cache = new LruCache<string, int>(3);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		cache.Set("key3", 3);

		// Act - Add fourth item, should evict key1
		cache.Set("key4", 4);

		// Assert
		cache.Count.ShouldBe(3);
		cache.TryGetValue("key1", out _).ShouldBeFalse();
		cache.TryGetValue("key2", out _).ShouldBeTrue();
		cache.TryGetValue("key3", out _).ShouldBeTrue();
		cache.TryGetValue("key4", out _).ShouldBeTrue();
	}

	[Fact]
	public void TryGetValue_WhenKeyExists_ReturnsTrue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);

		// Act
		var result = cache.TryGetValue("key1", out var value);

		// Assert
		result.ShouldBeTrue();
		value.ShouldBe(42);
	}

	[Fact]
	public void TryGetValue_WhenKeyDoesNotExist_ReturnsFalse()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act
		var result = cache.TryGetValue("nonexistent", out var value);

		// Assert
		result.ShouldBeFalse();
		value.ShouldBe(default);
	}

	[Fact]
	public void TryGetValue_MovesItemToFront_PreventingEviction()
	{
		// Arrange
		using var cache = new LruCache<string, int>(3);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		cache.Set("key3", 3);

		// Act - Access key1 to make it recently used
		_ = cache.TryGetValue("key1", out _);
		// Add key4, should evict key2 (now least recently used)
		cache.Set("key4", 4);

		// Assert
		cache.TryGetValue("key1", out _).ShouldBeTrue();
		cache.TryGetValue("key2", out _).ShouldBeFalse();
		cache.TryGetValue("key3", out _).ShouldBeTrue();
		cache.TryGetValue("key4", out _).ShouldBeTrue();
	}

	[Fact]
	public void Remove_WhenKeyExists_RemovesItemAndReturnsTrue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);

		// Act
		var result = cache.Remove("key1");

		// Assert
		result.ShouldBeTrue();
		cache.Count.ShouldBe(0);
		cache.TryGetValue("key1", out _).ShouldBeFalse();
	}

	[Fact]
	public void Remove_WhenKeyDoesNotExist_ReturnsFalse()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act
		var result = cache.Remove("nonexistent");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void Clear_RemovesAllItems()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		cache.Set("key3", 3);

		// Act
		cache.Clear();

		// Assert
		cache.Count.ShouldBe(0);
		cache.TryGetValue("key1", out _).ShouldBeFalse();
		cache.TryGetValue("key2", out _).ShouldBeFalse();
		cache.TryGetValue("key3", out _).ShouldBeFalse();
	}

	[Fact]
	public void GetOrAdd_WhenKeyDoesNotExist_CreatesAndReturnsNewValue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		var factoryCalled = false;

		// Act
		var result = cache.GetOrAdd("key1", _ =>
		{
			factoryCalled = true;
			return 42;
		});

		// Assert
		result.ShouldBe(42);
		factoryCalled.ShouldBeTrue();
		cache.Count.ShouldBe(1);
	}

	[Fact]
	public void GetOrAdd_WhenKeyExists_ReturnsExistingValue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);
		var factoryCalled = false;

		// Act
		var result = cache.GetOrAdd("key1", _ =>
		{
			factoryCalled = true;
			return 100;
		});

		// Assert
		result.ShouldBe(42);
		factoryCalled.ShouldBeFalse();
	}

	[Fact]
	public void Statistics_TracksHitsAndMisses()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);

		// Act
		_ = cache.TryGetValue("key1", out _); // Hit
		_ = cache.TryGetValue("key1", out _); // Hit
		_ = cache.TryGetValue("nonexistent", out _); // Miss

		// Assert
		var stats = cache.Statistics;
		stats.Hits.ShouldBe(2);
		stats.Misses.ShouldBe(1);
	}

	[Fact]
	public void Statistics_TracksEvictions()
	{
		// Arrange
		using var cache = new LruCache<string, int>(2);
		cache.Set("key1", 1);
		cache.Set("key2", 2);

		// Act - cause eviction
		cache.Set("key3", 3);

		// Assert
		var stats = cache.Statistics;
		stats.Evictions.ShouldBe(1);
	}

	[Fact]
	public void Dispose_CanBeCalledMultipleTimes()
	{
		// Arrange
		var cache = new LruCache<string, int>(10);
		cache.Set("key1", 42);

		// Act & Assert - should not throw
		cache.Dispose();
		cache.Dispose();
	}

	[Fact]
	public void Set_WithTtl_ItemCanBeRetrievedBeforeExpiry()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, TimeSpan.FromHours(1));
		cache.Set("key1", 42, TimeSpan.FromHours(1));

		// Act
		var result = cache.TryGetValue("key1", out var value);

		// Assert
		result.ShouldBeTrue();
		value.ShouldBe(42);
	}

	// ========== NEW TESTS FOR INCREASED COVERAGE ==========

	[Fact]
	public void Constructor_WithTtl_StartsCleanupTimer()
	{
		// Arrange & Act
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMinutes(1));

		// Assert
		cache.Capacity.ShouldBe(10);
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithTtlAndCustomCleanupInterval_CreatesCache()
	{
		// Arrange & Act
		using var cache = new LruCache<string, int>(
			10,
			defaultTtl: TimeSpan.FromMinutes(5),
			cleanupInterval: TimeSpan.FromSeconds(30));

		// Assert
		cache.Capacity.ShouldBe(10);
	}

	[Fact]
	public void Set_WithTtlOverride_UsesCustomTtl()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromHours(1));

		// Act
		cache.Set("key1", 42, ttl: TimeSpan.FromMilliseconds(50));
		Thread.Sleep(100); // Wait for expiration

		// Assert
		cache.TryGetValue("key1", out var value).ShouldBeFalse();
		value.ShouldBe(default);
	}

	[Fact]
	public void TryGetValue_WhenItemExpired_ReturnsFalseAndIncrementsExpirations()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(50));
		cache.Set("key1", 42);
		Thread.Sleep(100); // Wait for expiration

		// Act
		var found = cache.TryGetValue("key1", out var value);
		var stats = cache.Statistics;

		// Assert
		found.ShouldBeFalse();
		value.ShouldBe(default);
		stats.Expirations.ShouldBeGreaterThan(0);
		stats.Misses.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void RemoveExpiredItems_RemovesExpiredEntries()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(50));
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		Thread.Sleep(100); // Wait for expiration

		// Act
		cache.RemoveExpiredItems();
		var stats = cache.Statistics;

		// Assert
		cache.Count.ShouldBe(0);
		stats.Expirations.ShouldBe(2);
	}

	[Fact]
	public void RemoveExpiredItems_WithoutTtl_DoesNothing()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 1);
		cache.Set("key2", 2);

		// Act
		cache.RemoveExpiredItems();

		// Assert
		cache.Count.ShouldBe(2);
	}

	[Fact]
	public void GetOrAdd_WithTtl_AppliesTtlToNewValue()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act
		var value = cache.GetOrAdd("key1", key => 42, ttl: TimeSpan.FromMilliseconds(50));
		Thread.Sleep(100);

		// Assert
		cache.TryGetValue("key1", out var expiredValue).ShouldBeFalse();
		expiredValue.ShouldBe(default);
	}

	[Fact]
	public void GetOrAdd_WithNullFactory_ThrowsArgumentNullException()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act & Assert
#pragma warning disable CS8625 // Cannot convert null literal to non-nullable reference type - intentional for test
		Should.Throw<ArgumentNullException>(() => cache.GetOrAdd("key1", valueFactory: null));
#pragma warning restore CS8625
	}

	[Fact]
	public void Statistics_ReturnsCorrectMetrics()
	{
		// Arrange
		using var cache = new LruCache<string, int>(3);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		_ = cache.TryGetValue("key1", out _); // Hit
		_ = cache.TryGetValue("key3", out _); // Miss

		// Act
		var stats = cache.Statistics;

		// Assert
		stats.CurrentSize.ShouldBe(2);
		stats.MaxSize.ShouldBe(3);
		stats.Hits.ShouldBe(1);
		stats.Misses.ShouldBe(1);
		stats.Evictions.ShouldBe(0);
		stats.Expirations.ShouldBe(0);
	}

	[Fact]
	public void Statistics_AfterEviction_IncrementsEvictionCount()
	{
		// Arrange
		using var cache = new LruCache<string, int>(2);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		cache.Set("key3", 3); // Evicts key1

		// Act
		var stats = cache.Statistics;

		// Assert
		stats.Evictions.ShouldBe(1);
		stats.CurrentSize.ShouldBe(2);
	}

	[Fact]
	public void Clear_RemovesAllItemsAndResetsCounters()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		_ = cache.TryGetValue("key1", out _);
		_ = cache.TryGetValue("key3", out _);

		// Act
		cache.Clear();
		var stats = cache.Statistics;

		// Assert
		cache.Count.ShouldBe(0);
		stats.Hits.ShouldBe(0);
		stats.Misses.ShouldBe(0);
		stats.Evictions.ShouldBe(0);
		stats.Expirations.ShouldBe(0);
	}

	[Fact]
	public void Dispose_StopsCleanupTimer()
	{
		// Arrange
		var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(100));
		cache.Set("key1", 1);

		// Act
		cache.Dispose();

		// Assert - Timer should be stopped, cache cleared
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Set_UpdatesExistingKey_MovesToFront()
	{
		// Arrange
		using var cache = new LruCache<string, int>(3);
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		cache.Set("key3", 3);

		// Act
		cache.Set("key1", 100); // Updates and moves to front
		cache.Set("key4", 4);  // Should evict key2 (least recently used)

		// Assert
		cache.TryGetValue("key1", out var value1).ShouldBeTrue();
		value1.ShouldBe(100);
		cache.TryGetValue("key2", out _).ShouldBeFalse(); // Evicted
		cache.TryGetValue("key3", out _).ShouldBeTrue();
		cache.TryGetValue("key4", out _).ShouldBeTrue();
	}

	[Fact]
	public void Statistics_TracksExpirations()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(50));
		cache.Set("key1", 1);
		cache.Set("key2", 2);
		Thread.Sleep(100);

		// Act
		_ = cache.TryGetValue("key1", out _); // Expired, increments expirations
		_ = cache.TryGetValue("key2", out _); // Expired, increments expirations
		var stats = cache.Statistics;

		// Assert
		stats.Expirations.ShouldBe(2);
	}

	[Fact]
	public void RemoveExpiredItems_WithMixedExpiredAndValid_RemovesOnlyExpired()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromSeconds(10));
		cache.Set("key1", 1, ttl: TimeSpan.FromMilliseconds(50));  // Will expire
		cache.Set("key2", 2, ttl: TimeSpan.FromHours(1));          // Won't expire
		Thread.Sleep(100);

		// Act
		cache.RemoveExpiredItems();

		// Assert
		cache.Count.ShouldBe(1);
		cache.TryGetValue("key1", out _).ShouldBeFalse();
		cache.TryGetValue("key2", out _).ShouldBeTrue();
	}

	[Fact]
	public void Count_ReflectsCurrentItemCount()
	{
		// Arrange
		using var cache = new LruCache<string, int>(10);

		// Act & Assert
		cache.Count.ShouldBe(0);
		cache.Set("key1", 1);
		cache.Count.ShouldBe(1);
		cache.Set("key2", 2);
		cache.Count.ShouldBe(2);
		cache.Remove("key1");
		cache.Count.ShouldBe(1);
		cache.Clear();
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Capacity_ReturnsConfiguredMaximum()
	{
		// Arrange & Act
		using var cache = new LruCache<string, int>(25);

		// Assert
		cache.Capacity.ShouldBe(25);
	}
}
