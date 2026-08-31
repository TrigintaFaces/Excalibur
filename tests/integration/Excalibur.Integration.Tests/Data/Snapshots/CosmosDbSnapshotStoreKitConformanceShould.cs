// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.CosmosDb;
using Excalibur.Data.CosmosDb.Snapshots;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Runs the <strong>shipped</strong> <see cref="SnapshotStoreConformanceTestKit"/> against a real Cosmos
/// DB emulator, through the same <see cref="CosmosDbSnapshotStore"/> a consumer constructs.
/// </summary>
/// <remarks>
/// <see cref="CosmosDbSnapshotStoreConformanceShould"/> in this directory derives the internal, unshipped
/// <c>SnapshotConformanceTestBase</c> -- see <c>PostgresSnapshotStoreKitConformanceShould</c> for why that
/// leaves the published contract unexercised. This suite closes the same gap for Cosmos DB, reusing the
/// existing <see cref="CosmosDbSnapshotStoreContainerFixture"/> rather than standing up a second emulator.
/// </remarks>
[Collection(CosmosDbSnapshotStoreTestCollection.CollectionName)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
[Trait("Pattern", "STORE")]
public sealed class CosmosDbSnapshotStoreKitConformanceShould : SnapshotStoreConformanceTestKit,
	IClassFixture<CosmosDbSnapshotStoreContainerFixture>, IAsyncLifetime
{
	private readonly CosmosDbSnapshotStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="CosmosDbSnapshotStoreKitConformanceShould"/> class.</summary>
	/// <param name="fixture">The shared Cosmos DB snapshot-store container.</param>
	public CosmosDbSnapshotStoreKitConformanceShould(CosmosDbSnapshotStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>Confirms the emulator is up before any arm resolves the store.</summary>
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"Cosmos DB emulator must be available - real-infra conformance is never skipped.");
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// The store builds its own <c>CosmosClient</c> from the connection string (the default consumer
	/// path), in gateway mode with the emulator's HttpClient so the self-signed certificate is trusted.
	/// The ambient tenant context is required: the kit's tenancy arms switch the ambient scope
	/// (<see cref="TenantContextHolder.BeginScope"/>) around ONE store instance, and a store built with a
	/// fixed tenant would ignore that switch entirely. <see cref="ConformanceAmbientTenantContext"/> is
	/// the shipped kit helper for that; it falls back to the reserved untenanted sentinel outside any
	/// scope, which every non-tenancy arm runs under.
	/// </remarks>
	/// <remarks>
	/// Deliberately does NOT eagerly call <c>store.InitializeAsync()</c>: <see cref="CleanupAsync"/> runs
	/// AFTER this method returns (<c>CreateStoreForArmAsync</c> calls <c>CreateStoreAsync</c> then
	/// <c>ResetDataAsync</c>), and this fixture's cleanup deletes the container outright. An eager
	/// initialize here would latch a container reference that cleanup then deletes out from under it,
	/// so every write in the arm that follows 404s against a container that no longer exists. Leaving
	/// initialization lazy (<c>CreateContainerIfNotExists = true</c>) lets the store recreate the
	/// container on the first real operation, which runs after cleanup.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateStoreAsync()
	{
		var options = Options.Create(new CosmosDbSnapshotStoreOptions
		{
			Client = new CosmosDbClientOptions
			{
				ConnectionString = _fixture.ConnectionString,
				UseDirectMode = false,
				HttpClientFactory = () => _fixture.EmulatorHttpClient,
			},
			DatabaseName = _fixture.DatabaseName,
			ContainerName = _fixture.ContainerName,
			PartitionKeyPath = "/aggregateType",
			CreateContainerIfNotExists = true,
			ContainerThroughput = 400,
		});

		return Task.FromResult<ISnapshotStore>(new CosmosDbSnapshotStore(
			options,
			NullLogger<CosmosDbSnapshotStore>.Instance,
			new ConformanceAmbientTenantContext()));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Container, not database: the database is fixture-owned and shared across every arm in this class.
	/// </remarks>
	protected override Task CleanupAsync() => _fixture.CleanupContainerAsync();

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
