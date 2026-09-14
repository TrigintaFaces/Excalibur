// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.StackExchangeRedis;

using StackExchange.Redis;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Caching;

/// <summary>
/// Conformance tests binding <see cref="CacheTagTrackerConformanceTestKit"/> to the REAL,
/// <see langword="internal"/> <see cref="DistributedCacheTagTracker"/> against a REAL Redis-backed
/// <see cref="IDistributedCache"/> (<see cref="RedisCache"/> from
/// <c>Microsoft.Extensions.Caching.StackExchangeRedis</c>) — not an in-process
/// <c>MemoryDistributedCache</c> stand-in.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="DistributedCacheTagTracker"/> is the generic, backend-agnostic implementation of
/// <see cref="ICacheTagTracker"/> built on <see cref="IDistributedCache"/>'s Get/Set surface. A tag's
/// current state is a single opaque version stamp under one key, so resolving or invalidating a tag is
/// a single-key read or write — both atomic on every <see cref="IDistributedCache"/> backend. A real
/// distributed backend is still required to prove <see cref="RedisCache"/> actually round-trips the
/// tracker's stamp payloads end-to-end, which an in-memory stand-in cannot verify (per
/// <c>verify-against-real-infra-not-mock</c>).
/// </para>
/// <para>
/// <see cref="DistributedCacheTagTracker"/> is <c>internal sealed</c>; this test project is visible to it
/// via the <c>InternalsVisibleTo</c> already declared on <c>Excalibur.Dispatch.Caching.csproj</c> for
/// <c>Excalibur.Dispatch.Integration.Tests</c>. No production visibility was widened to write this test.
/// </para>
/// <para>
/// <b>Isolation without a fresh server per test:</b> this tracker's storage key
/// (<c>dispatch:tagver:*</c>) is fixed, not per-instance, and the Redis container is shared with every
/// other test class in <see cref="ContainerCollections.Redis"/>. <see cref="CreateTracker"/> flushes the
/// tracker's own keyspace before handing back a tracker, so each arm gets a fresh, isolated view
/// regardless of what an earlier arm left behind.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Redis)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "CACHE")]
[Trait("Database", "Redis")]
public sealed class DistributedCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit, IDisposable
{
	private readonly RedisContainerFixture _fixture;
	private readonly List<RedisCache> _redisCaches = [];

	public DistributedCacheTagTrackerConformanceTests(RedisContainerFixture fixture) => _fixture = fixture;

	// A REAL Redis backend shared by every instance CreateTracker() returns -- this is exactly the
	// property the cross-instance arms exist to prove (r7ptim). A short refresh interval keeps the
	// safety arm's convergence poll fast without weakening the single-instance arms, which never
	// depend on the refresh interval at all.
	/// <inheritdoc />
	protected override bool SupportsCrossInstanceSharing => true;

	/// <inheritdoc />
	protected override ICacheTagTracker CreateTracker()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"CacheTagTrackerConformanceTestKit arms against DistributedCacheTagTracker must run against a "
			+ "REAL Redis-backed IDistributedCache -- never skipped. "
			+ (_fixture.InitializationError ?? "Redis container required."));

		FlushTrackerKeyspace();

		var redisCache = new RedisCache(MsOptions.Create(new RedisCacheOptions
		{
			Configuration = _fixture.ConnectionString,
		}));
		_redisCaches.Add(redisCache);

		return new DistributedCacheTagTracker(
			redisCache,
			MsOptions.Create(new CacheOptions { TagStampRefreshInterval = TimeSpan.FromMilliseconds(200) }));
	}

	// Fixed key prefix ("dispatch:tagver:") -- clear it before each arm so a prior arm's fixed-literal
	// tags ("orders", "users", ...) cannot leak into the next arm.
	private void FlushTrackerKeyspace()
	{
		using var connection = ConnectionMultiplexer.Connect(_fixture.ConnectionString);
		var db = connection.GetDatabase();
		var server = connection.GetServer(connection.GetEndPoints()[0]);

		foreach (var key in server.Keys(pattern: "dispatch:tagver:*"))
		{
			_ = db.KeyDelete(key);
		}
	}

	public void Dispose()
	{
		foreach (var redisCache in _redisCaches)
		{
			redisCache.Dispose();
		}
	}

	#region Suite wiring guard

	[Fact]
	public override Task ConformanceSuite_ShouldWireEveryArm() => base.ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring guard

	#region GetOrCreateStampAsync Tests

	[Fact]
	public Task GetOrCreateStampAsync_NewTag_ShouldCreateStamp_Test() =>
		GetOrCreateStampAsync_NewTag_ShouldCreateStamp();

	[Fact]
	public Task GetOrCreateStampAsync_SameTagNoBump_ShouldReturnSameStamp_Test() =>
		GetOrCreateStampAsync_SameTagNoBump_ShouldReturnSameStamp();

	[Fact]
	public Task GetOrCreateStampAsync_DifferentTags_ShouldReturnDifferentStamps_Test() =>
		GetOrCreateStampAsync_DifferentTags_ShouldReturnDifferentStamps();

	#endregion GetOrCreateStampAsync Tests

	#region BumpStampAsync Tests

	[Fact]
	public Task BumpStampAsync_ShouldChangeStamp_Test() =>
		BumpStampAsync_ShouldChangeStamp();

	[Fact]
	public Task BumpStampAsync_NeverResolvedTag_ShouldBeSafeAndResolvable_Test() =>
		BumpStampAsync_NeverResolvedTag_ShouldBeSafeAndResolvable();

	[Fact]
	public Task BumpStampAsync_ShouldNotAffectOtherTags_Test() =>
		BumpStampAsync_ShouldNotAffectOtherTags();

	#endregion BumpStampAsync Tests

	#region Cross-Instance Sharing Tests

	[Fact]
	public Task CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance_Test() =>
		CrossInstanceBump_ShouldInvalidateEntriesOnAnotherInstance();

	[Fact]
	public Task CrossInstanceNoBump_ShouldStillHitOnAnotherInstance_Test() =>
		CrossInstanceNoBump_ShouldStillHitOnAnotherInstance();

	#endregion Cross-Instance Sharing Tests
}
