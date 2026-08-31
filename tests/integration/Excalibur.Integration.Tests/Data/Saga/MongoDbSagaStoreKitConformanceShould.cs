// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Testing.Conformance;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Runs the SHIPPED <see cref="SagaStoreConformanceTestKit"/> against the real MongoDB saga store.
/// </summary>
/// <remarks>
/// <para>
/// The companion class in this directory runs a conformance base that lives under <c>tests/</c> and is
/// therefore obtainable by nobody outside this repository. That base takes a store the test constructs by
/// hand; this kit takes a <see cref="IServiceCollection"/> and resolves the store the provider's own
/// registration produces. The difference is not stylistic: a hand-built store is whatever the test author
/// assembled, so the arms certify an object no consumer receives, and the four tenant arms below have no
/// counterpart in the private base at all.
/// </para>
/// <para>
/// MongoDB ships no <c>AddMongoDbSagaStore</c>. Its public path is the composed saga builder, so that is
/// what is wired here. The <c>AddSagas</c> overloads are ambiguous for an untyped lambda
/// (<c>Action&lt;SagaOptions&gt;</c> against <c>Action&lt;ISagaBuilder&gt;</c>), hence the explicit
/// parameter type.
/// </para>
/// <para>
/// Every arm the kit declares is wired here. An arm nobody wires never runs, and an arm that never runs is
/// indistinguishable in a test report from one that passed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "MongoDB")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class MongoDbSagaStoreKitConformanceShould : SagaStoreConformanceTestKit, IClassFixture<MongoDbContainerFixture>
{
	private const string CollectionName = "sagas";

	// Shared by every arm in this class, which is what makes ResetDataAsync the thing that isolates them —
	// a per-instance name would give each arm a private database and the reset would never be load-bearing.
	private static readonly string DatabaseName = $"saga_kit_conf_{Guid.NewGuid():N}";

	private readonly MongoDbContainerFixture _fixture;

	public MongoDbSagaStoreKitConformanceShould(MongoDbContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// The provider's own shipped registration path and nothing else. Constructing the store here would
	/// reintroduce exactly the hole this seam closes.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddExcalibur(x => x.AddSagas((ISagaBuilder saga) => saga.UseMongoDB(mongo =>
			mongo.ConnectionString(_fixture.ConnectionString)
				 .DatabaseName(DatabaseName)
				 .CollectionName(CollectionName))));

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only: documents are removed, the collection and the indexes the store built on it are left in
	/// place, and no client the arm is about to use is disposed. Dropping the database here would discard
	/// that index state on every arm; disposing anything here would fail every arm on a dead handle rather
	/// than on the contract.
	/// </remarks>
	protected override async Task ResetDataAsync()
	{
		var client = new MongoClient(_fixture.ConnectionString);
		var collection = client.GetDatabase(DatabaseName).GetCollection<BsonDocument>(CollectionName);
		_ = await collection.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty).ConfigureAwait(false);
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => ResetDataAsync();

	/// <inheritdoc/>
	/// <remarks>
	/// MongoDbSagaStore gates its update on <c>{tenant, SagaId, Version}</c> and upserts only at expected
	/// version 0 (MongoDbSagaStore.SaveAsync, the filter at :216 and the <c>isInsert</c> guard at :238), so
	/// the optimistic-concurrency arms run rather than early-return.
	/// </remarks>
	protected override bool SupportsOptimisticConcurrency => true;

	#region Save

	[Fact]
	public Task SaveAsync_NewSaga_ShouldSucceed_Test() => SaveAsync_NewSaga_ShouldSucceed();

	[Fact]
	public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() => SaveAsync_ExistingSaga_ShouldUpdate();

	[Fact]
	public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() =>
		SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();

	#endregion Save

	#region Load

	[Fact]
	public Task LoadAsync_NonExistent_ShouldReturnNull_Test() => LoadAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() => LoadAsync_ExistingSaga_ShouldReturnState();

	[Fact]
	public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() =>
		LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();

	#endregion Load

	#region Round-trip

	[Fact]
	public Task SaveAndLoad_ShouldPreserveAllProperties_Test() => SaveAndLoad_ShouldPreserveAllProperties();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() => SaveAndLoad_ShouldPreserveDateTimeValues();

	#endregion Round-trip

	#region Isolation

	[Fact]
	public Task Sagas_ShouldIsolateBySagaId_Test() => Sagas_ShouldIsolateBySagaId();

	[Fact]
	public Task UpdateOneSaga_ShouldNotAffectOthers_Test() => UpdateOneSaga_ShouldNotAffectOthers();

	#endregion Isolation

	#region Edge cases

	[Fact]
	public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() => SaveAsync_WithDefaultValues_ShouldSucceed();

	#endregion Edge cases

	#region Optimistic concurrency

	[Fact]
	public Task StaleSave_ThrowsConcurrencyException_NoLostUpdate_Test() =>
		StaleSave_ThrowsConcurrencyException_NoLostUpdate();

	[Fact]
	public Task StaleSave_OnMissingSaga_DoesNotResurrect_Test() => StaleSave_OnMissingSaga_DoesNotResurrect();

	[Fact]
	public Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds_Test() =>
		LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds();

	#endregion Optimistic concurrency

	#region Tenant confinement

	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsSaga();

	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnSaga_Test() => TenantScopedLoad_MustSeeItsOwnSaga();

	[Fact]
	public Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId_Test() =>
		TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();

	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnSaga_Test() => UntenantedPartition_MustRoundTripItsOwnSaga();

	#endregion Tenant confinement

	#region Suite wiring

	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite wiring
}
