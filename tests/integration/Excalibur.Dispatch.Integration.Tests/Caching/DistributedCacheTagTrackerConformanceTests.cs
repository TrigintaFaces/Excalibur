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
/// <see cref="ICacheTagTracker"/> built on <see cref="IDistributedCache"/>'s Get/Set/Remove surface — the
/// implementation's own remarks document that its tag-set read-modify-write is <b>not atomic</b> over
/// that abstraction (last-writer-wins under concurrent registration of different keys under the same
/// tag). The kit's arms are sequential, not concurrent, so that known limitation does not make any arm
/// here RED; a real distributed backend is still required to prove <see cref="RedisCache"/> actually
/// round-trips the tracker's JSON-serialized <c>HashSet&lt;string&gt;</c>/<c>string[]</c> payloads
/// end-to-end, which an in-memory stand-in cannot verify (per <c>verify-against-real-infra-not-mock</c>).
/// </para>
/// <para>
/// <see cref="DistributedCacheTagTracker"/> is <c>internal sealed</c>; this test project is visible to it
/// via the <c>InternalsVisibleTo</c> already declared on <c>Excalibur.Dispatch.Caching.csproj</c> for
/// <c>Excalibur.Dispatch.Integration.Tests</c>. No production visibility was widened to write this test.
/// </para>
/// <para>
/// <b>Isolation without a fresh server per test:</b> like <see cref="RedisCacheTagTracker"/>, this
/// tracker's storage keys (<c>dispatch:tag:*</c> / <c>dispatch:keytags:*</c>) are fixed, not
/// per-instance, and the Redis container is shared with every other test class in
/// <see cref="ContainerCollections.Redis"/> (including <see cref="RedisCacheTagTrackerConformanceTests"/>,
/// which targets the SAME key prefixes on the SAME shared container). <see cref="CreateTracker"/> flushes
/// the tracker's own keyspace before handing back a tracker, so each arm gets a fresh, isolated view
/// regardless of what an earlier arm — in this class or the sibling Redis-native one — left behind.
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

		return new DistributedCacheTagTracker(redisCache, MsOptions.Create(new CacheOptions()));
	}

	// Same fixed key prefixes as RedisCacheTagTracker ("dispatch:tag:", "dispatch:keytags:") -- clear
	// them before each arm so the kit's fixed-literal-key arms (e.g. "user:123") get a genuinely fresh
	// view regardless of what an earlier arm (in this class or the sibling native-Redis one) left behind.
	private void FlushTrackerKeyspace()
	{
		using var connection = ConnectionMultiplexer.Connect(_fixture.ConnectionString);
		var db = connection.GetDatabase();
		var server = connection.GetServer(connection.GetEndPoints()[0]);

		foreach (var key in server.Keys(pattern: "dispatch:tag:*"))
		{
			_ = db.KeyDelete(key);
		}

		foreach (var key in server.Keys(pattern: "dispatch:keytags:*"))
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

	#region RegisterKeyAsync Tests

	[Fact]
	public Task RegisterKeyAsync_WithTags_ShouldRegister_Test() =>
		RegisterKeyAsync_WithTags_ShouldRegister();

	[Fact]
	public Task RegisterKeyAsync_EmptyTags_ShouldBeNoOp_Test() =>
		RegisterKeyAsync_EmptyTags_ShouldBeNoOp();

	[Fact]
	public Task RegisterKeyAsync_NullTags_ShouldBeNoOp_Test() =>
		RegisterKeyAsync_NullTags_ShouldBeNoOp();

	[Fact]
	public Task RegisterKeyAsync_ReRegister_ShouldReplaceTags_Test() =>
		RegisterKeyAsync_ReRegister_ShouldReplaceTags();

	#endregion RegisterKeyAsync Tests

	#region GetKeysByTagsAsync Tests

	[Fact]
	public Task GetKeysByTagsAsync_SingleTag_ShouldReturnKeys_Test() =>
		GetKeysByTagsAsync_SingleTag_ShouldReturnKeys();

	[Fact]
	public Task GetKeysByTagsAsync_MultipleTags_ShouldReturnUnion_Test() =>
		GetKeysByTagsAsync_MultipleTags_ShouldReturnUnion();

	[Fact]
	public Task GetKeysByTagsAsync_EmptyTags_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_EmptyTags_ShouldReturnEmpty();

	[Fact]
	public Task GetKeysByTagsAsync_NullTags_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_NullTags_ShouldReturnEmpty();

	[Fact]
	public Task GetKeysByTagsAsync_NonExistentTag_ShouldReturnEmpty_Test() =>
		GetKeysByTagsAsync_NonExistentTag_ShouldReturnEmpty();

	#endregion GetKeysByTagsAsync Tests

	#region UnregisterKeyAsync Tests

	[Fact]
	public Task UnregisterKeyAsync_ShouldRemoveFromAllTags_Test() =>
		UnregisterKeyAsync_ShouldRemoveFromAllTags();

	[Fact]
	public Task UnregisterKeyAsync_NonExistentKey_ShouldBeNoOp_Test() =>
		UnregisterKeyAsync_NonExistentKey_ShouldBeNoOp();

	[Fact]
	public Task UnregisterKeyAsync_ShouldCleanupEmptyTagEntries_Test() =>
		UnregisterKeyAsync_ShouldCleanupEmptyTagEntries();

	#endregion UnregisterKeyAsync Tests

	#region Edge Case Tests

	[Fact]
	public Task RegisterKeyAsync_MultipleTags_ShouldBeFoundInAll_Test() =>
		RegisterKeyAsync_MultipleTags_ShouldBeFoundInAll();

	#endregion Edge Case Tests
}
