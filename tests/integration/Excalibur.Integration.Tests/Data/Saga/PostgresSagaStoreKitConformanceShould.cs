// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Runs the SHIPPED <see cref="SagaStoreConformanceTestKit"/> against the real Postgres saga store.
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
/// Every arm the kit declares is wired here. An arm nobody wires never runs, and an arm that never runs is
/// indistinguishable in a test report from one that passed.
/// </para>
/// </remarks>
[Collection("PostgresSagaStore")]
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Postgres")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
public sealed class PostgresSagaStoreKitConformanceShould : SagaStoreConformanceTestKit, IAsyncLifetime
{
	private readonly PostgresSagaStoreContainerFixture _fixture;

	public PostgresSagaStoreKitConformanceShould(PostgresSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	public async ValueTask InitializeAsync() => await _fixture.EnsureInitializedAsync().ConfigureAwait(false);

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// The provider's own shipped registration extension and nothing else. Constructing the store here
	/// would reintroduce exactly the hole this seam closes.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddPostgresSagaStore(options =>
		{
			options.ConnectionString = _fixture.ConnectionString;
			options.Schema = _fixture.Schema;
			options.TableName = _fixture.TableName;
			options.CommandTimeoutSeconds = 30;
		});

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only. The kit calls this before every arm, so it must not dispose anything the arm is about
	/// to use; the container fixture owns the connection lifetime.
	/// </remarks>
	protected override Task ResetDataAsync() => _fixture.CleanupTableAsync();

	/// <inheritdoc/>
	protected override Task CleanupAsync() => _fixture.CleanupTableAsync();

	/// <inheritdoc/>
	/// <remarks>
	/// PostgresSagaStore is version-gated (<c>WHERE version = @ExpectedVersion</c> plus a no-resurrect
	/// insert guard), so the optimistic-concurrency arms run rather than early-return.
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
