// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data;
using Excalibur.Data.Firestore.Snapshots;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Runs the <strong>shipped</strong> <see cref="SnapshotStoreConformanceTestKit"/> against a real
/// Firestore emulator, through the same <see cref="FirestoreSnapshotStore"/> a consumer constructs.
/// </summary>
/// <remarks>
/// See <c>PostgresSnapshotStoreKitConformanceShould</c> for why the internal
/// <c>FirestoreSnapshotStoreConformanceShould</c> in this directory does not exercise the published
/// contract. This suite closes the same gap for Firestore, reusing the existing
/// <see cref="FirestoreSnapshotStoreContainerFixture"/>.
/// </remarks>
[Collection(FirestoreSnapshotStoreTestCollection.CollectionName)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "STORE")]
public sealed class FirestoreSnapshotStoreKitConformanceShould : SnapshotStoreConformanceTestKit, IAsyncLifetime
{
	private readonly FirestoreSnapshotStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="FirestoreSnapshotStoreKitConformanceShould"/> class.</summary>
	/// <param name="fixture">The shared Firestore emulator container.</param>
	public FirestoreSnapshotStoreKitConformanceShould(FirestoreSnapshotStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>Confirms the emulator is up before any arm resolves the store.</summary>
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Firestore emulator must be available - real-infra conformance is never skipped.");
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// Binds the emulator-connected <c>FirestoreDb</c> (default serializer settings), so the round-trip
	/// exercises the wire shape consumers actually get. <see cref="ConformanceAmbientTenantContext"/> is
	/// the shipped kit helper: without it a store built with no context resolves <c>None</c> for every
	/// caller, which collapses every tenant onto the untenanted document id.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateStoreAsync()
	{
		var options = Options.Create(new FirestoreSnapshotStoreOptions
		{
			ProjectId = _fixture.ProjectId,
			CollectionName = _fixture.CollectionName,
			EmulatorHost = _fixture.EmulatorEndpoint,
		});

		return Task.FromResult<ISnapshotStore>(
			new FirestoreSnapshotStore(
				_fixture.Db,
				options,
				NullLogger<FirestoreSnapshotStore>.Instance,
				new ConformanceAmbientTenantContext()));
	}

	/// <inheritdoc />
	protected override Task CleanupAsync() => _fixture.CleanupCollectionAsync();

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

	/// <summary>
	/// Runs the kit's concurrency arm unmodified: ten concurrent saves at rising versions must all
	/// succeed and must leave version 100 -- the highest -- readable. Proves Firestore resolves
	/// contended upserts highest-version-wins rather than last-writer-wins, and that the store's
	/// bounded contention retry actually absorbs the emulator's transaction-lock aborts instead of
	/// surfacing them to the caller.
	/// </summary>
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
