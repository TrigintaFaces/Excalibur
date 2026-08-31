// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.EventSourcing;
using Excalibur.EventSourcing.Postgres;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Runs the <strong>shipped</strong> <see cref="SnapshotStoreConformanceTestKit"/> against a real
/// Postgres, through the same <see cref="PostgresSnapshotStore"/> a consumer constructs.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="PostgresSnapshotStoreConformanceShould"/> in this file's own directory derives
/// <c>SnapshotConformanceTestBase</c>, a test-only base under <c>tests/</c> that is not shipped. Every
/// durable snapshot backend in this repository is bound to that internal base -- Postgres included -- so
/// the artifact the package's NuGet Description names (<c>ISnapshotStore</c>) and the artifact this
/// provider is actually held to have never been the same object. A consumer who derives the published
/// <see cref="SnapshotStoreConformanceTestKit"/> inherits arms that no provider in this repository, real
/// or in-memory, had ever run against a real engine.
/// </para>
/// <para>
/// This suite closes that gap for one provider, the same way the outbox and event-store families were
/// closed: derive the published kit, not the internal twin, against the real backend. It reuses the
/// existing <see cref="PostgresSnapshotStoreContainerFixture"/> rather than standing up a second
/// container -- that fixture already provisions from the scripts the package ships
/// (<c>001_CreateSnapshotSchema.sql</c>, <c>002_MigrateSnapshotsToKeyedSentinel.sql</c>), so nothing here
/// restates the schema.
/// </para>
/// </remarks>
[Collection(PostgresSnapshotStoreTestCollection.CollectionName)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
public sealed class PostgresSnapshotStoreKitConformanceShould : SnapshotStoreConformanceTestKit,
	IClassFixture<PostgresSnapshotStoreContainerFixture>, IAsyncLifetime
{
	private readonly PostgresSnapshotStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="PostgresSnapshotStoreKitConformanceShould"/> class.</summary>
	/// <param name="fixture">The shared Postgres snapshot-store container.</param>
	public PostgresSnapshotStoreKitConformanceShould(PostgresSnapshotStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>Brings the container and its shipped schema up before any arm resolves the store.</summary>
	public async ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"The snapshot conformance contract is verified against a real Postgres -- its upsert "
			+ "resolution, tenant partitioning and concurrency behaviour are server-side. This suite "
			+ "must never be skipped.");

		await _fixture.EnsureInitializedAsync().ConfigureAwait(false);
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// Constructed directly, per the kit's own seam (<c>CreateStoreAsync</c> takes no dependencies) --
	/// this kit has no DI-resolve entry point to go through, unlike the event-store kit. The ambient
	/// tenant context is shared with <see cref="ConformanceAmbientTenantContext"/>'s documented reason:
	/// the tenancy arms switch the ambient scope around ONE store instance, and a store built with a
	/// fixed tenant would ignore that switch entirely.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateStoreAsync() =>
		Task.FromResult<ISnapshotStore>(new PostgresSnapshotStore(
			_fixture.ConnectionString,
			NullLogger<PostgresSnapshotStore>.Instance,
			new ConformanceAmbientTenantContext()));

	/// <inheritdoc />
	/// <remarks>
	/// Arms share one container and one table, so a row left by an earlier arm is visible to the next.
	/// </remarks>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

	[Fact]
	public Task GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull_Test() => GetLatestSnapshotAsync_NoSnapshot_ShouldReturnNull();

	[Fact]
	public Task SaveAndGetLatestSnapshot_ShouldRoundTrip_Test() => SaveAndGetLatestSnapshot_ShouldRoundTrip();

	[Fact]
	public Task GetLatestSnapshot_MultipleVersions_ShouldReturnLatest_Test() => GetLatestSnapshot_MultipleVersions_ShouldReturnLatest();

	[Fact]
	public Task SaveSnapshot_ShouldUpdateLatest_Test() => SaveSnapshot_ShouldUpdateLatest();

	[Fact]
	public Task SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp_Test() => SaveSnapshot_StaleOrEqualVersion_ShouldBeASuccessfulNoOp();

	[Fact]
	public Task DeleteSnapshots_ShouldRemoveAll_Test() => DeleteSnapshots_ShouldRemoveAll();

	[Fact]
	public Task DeleteSnapshotsOlderThan_ShouldPreserveNewer_Test() => DeleteSnapshotsOlderThan_ShouldPreserveNewer();

	[Fact]
	public Task DeleteSnapshots_NonExistent_ShouldNotThrow_Test() => DeleteSnapshots_NonExistent_ShouldNotThrow();

	[Fact]
	public Task Snapshots_ShouldIsolateByAggregateType_Test() => Snapshots_ShouldIsolateByAggregateType();

	[Fact]
	public Task Snapshots_ShouldIsolateByAggregateId_Test() => Snapshots_ShouldIsolateByAggregateId();

	[Fact]
	public Task DeleteSnapshots_ShouldNotAffectOtherAggregates_Test() => DeleteSnapshots_ShouldNotAffectOtherAggregates();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveData_Test() => SaveAndLoad_ShouldPreserveData();

	[Fact]
	public Task Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite_Test() => Snapshots_HigherVersionFromAnotherTenant_MustNotOverwrite();

	[Fact]
	public Task Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded_Test() => Snapshots_SaveBehindAnotherTenantsVersion_MustNotBeDiscarded();

	[Fact]
	public Task Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId_Test() => Snapshots_EachTenant_MustReadItsOwnForASharedAggregateId();

	[Fact]
	public Task SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable_Test() => SaveSnapshot_ConcurrentRisingVersions_ShouldLeaveTheHighestReadable();

	[Fact]
	public Task GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt_Test() => GetLatestSnapshot_ConcurrentReaders_ShouldAllObserveIt();

	[Fact]
	public Task Store_ShouldNotFaultWhenManyCallersArriveAtOnce_Test() => Store_ShouldNotFaultWhenManyCallersArriveAtOnce();

	[Fact]
	public Task SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty_Test() => SaveAndLoad_EmptyPayload_ShouldRoundTripAsEmpty();

	[Fact]
	public Task SaveAndLoad_LargePayload_ShouldRoundTripByteForByte_Test() => SaveAndLoad_LargePayload_ShouldRoundTripByteForByte();

	[Fact]
	public Task SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip_Test() => SaveAndLoad_AggregateIdWithSeparatorCharacters_ShouldRoundTrip();

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();
}
