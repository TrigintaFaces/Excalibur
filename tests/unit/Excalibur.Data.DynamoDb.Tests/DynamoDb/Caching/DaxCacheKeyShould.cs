// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Data.DynamoDb.Caching;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Data.Tests.DynamoDb.Caching;

/// <summary>
/// Regression lock (SAFETY-CRITICAL, wrong item served) for the DAX cache key.
/// </summary>
/// <remarks>
/// <para>
/// <b>The invariant:</b> two different DynamoDB items never share a cache entry.
/// </para>
/// <para>
/// <b>The defect this locks:</b> the cache key was a bare join —
/// <c>tableName + ":" + partitionKey</c>, plus <c>":" + sortKey</c> when present — over terms that are
/// arbitrary strings. DynamoDB key values are of type S and may contain ':' freely, so a partition key
/// carrying the separator shifts across the boundary and two distinct items render one key.
/// </para>
/// <code>
/// pk "a:b"  sk "c"     ->  T:a:b:c
/// pk "a"    sk "b:c"   ->  T:a:b:c     IDENTICAL
/// </code>
/// <para>
/// Both are ordinary items in the SAME table — a table either has a sort key or it does not, and this
/// pair has one either way — so this needs no unusual schema, only a ':' in a key value. The consequence
/// is a read returning another item's cached value under a key that was never written, which is a wrong
/// answer rather than a miss.
/// </para>
/// <para>
/// The fix reuses <see cref="Excalibur.Dispatch.SegmentedKey"/>, the framework's existing injective
/// composer, rather than inventing a second encoding for this one cache.
/// </para>
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "DynamoDb")]
public sealed class DaxCacheKeyShould
{
	private sealed record Item(string Value);

	/// <summary>
	/// THE ARM THAT MATTERS. Two distinct items whose key terms straddle the separator must not share an
	/// entry — a read for one must not be answered with the other.
	/// </summary>
	[Fact]
	public async Task NotServeOneItemUnderAnotherItemsKey_WhenAKeyValueContainsTheSeparator()
	{
		var cache = CreateCache();

		await cache.PutItemAsync("orders", "a:b", "c", new Item("belongs-to-pk-a-colon-b"), CancellationToken.None);

		var other = await cache.GetItemAsync<Item>("orders", "a", "b:c", CancellationToken.None);

		other.ShouldBeNull(
			"pk 'a:b' + sk 'c' and pk 'a' + sk 'b:c' are two different items in one table; a bare join "
			+ "renders both as 'orders:a:b:c', so the second read is answered with the first item's value "
			+ "— a wrong answer, not a cache miss, and nothing surfaces it.");
	}

	/// <summary>
	/// LIVENESS counterweight. An ordinary item still round-trips, so the arm above cannot be satisfied
	/// by a cache that has simply stopped storing anything.
	/// </summary>
	[Fact]
	public async Task StillServeAnItemUnderItsOwnKey()
	{
		var cache = CreateCache();

		await cache.PutItemAsync("orders", "a:b", "c", new Item("mine"), CancellationToken.None);

		var mine = await cache.GetItemAsync<Item>("orders", "a:b", "c", CancellationToken.None);

		mine.ShouldNotBeNull("the cache must still answer a read for the key that was written.");
		mine.Value.ShouldBe("mine");
	}

	/// <summary>
	/// PRECISION. The sort-key-absent and sort-key-present forms address different items.
	/// </summary>
	/// <remarks>
	/// The key was built with a variable number of segments, so a two-term key and a three-term key could
	/// render the same string. Arity must not be something a key value can change.
	/// </remarks>
	[Fact]
	public async Task DistinguishAnAbsentSortKeyFromOneFoldedIntoThePartitionKey()
	{
		var cache = CreateCache();

		await cache.PutItemAsync("orders", "a:b", null, new Item("no-sort-key"), CancellationToken.None);

		var withSortKey = await cache.GetItemAsync<Item>("orders", "a", "b", CancellationToken.None);

		withSortKey.ShouldBeNull(
			"an item with no sort key and an item whose sort key is 'b' are different items; the "
			+ "variable-arity join let a ':' in the partition key manufacture a sort-key boundary.");
	}

	/// <summary>
	/// SAFETY. Invalidation must remove the entry it names and no other.
	/// </summary>
	[Fact]
	public async Task InvalidateOnlyTheItemItNames()
	{
		var cache = CreateCache();

		await cache.PutItemAsync("orders", "a:b", "c", new Item("keep-me"), CancellationToken.None);

		await cache.InvalidateAsync("orders", "a", "b:c", CancellationToken.None);

		var survivor = await cache.GetItemAsync<Item>("orders", "a:b", "c", CancellationToken.None);

		survivor.ShouldNotBeNull(
			"invalidating a different item evicted this one, because both rendered the same key. A cache "
			+ "that drops entries it was not asked to drop is the same defect pointed the other way.");
	}

	private static InMemoryDaxCacheProvider CreateCache() => new(
		Microsoft.Extensions.Options.Options.Create(new DaxCacheOptions
		{
			CacheItemTtl = TimeSpan.FromMinutes(5),
		}),
		NullLogger<InMemoryDaxCacheProvider>.Instance);
}
