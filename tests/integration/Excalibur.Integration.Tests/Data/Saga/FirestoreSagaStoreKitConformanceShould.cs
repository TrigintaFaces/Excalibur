// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Saga.DependencyInjection;
using Excalibur.Testing.Conformance;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Integration.Tests.Data.Saga;

/// <summary>
/// Runs the SHIPPED <see cref="SagaStoreConformanceTestKit"/> against the real Firestore saga store
/// (emulator), through the provider's OWN public registration path.
/// </summary>
/// <remarks>
/// <para>
/// The companion concurrency suite in this directory derives a conformance base that lives under
/// <c>tests/</c> and is obtainable by nobody outside this repository. That base takes a store the test
/// constructs by hand; this kit takes an <see cref="IServiceCollection"/> and resolves the store the
/// provider's own registration produces. The difference is not stylistic: a hand-built store is whatever
/// the test author assembled, so the arms certify an object no consumer receives, and the four tenant arms
/// below have no counterpart in the private base at all.
/// </para>
/// <para>
/// <strong>This suite is expected to fail at store resolution, and that failure is the point.</strong>
/// <c>UseFirestore</c> registers the concrete <c>FirestoreSagaStore</c> plus two KEYED <c>ISagaStore</c>
/// aliases (<c>"firestore"</c> and <c>"default"</c>), but never a non-keyed <c>ISagaStore</c> — so
/// <c>GetRequiredService&lt;ISagaStore&gt;()</c>, which is what a consumer injecting <c>ISagaStore</c>
/// receives, has nothing to resolve. Registering that alias here would turn this suite green against a
/// registration no consumer can reproduce, which is the same defect wearing a different hat. The fix
/// belongs in the provider's registration extension.
/// </para>
/// <para>
/// Every arm the kit declares is wired here. An arm nobody wires never runs, and an arm that never runs is
/// indistinguishable in a test report from one that passed.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Component", "Saga")]
[Trait("Database", "Firestore")]
[Trait("Pattern", "STORE")]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Conformance arm naming convention")]
[Collection(global::Excalibur.Integration.Tests.FirestoreSerialCollection.CollectionName)]
public sealed class FirestoreSagaStoreKitConformanceShould : SagaStoreConformanceTestKit, IAsyncLifetime
{
	private readonly FirestoreSagaStoreContainerFixture _fixture;
	private readonly string _collectionName = $"sagas_kit_{Guid.NewGuid():N}";

	public FirestoreSagaStoreKitConformanceShould(FirestoreSagaStoreContainerFixture fixture) => _fixture = fixture;

	/// <inheritdoc/>
	/// <remarks>
	/// NON-SKIPPED. Real infrastructure is a hard requirement here: a skip-gated infra arm passes by not
	/// running, which is exactly the reporting shape this suite exists to remove.
	/// </remarks>
	public ValueTask InitializeAsync()
	{
		// EnsureAvailable() rather than asserting the flag: the FIXTURE owns the availability
		// policy, and its message carries the RECORDED startup error. Asserting a bool produced
		// a true-but-useless "expected True" with the actual container failure discarded.
		_fixture.EnsureAvailable();

		return ValueTask.CompletedTask;
	}

	/// <inheritdoc/>
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>
	/// The provider's own shipped saga-builder extension and nothing else. Constructing the store here, or
	/// adding the missing non-keyed <c>ISagaStore</c> alias, would reintroduce exactly the hole this seam
	/// closes. The lambda parameter is typed explicitly because the <c>AddSagas</c> overloads are otherwise
	/// ambiguous for a bare lambda and the compiler binds <c>Action&lt;SagaOptions&gt;</c>.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services) =>
		_ = services.AddExcalibur(x => x.AddSagas((ISagaBuilder saga) =>
			_ = saga.UseFirestore(firestore =>
				_ = firestore
					.ProjectId(_fixture.ProjectId)
					.EmulatorHost(_fixture.EmulatorEndpoint)
					.CollectionName(_collectionName))));

	/// <inheritdoc/>
	/// <remarks>
	/// Data-only, and a no-op by construction: this instance owns a private collection name, so an arm never
	/// starts against another arm's data. The kit calls this before every arm, so it must not dispose
	/// anything the arm is about to use; the container fixture owns the emulator and client lifetime.
	/// </remarks>
	protected override Task ResetDataAsync() => Task.CompletedTask;

	/// <inheritdoc/>
	/// <remarks>Data-only. The throwaway per-instance collection goes away with the emulator.</remarks>
	protected override Task CleanupAsync() => Task.CompletedTask;


	#region Save

	// Restored. This derives from the SHIPPED kit, whose SupportsOptimisticConcurrency still defaults to
	// false and whose concurrency arms SkipArm when it does. Removing the flag from the INTERNAL
	// conformance base and stripping this override together silently turned those arms off here -- and a
	// recorded skip does not fail, so nothing reported it. This store does enforce optimistic concurrency.
	protected override bool SupportsOptimisticConcurrency => true;

	[Fact]
	public Task SaveAsync_NewSaga_ShouldSucceed_Test() => SaveAsync_NewSaga_ShouldSucceed();

	[Fact]
	public Task ProcessedEventIds_SurviveTheRoundTrip_SoAReplayIsStillRecognised_Test() =>
		ProcessedEventIds_SurviveTheRoundTrip_SoAReplayIsStillRecognised();

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
