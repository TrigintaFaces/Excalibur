// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using Excalibur.Saga.Orchestration;

using Excalibur.Testing.Conformance;

using Xunit;

namespace Excalibur.Tests.Testing.Conformance;

/// <summary>
/// Conformance tests for <see cref="InMemorySagaStore"/> validating ISagaStore contract compliance.
/// </summary>
/// <remarks>
/// InMemorySagaStore directly implements <see cref="ISagaStore"/> from Excalibur.Dispatch.Messaging.Delivery,
/// so no adapter is needed. The conformance test kit validates the contract compliance.
/// </remarks>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
[Trait("Category", "Integration")]
[Trait("Component", "Core")]
[Trait("Pattern", "STORE")]
public sealed class InMemorySagaStoreConformanceTests : SagaStoreConformanceTestKit
{
	/// <inheritdoc />
	/// <remarks>
	/// The kit models a host with no tenant established, which the store requires be stated rather than
	/// implied. <c>LoadAsync</c> and <c>SaveAsync</c> key on the tenant partition <em>together with</em>
	/// the saga identifier, so the ambient scope is part of the address rather than a predicate applied
	/// after lookup. That is what makes a cross-tenant read unaddressable instead of merely refused, and
	/// it is the property the tenancy arms below bind.
	///
	/// This remark previously stated the opposite — that both members keyed on the saga identifier alone
	/// and never consulted the ambient scope. That was an accurate description of a defect, not of a
	/// design: the shared key gave every tenant one version counter, so a second tenant creating its own
	/// saga under the same identifier could never succeed. Corrected here when the store was fixed,
	/// because a comment describing a repaired defect in the present tense reads as the contract.
	/// </remarks>
	protected override void ConfigureProvider(IServiceCollection services) =>
		services.AddInMemorySagaStore();

	// InMemorySagaStore is a genuine optimistic-concurrency implementation (expected-version CAS +
	// no-resurrect guard, InMemorySagaStore.SaveAsync) since boxiyl (S853), so the keystone facts run
	// NON-SKIPPED against real optimistic logic (verify-against-real-infra: a real impl, not a mock).
	/// <inheritdoc />
	protected override bool SupportsOptimisticConcurrency => true;

	#region Save Tests

	[Fact]
	public Task SaveAsync_NewSaga_ShouldSucceed_Test() =>
		SaveAsync_NewSaga_ShouldSucceed();

	[Fact]
	public Task SaveAsync_ExistingSaga_ShouldUpdate_Test() =>
		SaveAsync_ExistingSaga_ShouldUpdate();

	[Fact]
	public Task SaveAsync_CompletedSaga_ShouldPersistCompletedFlag_Test() =>
		SaveAsync_CompletedSaga_ShouldPersistCompletedFlag();

	#endregion Save Tests

	#region Load Tests

	[Fact]
	public Task LoadAsync_NonExistent_ShouldReturnNull_Test() =>
		LoadAsync_NonExistent_ShouldReturnNull();

	[Fact]
	public Task LoadAsync_ExistingSaga_ShouldReturnState_Test() =>
		LoadAsync_ExistingSaga_ShouldReturnState();

	[Fact]
	public Task LoadAsync_AfterMultipleUpdates_ShouldReturnLatest_Test() =>
		LoadAsync_AfterMultipleUpdates_ShouldReturnLatest();

	#endregion Load Tests

	#region Round-Trip Tests

	[Fact]
	public Task SaveAndLoad_ShouldPreserveAllProperties_Test() =>
		SaveAndLoad_ShouldPreserveAllProperties();

	[Fact]
	public Task SaveAndLoad_ShouldPreserveDateTimeValues_Test() =>
		SaveAndLoad_ShouldPreserveDateTimeValues();

	#endregion Round-Trip Tests

	#region Isolation Tests

	[Fact]
	public Task Sagas_ShouldIsolateBySagaId_Test() =>
		Sagas_ShouldIsolateBySagaId();

	[Fact]
	public Task UpdateOneSaga_ShouldNotAffectOthers_Test() =>
		UpdateOneSaga_ShouldNotAffectOthers();

	#endregion Isolation Tests

	#region Edge Cases

	[Fact]
	public Task SaveAsync_WithDefaultValues_ShouldSucceed_Test() =>
		SaveAsync_WithDefaultValues_ShouldSucceed();

	#endregion Edge Cases

	#region Optimistic Concurrency Tests (e1tsq2 / FR-D5)

	[Fact]
	public Task StaleSave_ThrowsConcurrencyException_NoLostUpdate_Test() =>
		StaleSave_ThrowsConcurrencyException_NoLostUpdate();

	[Fact]
	public Task StaleSave_OnMissingSaga_DoesNotResurrect_Test() =>
		StaleSave_OnMissingSaga_DoesNotResurrect();

	[Fact]
	public Task LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds_Test() =>
		LoadAsync_ReturnsAuthoritativeVersion_AndReloadMutateSaveSucceeds();

	#endregion Optimistic Concurrency Tests

	#region Tenant Confinement Tests

	// InMemorySagaStore resolves the ambient ITenantContext per operation, so these run against a real
	// tenant-partitioned implementation rather than a mock. Their non-vacuity -- that each goes RED
	// against a store carrying the defect it names -- is proven separately in SagaStoreTenantArmsBindShould.

	[Fact]
	public Task TenantScopedLoad_MustNotSeeAnotherTenantsSaga_Test() =>
		TenantScopedLoad_MustNotSeeAnotherTenantsSaga();

	[Fact]
	public Task TenantScopedLoad_MustSeeItsOwnSaga_Test() =>
		TenantScopedLoad_MustSeeItsOwnSaga();

	[Fact]
	public Task TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId_Test() =>
		TenantPartitions_MustNotOverwriteEachOthersSagaWithTheSameId();

	[Fact]
	public Task UntenantedPartition_MustRoundTripItsOwnSaga_Test() =>
		UntenantedPartition_MustRoundTripItsOwnSaga();

	#endregion Tenant Confinement Tests

	#region Suite Wiring

	/// <summary>
	/// Fails if this suite stops exposing any arm the kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail — in the results it
	/// is indistinguishable from one that passed. That is why the wiring is checked rather than trusted to
	/// survive an edit: a new arm added to the shipped kit turns this red here instead of going silently
	/// unrun.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion
}
