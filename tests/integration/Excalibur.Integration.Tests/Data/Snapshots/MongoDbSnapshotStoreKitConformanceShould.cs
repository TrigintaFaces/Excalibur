// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.MongoDB.Snapshots;
using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Integration.Tests.Data.Snapshots;

/// <summary>
/// Runs the <strong>shipped</strong> <see cref="SnapshotStoreConformanceTestKit"/> against a real
/// MongoDB container, through the same <see cref="MongoDbSnapshotStore"/> a consumer constructs.
/// </summary>
/// <remarks>
/// See <c>PostgresSnapshotStoreKitConformanceShould</c> for why the internal
/// <c>MongoDbSnapshotStoreConformanceShould</c> in this directory does not exercise the published
/// contract. This suite closes the same gap for MongoDB, reusing the existing
/// <see cref="MongoDbSnapshotStoreContainerFixture"/>. The internal suite's two extra regression
/// facts (a rare version-loss race, a duplicate key from a foreign index) stay where they are -- they
/// are provider-specific regression locks, not kit arms, and remain exercised there.
/// </remarks>
[Collection(MongoDbSnapshotStoreTestCollection.CollectionName)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Database", "MongoDb")]
[Trait("Pattern", "STORE")]
public sealed class MongoDbSnapshotStoreKitConformanceShould : SnapshotStoreConformanceTestKit,
	IClassFixture<MongoDbSnapshotStoreContainerFixture>, IAsyncLifetime
{
	private readonly MongoDbSnapshotStoreContainerFixture _fixture;

	/// <summary>Initializes a new instance of the <see cref="MongoDbSnapshotStoreKitConformanceShould"/> class.</summary>
	/// <param name="fixture">The shared MongoDB container.</param>
	public MongoDbSnapshotStoreKitConformanceShould(MongoDbSnapshotStoreContainerFixture fixture) => _fixture = fixture;

	/// <summary>Confirms the container is up before any arm resolves the store.</summary>
	public ValueTask InitializeAsync()
	{
		_fixture.DockerAvailable.ShouldBeTrue(
			"MongoDB container must be available - real-infra conformance is never skipped.");
		return ValueTask.CompletedTask;
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	/// <remarks>
	/// Options-only constructor: the store builds the provider's DEFAULT <c>MongoClient</c> (default
	/// serializer) from the connection string, the surface most consumers use.
	/// <see cref="ConformanceAmbientTenantContext"/> is the shipped kit helper: without it a store built
	/// with no context resolves <c>None</c> for every caller, collapsing every tenant onto one document.
	/// </remarks>
	protected override Task<ISnapshotStore> CreateStoreAsync()
	{
		var options = Options.Create(new MongoDbSnapshotStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			DatabaseName = _fixture.DatabaseName,
			CollectionName = _fixture.CollectionName,
		});

		return Task.FromResult<ISnapshotStore>(
			new MongoDbSnapshotStore(
				options,
				NullLogger<MongoDbSnapshotStore>.Instance,
				new ConformanceAmbientTenantContext()));
	}

	/// <inheritdoc />
	protected override Task CleanupAsync() => _fixture.CleanupAsync();

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
