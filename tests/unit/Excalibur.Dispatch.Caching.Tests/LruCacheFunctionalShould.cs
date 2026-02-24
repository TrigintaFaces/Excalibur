// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

namespace Excalibur.Dispatch.Caching.Tests;

/// <summary>
/// Functional tests for <see cref="LruCache{TKey,TValue}"/> verifying
/// eviction, TTL, statistics, and thread safety.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LruCacheFunctionalShould
{
	[Fact]
	public void Store_and_retrieve_values()
	{
		using var cache = new LruCache<string, int>(10);

		cache.Set("a", 1);
		cache.Set("b", 2);

		cache.TryGetValue("a", out var val1).ShouldBeTrue();
		val1.ShouldBe(1);

		cache.TryGetValue("b", out var val2).ShouldBeTrue();
		val2.ShouldBe(2);
	}

	[Fact]
	public void Return_false_for_nonexistent_key()
	{
		using var cache = new LruCache<string, int>(5);

		cache.TryGetValue("missing", out _).ShouldBeFalse();
	}

	[Fact]
	public void Evict_least_recently_used_when_at_capacity()
	{
		using var cache = new LruCache<string, int>(3);

		cache.Set("a", 1);
		cache.Set("b", 2);
		cache.Set("c", 3);

		// Cache is at capacity; adding "d" should evict "a" (LRU)
		cache.Set("d", 4);

		cache.TryGetValue("a", out _).ShouldBeFalse();
		cache.TryGetValue("b", out _).ShouldBeTrue();
		cache.TryGetValue("c", out _).ShouldBeTrue();
		cache.TryGetValue("d", out _).ShouldBeTrue();
	}

	[Fact]
	public void Promote_accessed_items_to_most_recent()
	{
		using var cache = new LruCache<string, int>(3);

		cache.Set("a", 1);
		cache.Set("b", 2);
		cache.Set("c", 3);

		// Access "a" to promote it
		cache.TryGetValue("a", out _);

		// Now add "d" - should evict "b" (now the LRU since "a" was accessed)
		cache.Set("d", 4);

		cache.TryGetValue("a", out _).ShouldBeTrue(); // Was promoted
		cache.TryGetValue("b", out _).ShouldBeFalse(); // Was LRU, evicted
		cache.TryGetValue("c", out _).ShouldBeTrue();
		cache.TryGetValue("d", out _).ShouldBeTrue();
	}

	[Fact]
	public void Update_existing_key_value()
	{
		using var cache = new LruCache<string, int>(5);

		cache.Set("key", 100);
		cache.Set("key", 200);

		cache.TryGetValue("key", out var val).ShouldBeTrue();
		val.ShouldBe(200);
		cache.Count.ShouldBe(1);
	}

	[Fact]
	public void Remove_key_returns_true_when_found()
	{
		using var cache = new LruCache<string, int>(5);
		cache.Set("x", 42);

		cache.Remove("x").ShouldBeTrue();
		cache.TryGetValue("x", out _).ShouldBeFalse();
		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Remove_returns_false_when_not_found()
	{
		using var cache = new LruCache<string, int>(5);

		cache.Remove("nonexistent").ShouldBeFalse();
	}

	[Fact]
	public void Clear_removes_all_entries()
	{
		using var cache = new LruCache<string, int>(10);

		for (var i = 0; i < 5; i++)
		{
			cache.Set($"key-{i}", i);
		}

		cache.Clear();

		cache.Count.ShouldBe(0);
		cache.TryGetValue("key-0", out _).ShouldBeFalse();
	}

	[Fact]
	public void Track_statistics_accurately()
	{
		using var cache = new LruCache<string, int>(5);

		cache.Set("a", 1);
		cache.Set("b", 2);

		// 2 hits
		cache.TryGetValue("a", out _);
		cache.TryGetValue("b", out _);

		// 1 miss
		cache.TryGetValue("c", out _);

		var stats = cache.Statistics;
		stats.Hits.ShouldBe(2);
		stats.Misses.ShouldBe(1);
		stats.CurrentSize.ShouldBe(2);
		stats.MaxSize.ShouldBe(5);
	}

	[Fact]
	public void Track_eviction_count_in_statistics()
	{
		using var cache = new LruCache<string, int>(2);

		cache.Set("a", 1);
		cache.Set("b", 2);
		cache.Set("c", 3); // Evicts "a"
		cache.Set("d", 4); // Evicts "b"

		var stats = cache.Statistics;
		stats.Evictions.ShouldBe(2);
	}

	[Fact]
	public void Get_or_add_uses_factory_for_missing_key()
	{
		using var cache = new LruCache<string, int>(5);
		var factoryCalled = false;

		var result = cache.GetOrAdd("new-key", _ =>
		{
			factoryCalled = true;
			return 42;
		});

		result.ShouldBe(42);
		factoryCalled.ShouldBeTrue();
	}

	[Fact]
	public void Get_or_add_returns_existing_without_factory()
	{
		using var cache = new LruCache<string, int>(5);
		cache.Set("existing", 99);
		var factoryCalled = false;

		var result = cache.GetOrAdd("existing", _ =>
		{
			factoryCalled = true;
			return 0;
		});

		result.ShouldBe(99);
		factoryCalled.ShouldBeFalse();
	}

	[Fact]
	public void Expire_items_with_ttl()
	{
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(50));

		cache.Set("expires-soon", 1);
		cache.TryGetValue("expires-soon", out _).ShouldBeTrue();

		// Wait for TTL to expire
		global::Tests.Shared.Infrastructure.TestTiming.Sleep(100);

		cache.TryGetValue("expires-soon", out _).ShouldBeFalse();
	}

	[Fact]
	public void Support_per_item_ttl_override()
	{
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromSeconds(60));

		// This item has a very short TTL override
		cache.Set("short-lived", 1, ttl: TimeSpan.FromMilliseconds(50));

		// This item uses the default (60s) TTL
		cache.Set("long-lived", 2);

		global::Tests.Shared.Infrastructure.TestTiming.Sleep(100);

		cache.TryGetValue("short-lived", out _).ShouldBeFalse();
		cache.TryGetValue("long-lived", out _).ShouldBeTrue();
	}

	[Fact]
	public void Handle_concurrent_access()
	{
		using var cache = new LruCache<int, int>(100);

		Parallel.For(0, 1000, i =>
		{
			cache.Set(i % 200, i);
			cache.TryGetValue(i % 200, out _);
		});

		// Should not throw and count should be reasonable
		cache.Count.ShouldBeLessThanOrEqualTo(100); // Capacity
		cache.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Throw_for_invalid_capacity()
	{
		Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(0));
		Should.Throw<ArgumentOutOfRangeException>(() => new LruCache<string, int>(-1));
	}

	[Fact]
	public void Report_correct_capacity()
	{
		using var cache = new LruCache<string, int>(42);

		cache.Capacity.ShouldBe(42);
	}

	[Fact]
	public void Remove_expired_items_on_cleanup()
	{
		using var cache = new LruCache<string, int>(10, defaultTtl: TimeSpan.FromMilliseconds(50));

		cache.Set("item1", 1);
		cache.Set("item2", 2);
		cache.Set("item3", 3);

		global::Tests.Shared.Infrastructure.TestTiming.Sleep(100);

		cache.RemoveExpiredItems();

		cache.Count.ShouldBe(0);
	}

	[Fact]
	public void Throw_for_null_factory_in_get_or_add()
	{
		using var cache = new LruCache<string, int>(5);

		Should.Throw<ArgumentNullException>(() =>
			cache.GetOrAdd("key", null!));
	}

	[Fact]
	public void Handle_dispose_idempotently()
	{
		var cache = new LruCache<string, int>(5);
		cache.Set("a", 1);

		cache.Dispose();
		cache.Dispose(); // Should not throw
	}
}
