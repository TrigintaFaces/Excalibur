// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

namespace Excalibur.Dispatch.Tests.Functional.Caching;

/// <summary>
/// Functional tests for caching patterns in dispatch scenarios.
/// </summary>
[Trait("Category", "Functional")]
[Trait("Component", "Caching")]
[Trait("Feature", "Patterns")]
public sealed class CachingPatternFunctionalShould : FunctionalTestBase
{
	[Fact]
	public void TrackCacheHitsAndMisses()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, string>();
		var hits = 0;
		var misses = 0;

		// Act - Simulate cache operations
		for (var i = 0; i < 10; i++)
		{
			var key = $"key{i % 3}"; // Creates 3 unique keys
			if (cache.TryGetValue(key, out _))
			{
				hits++;
			}
			else
			{
				misses++;
				cache[key] = $"value{i}";
			}
		}

		// Assert
		misses.ShouldBe(3); // First access to each of 3 keys
		hits.ShouldBe(7);   // Subsequent accesses
		cache.Count.ShouldBe(3);
	}

	[Fact]
	public void CalculateHitRatio()
	{
		// Arrange
		var totalRequests = 1000;
		var cacheHits = 850;
		var cacheMisses = totalRequests - cacheHits;

		// Act
		var hitRatio = (double)cacheHits / totalRequests;
		var missRatio = (double)cacheMisses / totalRequests;

		// Assert
		hitRatio.ShouldBe(0.85);
		missRatio.ShouldBe(0.15);
	}

	[Fact]
	public async Task CacheResultsForConfiguredTtl()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, (string value, DateTimeOffset expiry)>();
		var ttl = TimeSpan.FromMilliseconds(100);
		var key = "test-key";
		var value = "test-value";

		// Act - Cache with TTL
		var expiry = DateTimeOffset.UtcNow.Add(ttl);
		cache[key] = (value, expiry);

		// Assert - Value is cached
		cache.TryGetValue(key, out var cached).ShouldBeTrue();
		cached.value.ShouldBe(value);
		cached.expiry.ShouldBeGreaterThan(DateTimeOffset.UtcNow);

		// Wait for expiry
		await Task.Delay(ttl + TimeSpan.FromMilliseconds(50)).ConfigureAwait(false);

		// Assert - Value should be expired
		cache.TryGetValue(key, out var expiredCached).ShouldBeTrue();
		expiredCached.expiry.ShouldBeLessThan(DateTimeOffset.UtcNow);
	}

	[Fact]
	public void EvictExpiredEntries()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, (string value, DateTimeOffset expiry)>();
		var now = DateTimeOffset.UtcNow;

		cache["expired1"] = ("value1", now.AddMinutes(-5));
		cache["expired2"] = ("value2", now.AddMinutes(-1));
		cache["valid"] = ("value3", now.AddMinutes(5));

		// Act - Evict expired entries
		var expiredKeys = cache
			.Where(kvp => kvp.Value.expiry < now)
			.Select(kvp => kvp.Key)
			.ToList();

		foreach (var key in expiredKeys)
		{
			_ = cache.TryRemove(key, out _);
		}

		// Assert
		expiredKeys.Count.ShouldBe(2);
		cache.Count.ShouldBe(1);
		cache.ContainsKey("valid").ShouldBeTrue();
	}

	[Fact]
	public void ImplementCacheAsidePattern()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, string>();
		var dataStore = new Dictionary<string, string>
		{
			["key1"] = "value1",
			["key2"] = "value2",
		};
		var dataStoreReads = 0;

		string GetFromCacheAside(string key)
		{
			if (cache.TryGetValue(key, out var cachedValue))
			{
				return cachedValue;
			}

			// Cache miss - read from data store
			dataStoreReads++;
			if (dataStore.TryGetValue(key, out var value))
			{
				cache[key] = value;
				return value;
			}

			return string.Empty;
		}

		// Act - Multiple reads
		var result1 = GetFromCacheAside("key1");
		var result2 = GetFromCacheAside("key1"); // Should hit cache
		var result3 = GetFromCacheAside("key2");
		var result4 = GetFromCacheAside("key2"); // Should hit cache

		// Assert
		result1.ShouldBe("value1");
		result2.ShouldBe("value1");
		result3.ShouldBe("value2");
		result4.ShouldBe("value2");
		dataStoreReads.ShouldBe(2); // Only 2 data store reads for 4 requests
	}

	[Fact]
	public void ImplementWriteThroughPattern()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, string>();
		var dataStore = new Dictionary<string, string>();

		void WriteThrough(string key, string value)
		{
			// Write to data store first
			dataStore[key] = value;
			// Then update cache
			cache[key] = value;
		}

		// Act
		WriteThrough("key1", "value1");
		WriteThrough("key2", "value2");

		// Assert
		cache.Count.ShouldBe(2);
		dataStore.Count.ShouldBe(2);
		cache["key1"].ShouldBe("value1");
		dataStore["key1"].ShouldBe("value1");
	}

	[Fact]
	public void TrackCacheMetrics()
	{
		// Arrange
		var metrics = new TestCacheMetrics
		{
			TotalRequests = 0,
			CacheHits = 0,
			CacheMisses = 0,
			Evictions = 0,
		};

		// Act - Simulate operations
		metrics.TotalRequests = 1000;
		metrics.CacheHits = 800;
		metrics.CacheMisses = 200;
		metrics.Evictions = 50;

		// Assert
		metrics.TotalRequests.ShouldBe(1000);
		metrics.CacheHits.ShouldBe(800);
		metrics.CacheMisses.ShouldBe(200);
		metrics.Evictions.ShouldBe(50);

		// Calculate hit rate
		var hitRate = (double)metrics.CacheHits / metrics.TotalRequests;
		hitRate.ShouldBe(0.8);
	}

	[Fact]
	public void HandleConcurrentCacheAccess()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, int>();
		var taskCount = 100;

		// Act - Concurrent increments
		var tasks = Enumerable.Range(0, taskCount)
			.Select(_ => Task.Run(() =>
			{
				_ = cache.AddOrUpdate("counter", 1, (_, current) => current + 1);
			}));

		Task.WhenAll(tasks).GetAwaiter().GetResult();

		// Assert
		cache["counter"].ShouldBe(taskCount);
	}

	[Fact]
	public void ImplementLruEviction()
	{
		// Arrange
		var maxSize = 3;
		var cache = new Dictionary<string, (string value, DateTimeOffset lastAccess)>();

		void AddToCache(string key, string value)
		{
			if (cache.Count >= maxSize)
			{
				// Evict least recently used
				var lruKey = cache.MinBy(kvp => kvp.Value.lastAccess).Key;
				_ = cache.Remove(lruKey);
			}

			cache[key] = (value, DateTimeOffset.UtcNow);
		}

		// Act
		AddToCache("key1", "value1");
		AddToCache("key2", "value2");
		AddToCache("key3", "value3");

		// Access key1 to make it recently used
		cache["key1"] = ("value1", DateTimeOffset.UtcNow);

		AddToCache("key4", "value4"); // Should evict key2 (least recently used)

		// Assert
		cache.Count.ShouldBe(3);
		cache.ContainsKey("key1").ShouldBeTrue();
		cache.ContainsKey("key3").ShouldBeTrue();
		cache.ContainsKey("key4").ShouldBeTrue();
	}

	[Fact]
	public void InvalidateCacheOnUpdate()
	{
		// Arrange
		var cache = new ConcurrentDictionary<string, string>();
		var dataStore = new Dictionary<string, string>
		{
			["key1"] = "initial_value",
		};

		cache["key1"] = dataStore["key1"];

		// Act - Update data store and invalidate cache
		dataStore["key1"] = "updated_value";
		_ = cache.TryRemove("key1", out _);

		// Re-fetch from store
		var newValue = dataStore["key1"];
		cache["key1"] = newValue;

		// Assert
		cache["key1"].ShouldBe("updated_value");
	}

	private sealed class TestCacheMetrics
	{
		public int TotalRequests { get; set; }
		public int CacheHits { get; set; }
		public int CacheMisses { get; set; }
		public int Evictions { get; set; }
	}
}
