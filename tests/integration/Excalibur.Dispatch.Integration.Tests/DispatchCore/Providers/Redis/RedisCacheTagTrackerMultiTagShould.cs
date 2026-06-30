// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using StackExchange.Redis;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// sqrp8u augmentation (independent Tests review of FrontendDeveloper's <see cref="RedisCacheTagTrackerConcurrencyShould"/>,
/// per the PM P2 middle-path ruling 17576 — "trace the seam, confirm RED-on-pre-fix, add any missed case").
/// The sibling lock covers single-tag concurrency; this adds the uncovered <b>multi-tag</b> path:
/// <see cref="RedisCacheTagTracker.RegisterKeyAsync"/> takes a tag <b>array</b> and atomically SADDs the key
/// into EACH tag's set, and <see cref="RedisCacheTagTracker.GetKeysByTagsAsync"/> returns the de-duplicated
/// UNION across tags.
/// </summary>
/// <remarks>
/// <para>
/// <b>Atomicity-relevant + multi-tag coverage:</b> N keys each register under the SAME TWO tags concurrently.
/// Every tag set must contain all N keys — the same atomic-SADD invariant the sibling proves, exercised across
/// the per-tag loop, so it is equally RED on the coarse-blob read-modify-write substrate (which loses keys).
/// </para>
/// <para>Real Redis, never skipped (<c>verify-against-real-infra-not-mock</c>); serial (-m:1); GUID isolation.</para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Database", "Redis")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Caching")]
public sealed class RedisCacheTagTrackerMultiTagShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisCacheTagTrackerMultiTagShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task RetainEveryKey_InEachTagSet_AndUnionAcrossTags_WhenRegisteredUnderMultipleTagsConcurrently()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis multi-tag atomicity lock must never be skipped (verify-against-real-infra-not-mock)");

		using var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		var tracker = new RedisCacheTagTracker(connection, MsOptions.Create(new CacheOptions()));

		var tagX = $"tag-x-{Guid.NewGuid():N}";
		var tagY = $"tag-y-{Guid.NewGuid():N}";
		const int concurrency = 30;
		var keys = Enumerable.Range(0, concurrency)
			.Select(i => $"key-{Guid.NewGuid():N}-{i}")
			.ToArray();

		// Act — register every key under BOTH tags concurrently (exercises the per-tag atomic-SADD loop under contention).
		await Task.WhenAll(keys.Select(k =>
			Task.Run(() => tracker.RegisterKeyAsync(k, [tagX, tagY], TestCancellationToken), TestCancellationToken)));

		// Assert — each tag set independently retains every key (atomic SADD per tag, no lost update).
		var underX = await tracker.GetKeysByTagsAsync([tagX], TestCancellationToken);
		var underY = await tracker.GetKeysByTagsAsync([tagY], TestCancellationToken);
		underX.Count.ShouldBe(concurrency, "tag X must retain every concurrently-registered key");
		underY.Count.ShouldBe(concurrency, "tag Y must retain every concurrently-registered key");

		// Assert — querying both tags returns the de-duplicated union (each key is in both sets, counted once).
		var union = await tracker.GetKeysByTagsAsync([tagX, tagY], TestCancellationToken);
		union.Count.ShouldBe(concurrency, "GetKeysByTagsAsync over multiple tags must return the de-duplicated union");
		foreach (var k in keys)
		{
			union.ShouldContain(k);
		}
	}
}
