// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Tests.Conformance.Snapshot;

using Excalibur.Dispatch;

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using Shouldly;

using StackExchange.Redis;

#pragma warning disable CA1812 // Internal class is never instantiated

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Real-infrastructure conformance tests for <see cref="RedisSnapshotStore"/> using the
/// Snapshot Conformance Test Kit against a live Redis container.
/// </summary>
/// <remarks>
/// These tests verify that the Redis implementation correctly implements the
/// <see cref="ISnapshotStore"/> contract using TestContainers. They are never skipped:
/// when Docker is unavailable the fixture fails fast, so a missing container surfaces as a
/// failure rather than a silent pass. The store binds the default
/// <see cref="ConnectionMultiplexer"/> client surface a consumer would use.
/// </remarks>
[Collection(RedisSnapshotStoreTestCollection.CollectionName)]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Redis")]
public sealed class RedisSnapshotStoreConformanceShould : SnapshotConformanceTestBase, IClassFixture<RedisSnapshotStoreContainerFixture>
{
	private readonly RedisSnapshotStoreContainerFixture _fixture;
	private ConnectionMultiplexer? _storeConnection;

	/// <summary>
	/// Initializes a new instance of the <see cref="RedisSnapshotStoreConformanceShould"/> class.
	/// </summary>
	/// <param name="fixture">The Redis container fixture.</param>
	public RedisSnapshotStoreConformanceShould(RedisSnapshotStoreContainerFixture fixture)
	{
		_fixture = fixture;
	}

	/// <inheritdoc/>
	protected override async Task<ISnapshotStore> CreateSnapshotStoreAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Redis container must be available - real-infra conformance is never skipped.");

		// Bind the DEFAULT client surface (no admin, default options) the way a consumer would.
		_storeConnection = await ConnectionMultiplexer.ConnectAsync(_fixture.ConnectionString).ConfigureAwait(false);

		var options = Options.Create(new RedisSnapshotStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
		});

		var logger = NullLogger<RedisSnapshotStore>.Instance;

		// Ambient context, not the default null: the tenant-isolation arms use
		// TenantContextHolder.BeginScope, and CurrentTenantScope collapses every tenant to the
		// untenanted sentinel so they overwrite each other's snapshots.
		return new RedisSnapshotStore(_storeConnection, options, logger, new AmbientTenantContext());
	}

	/// <inheritdoc/>
	protected override async Task DisposeSnapshotStoreAsync()
	{
		if (_storeConnection is not null)
		{
			await _storeConnection.DisposeAsync().ConfigureAwait(false);
			_storeConnection = null;
		}

		await _fixture.CleanupAsync().ConfigureAwait(false);
	}

	/// <summary>
	/// A save carrying an OLDER version must not replace a newer stored snapshot.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The write was an unconditional <c>HSET</c>, so it was last-writer-wins by arrival: whichever save
	/// reached Redis last won, regardless of the version it carried. A delayed or concurrent save at an
	/// older version replaced a newer snapshot, and <c>GetLatestSnapshotAsync</c> then returned a
	/// snapshot that was not the latest - silently, because nothing about that path reports an error.
	/// </para>
	/// <para>
	/// This arm removes the scheduler from the question rather than racing saves: save 100, then save 80,
	/// then read. The kit's concurrency arm can only catch the defect when the scheduler happens to land
	/// the older write last, so it passes on most runs; this one fails on every run against an unguarded
	/// store.
	/// </para>
	/// <para>
	/// It asserts the payload as well as the version because a fix that guarded the version field while
	/// still writing the other fields would satisfy a version-only assertion and hand back a snapshot
	/// whose body and version disagree.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NeverLetAnOlderSaveReplaceANewerSnapshot()
	{
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "MonotonicAggregate";

		var newer = CreateTestSnapshot(aggregateId, aggregateType, 100, [100]);
		await SnapshotStore!.SaveSnapshotAsync(newer, TestContext.Current.CancellationToken);

		var stale = CreateTestSnapshot(aggregateId, aggregateType, 80, [80]);
		await SnapshotStore.SaveSnapshotAsync(stale, TestContext.Current.CancellationToken);

		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			TestContext.Current.CancellationToken);

		_ = retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(
			100,
			"a stale save must not move the stored snapshot backwards - GetLatestSnapshotAsync would then "
			+ "return a snapshot that is not the latest, which is the contract this store advertises");

		retrieved.Data.ToArray().ShouldBe(new byte[] { 100 });
	}

	/// <summary>
	/// A save carrying a NEWER version must still be accepted, payload and all.
	/// </summary>
	/// <remarks>
	/// The liveness half of the guard above, and it is not redundant: a store that refused every save
	/// after the first would satisfy the safety arm perfectly while being useless, and a store that
	/// compared versions the wrong way round would pass it too. This arm pins the direction. It advances
	/// the version twice so the second save exercises the compare against an already-guarded write
	/// rather than only against an absent key.
	/// </remarks>
	[Fact]
	public async Task StillAcceptASaveThatCarriesANewerVersion()
	{
		var aggregateId = Guid.NewGuid().ToString();
		const string aggregateType = "MonotonicAggregate";

		await SnapshotStore!.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 10, [10]),
			TestContext.Current.CancellationToken);

		await SnapshotStore.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 20, [20]),
			TestContext.Current.CancellationToken);

		await SnapshotStore.SaveSnapshotAsync(
			CreateTestSnapshot(aggregateId, aggregateType, 30, [30]),
			TestContext.Current.CancellationToken);

		var retrieved = await SnapshotStore.GetLatestSnapshotAsync(
			aggregateId,
			aggregateType,
			TestContext.Current.CancellationToken);

		_ = retrieved.ShouldNotBeNull();
		retrieved.Version.ShouldBe(
			30,
			"the guard must refuse only saves that would move the snapshot backwards - a store that "
			+ "refused everything would satisfy the stale-save arm while storing nothing");

		// The body must advance with the version. A guard that updated the version field while leaving
		// the previous payload in place would pass a version-only assertion and return a snapshot whose
		// body belongs to an earlier version.
		retrieved.Data.ToArray().ShouldBe(new byte[] { 30 });
	}

	/// <summary>
	/// Reads the tenant established by <see cref="TenantContextHolder.BeginScope"/>. The production
	/// equivalent is internal to Excalibur.Dispatch, so a directly-constructed store needs this here.
	/// </summary>
	private sealed class AmbientTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}
}
