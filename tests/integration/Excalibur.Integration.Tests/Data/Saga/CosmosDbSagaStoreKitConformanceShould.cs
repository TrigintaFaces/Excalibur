// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Saga.CosmosDb;
using Excalibur.Saga.DependencyInjection;
using Excalibur.Testing.Conformance;

using Microsoft.Azure.Cosmos;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Runs the SHIPPED <see cref="SagaStoreConformanceTestKit"/> against the real Cosmos DB saga store.
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
/// Cosmos DB ships no <c>AddCosmosDbSagaStore</c>. Its public path is the composed saga builder, so that is
/// what is wired here. The <c>AddSagas</c> overloads are ambiguous for an untyped lambda
/// (<c>Action&lt;SagaOptions&gt;</c> against <c>Action&lt;ISagaBuilder&gt;</c>), hence the explicit
/// parameter type. The emulator client is handed to the builder rather than a connection string because
/// only that client trusts the self-signed certificate the emulator presents.
/// </para>
/// <para>
/// Every arm the kit declares is wired here. An arm nobody wires never runs, and an arm that never runs is
/// indistinguishable in a test report from one that passed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Infrastructure", "CosmosEmulator")]
[Trait("Database", "CosmosDb")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class CosmosDbSagaStoreKitConformanceShould : SagaStoreConformanceTestKit, IClassFixture<CosmosDbSagaStoreContainerFixture>
{
	// Shared by every arm in this class, which is what makes ResetDataAsync the thing that isolates them.
	// A per-instance name would give each arm a private Cosmos container (and a fresh 400-RU provision) and
	// the reset would never be load-bearing.
	private static readonly string ContainerName = $"sagas_kit_{Guid.NewGuid():N}";

	private readonly CosmosDbSagaStoreContainerFixture _fixture;

	public CosmosDbSagaStoreKitConformanceShould(CosmosDbSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// The provider's own shipped registration path and nothing else. Constructing the store here would
	/// reintroduce exactly the hole this seam closes.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddExcalibur(x => x.AddSagas((ISagaBuilder saga) => saga.UseCosmosDb(cosmos =>
			cosmos.Client(_fixture.Client)
				  .DatabaseName(_fixture.DatabaseName)
				  .ContainerName(ContainerName))));

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only: items are deleted, the container is left in place, and neither the fixture client nor the
	/// store is disposed. The kit calls this before every arm, so disposing anything here would fail every
	/// arm on a dead handle rather than on the contract, which is the failure this suite's sibling spent a
	/// whole run reporting instead of the real cause.
	/// </remarks>
	protected override async Task ResetDataAsync()
	{
		var container = _fixture.Client.GetContainer(_fixture.DatabaseName, ContainerName);

		try
		{
			using var iterator = container.GetItemQueryIterator<SagaDocumentKey>("SELECT c.id, c.sagaType FROM c");
			while (iterator.HasMoreResults)
			{
				foreach (var document in await iterator.ReadNextAsync().ConfigureAwait(false))
				{
					_ = await container
						.DeleteItemAsync<SagaDocumentKey>(document.Id, new PartitionKey(document.SagaType))
						.ConfigureAwait(false);
				}
			}
		}
		catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
		{
			// The store creates the container on first use, so before the first arm there is nothing to
			// clear. An absent container is an empty one for this purpose.
		}
	}

	/// <inheritdoc/>
	protected override Task CleanupAsync() => ResetDataAsync();

	/// <summary>
	/// The two fields a delete needs: the document id and the partition key value.
	/// </summary>
	/// <remarks>
	/// The provider's own document type is <c>internal</c>, and this teardown is not a reason to widen the
	/// shipped surface. Projecting the two fields in the query keeps the coupling to the stored shape down
	/// to the property names the store itself writes.
	/// </remarks>
	private sealed class SagaDocumentKey
	{
		[System.Text.Json.Serialization.JsonPropertyName("id")]
		public string Id { get; set; } = string.Empty;

		[System.Text.Json.Serialization.JsonPropertyName("sagaType")]
		public string SagaType { get; set; } = string.Empty;
	}

	/// <inheritdoc/>
	/// <remarks>
	/// CosmosDbSagaStore compares the persisted version against the loaded one and throws
	/// <c>ConcurrencyException</c> on a mismatch (CosmosDbSagaStore.SaveAsync :246-253), closing the
	/// read-write race with <c>IfMatchEtag</c>, so the optimistic-concurrency arms run rather than
	/// early-return.
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
