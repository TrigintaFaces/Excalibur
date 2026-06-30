// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using StackExchange.Redis;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.DispatchCore.Providers.Redis;

/// <summary>
/// sqrp8u real-infra regression lock (SA RULING msg 17397 / PM scope ruling 17396): the Redis-native
/// <see cref="RedisCacheTagTracker"/> must register concurrent keys under one tag <b>atomically</b> so no key
/// is lost — the multi-instance production scenario where the generic <see cref="DistributedCacheTagTracker"/>'s
/// read-modify-write drops keys (last-writer-wins) and silently serves stale data after tag invalidation.
/// </summary>
/// <remarks>
/// <para>
/// <b>Real infrastructure, never skipped</b> (<c>verify-against-real-infra-not-mock</c>): a mocked
/// <c>IDistributedCache</c> cannot reproduce the lost-update race, so this runs against a real Redis via
/// TestContainers and asserts <see cref="Tests.Shared.Fixtures.ContainerFixtureBase.DockerAvailable"/> rather
/// than skip-gating — Docker-down makes the lock RED, not silently green.
/// </para>
/// <para>
/// <b>Non-vacuity (RED on the pre-fix substrate):</b> proven by FrontendDeveloper via a cp-backup mutate of
/// <c>RedisCacheTagTracker.RegisterKeyAsync</c> to a non-atomic SMEMBERS→DEL→SADD-each read-modify-write — the
/// 50-way concurrent registration then drops keys and <c>RetainAll…</c> goes RED (observed count &lt; 50);
/// restored byte-identical. Authored author=impl at PM direction (msg 17566); Tests review/augment requested
/// (msg 17570).
/// </para>
/// <para>Serial (-m:1, real Redis). Per-test isolation via a unique tag + key GUIDs.</para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Redis)]
[Trait("Database", "Redis")]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Caching")]
public sealed class RedisCacheTagTrackerConcurrencyShould : IntegrationTestBase
{
	private readonly RedisContainerFixture _redisFixture;

	public RedisCacheTagTrackerConcurrencyShould(RedisContainerFixture redisFixture)
	{
		_redisFixture = redisFixture;
	}

	[Fact]
	public async Task RetainAllConcurrentlyRegisteredKeysUnderOneTag()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis atomicity lock must never be skipped (verify-against-real-infra-not-mock)");

		// Arrange — one tracker, one shared tag; N distinct keys race to register under it.
		using var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		var tracker = new RedisCacheTagTracker(connection, MsOptions.Create(new CacheOptions()));

		var tag = $"tag-{Guid.NewGuid():N}";
		const int concurrency = 50;
		var keys = Enumerable.Range(0, concurrency)
			.Select(i => $"key-{Guid.NewGuid():N}-{i}")
			.ToArray();

		// Act — register all N DISTINCT keys under the SAME tag concurrently (the lost-update race window).
		await Task.WhenAll(keys.Select(k =>
			Task.Run(() => tracker.RegisterKeyAsync(k, [tag], TestCancellationToken), TestCancellationToken)));

		// Assert — atomic SADD keeps every key, so tag-invalidation will hit all N (no silent stale entry).
		var tracked = await tracker.GetKeysByTagsAsync([tag], TestCancellationToken);
		tracked.Count.ShouldBe(concurrency, "atomic SADD must not lose any concurrently-registered key");
		foreach (var k in keys)
		{
			tracked.ShouldContain(k);
		}
	}

	[Fact]
	public async Task ReturnRegisteredKeysForTag_AndDropOnlyTheUnregisteredKey()
	{
		_redisFixture.DockerAvailable.ShouldBeTrue(
			"the real-Redis atomicity lock must never be skipped (verify-against-real-infra-not-mock)");

		// Arrange
		using var connection = ConnectionMultiplexer.Connect(_redisFixture.ConnectionString);
		var tracker = new RedisCacheTagTracker(connection, MsOptions.Create(new CacheOptions()));

		var tag = $"tag-{Guid.NewGuid():N}";
		var keyA = $"key-a-{Guid.NewGuid():N}";
		var keyB = $"key-b-{Guid.NewGuid():N}";

		await tracker.RegisterKeyAsync(keyA, [tag], TestCancellationToken);
		await tracker.RegisterKeyAsync(keyB, [tag], TestCancellationToken);

		// Both keys are resolvable for tag-invalidation.
		var both = await tracker.GetKeysByTagsAsync([tag], TestCancellationToken);
		both.ShouldContain(keyA);
		both.ShouldContain(keyB);

		// Act — unregister one key (its cache entry was removed/expired).
		await tracker.UnregisterKeyAsync(keyA, TestCancellationToken);

		// Assert — only the unregistered key drops from the tag set; the other survives (atomic SREM).
		var remaining = await tracker.GetKeysByTagsAsync([tag], TestCancellationToken);
		remaining.ShouldNotContain(keyA);
		remaining.ShouldContain(keyB);
	}
}
