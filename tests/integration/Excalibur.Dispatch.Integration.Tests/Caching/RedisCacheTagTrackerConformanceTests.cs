// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Caching;

using Excalibur.Testing.Conformance;

using StackExchange.Redis;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Integration.Tests.Caching;

/// <summary>
/// Conformance tests binding <see cref="CacheTagTrackerConformanceTestKit"/> to the REAL,
/// <see langword="internal"/> <see cref="RedisCacheTagTracker"/> against a real Redis container (see
/// <see cref="RedisContainerFixture"/>) — not a mock.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="RedisCacheTagTracker"/> is <c>internal sealed</c>; this test project is visible to it via
/// the <c>InternalsVisibleTo</c> already declared on <c>Excalibur.Dispatch.Caching.csproj</c> for
/// <c>Excalibur.Dispatch.Integration.Tests</c>. No production visibility was widened to write this test.
/// </para>
/// <para>
/// <b>Isolation without a fresh server per test:</b> the tracker's storage keys
/// (<c>dispatch:tag:*</c> / <c>dispatch:keytags:*</c>) are NOT scoped per-instance, and the Redis
/// container is shared across every test class in <see cref="ContainerCollections.Redis"/>. Several of
/// the kit's arms use fixed literal keys/tags (e.g. <c>"user:123"</c>, <c>"users"</c>) rather than
/// GUID-generated ones, so <see cref="CreateTracker"/> flushes the tracker's own keyspace before handing
/// back a tracker, giving each arm the same "fresh instance" guarantee the kit's own documentation
/// promises for <c>InMemoryCacheTagTracker</c> — without disturbing unrelated Redis-backed fixtures
/// (Outbox/Inbox stores, etc.) that share the same container under different key prefixes.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Redis)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "CACHE")]
[Trait("Database", "Redis")]
public sealed class RedisCacheTagTrackerConformanceTests : CacheTagTrackerConformanceTestKit, IDisposable
{
	private readonly RedisContainerFixture _fixture;
	private readonly List<IConnectionMultiplexer> _connections = [];

	public RedisCacheTagTrackerConformanceTests(RedisContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override ICacheTagTracker CreateTracker()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"CacheTagTrackerConformanceTestKit arms against RedisCacheTagTracker must run against real "
			+ "Redis -- never skipped. " + (_fixture.InitializationError ?? "Redis container required."));

		var connection = ConnectionMultiplexer.Connect(_fixture.ConnectionString);
		_connections.Add(connection);

		FlushTrackerKeyspace(connection);

		return new RedisCacheTagTracker(connection, MsOptions.Create(new CacheOptions()));
	}

	// RedisCacheTagTracker's key prefixes are fixed ("dispatch:tag:", "dispatch:keytags:") and not
	// per-instance -- clear only THIS tracker's own keyspace so a prior arm's fixed-literal keys
	// ("user:123", "users", ...) cannot leak into the next arm, without touching unrelated Redis state
	// (Outbox/Inbox stores, etc.) that other test classes in this collection may have written.
	private static void FlushTrackerKeyspace(IConnectionMultiplexer connection)
	{
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
		foreach (var connection in _connections)
		{
			connection.Dispose();
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
