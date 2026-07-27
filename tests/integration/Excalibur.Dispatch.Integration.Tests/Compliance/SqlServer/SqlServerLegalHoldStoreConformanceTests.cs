// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Testing.Conformance;


namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Runs the shared legal-hold conformance kit against the REAL SqlServer store.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHY THIS EXISTS.</b> Before this class, <c>SqlServerLegalHoldStore</c> had <b>zero test files of any
/// kind</b> — not merely no conformance derivation. Measured across both engines: SqlServer 0, Postgres 0,
/// against a control of 7 apiece for the audit stores. A legal hold is the control that stops erasure of
/// data under litigation hold; a defect in its query predicates is the difference between honouring a
/// preservation order and destroying evidence, and nothing exercised it.
/// </para>
/// <para>
/// <b>What a green run here does and does not prove.</b> It proves this store satisfies the arms the kit
/// declares, against a real database engine. It does <b>not</b> prove the kit's arms are sufficient — the
/// kit is the contract, and a contract can be incomplete. Two arms below name a tenant filter; whether
/// they detect a cross-tenant disclosure rather than merely exercising the parameter is a property of the
/// kit, not of this class, and is not asserted here.
/// </para>
/// <para>
/// <b>Every arm is surfaced deliberately.</b> The kit's arms are inherited wholesale and each is wrapped as
/// a <c>[Fact]</c> below, so adding an arm to the kit does not silently skip this provider — an un-wrapped
/// arm becomes a visible omission in this file rather than an absence nobody can see. That is the same
/// reasoning the audit-store conformance classes record, and it is the property that makes the count
/// checkable rather than trusted.
/// </para>
/// <para>
/// <b>No hand-written DDL, deliberately.</b> The store self-initialises its schema
/// (<c>AutoCreateSchema</c>), so this fixture asks the production code to create the table rather than
/// declaring a copy of it. A fixture that restates the schema can drift from the shipped one in either
/// direction: stale, and the suite fails loudly on a column it no longer has; <i>ahead</i>, and the suite
/// passes green against a schema no consumer will ever provision — concealing the very defect it was
/// written to catch. Letting the store own its own DDL makes that class of divergence inexpressible here.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerLegalHoldStoreConformanceTests : LegalHoldStoreConformanceTestKit
{
	private readonly SqlServerFixture _fixture;

	public SqlServerLegalHoldStoreConformanceTests(SqlServerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override ILegalHoldStore CreateStore()
	{
		var options = new SqlServerLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			TableName = "LegalHolds",
			CommandTimeoutSeconds = 30,

			// The store provisions its own table. See the class remarks: a fixture-declared copy of the
			// schema is the drift hazard this avoids.
			AutoCreateSchema = true
		};

		// Fully qualified: this file's namespace makes a bare `Options` bind to Excalibur.Dispatch.Options.
		return new SqlServerLegalHoldStore(
			Microsoft.Extensions.Options.Options.Create(options),
			EnabledTestLogger.Create<SqlServerLegalHoldStore>());
	}

	#region Save

	[Fact]
	public Task SaveHoldAsync_ShouldPersistHold_Test() => SaveHoldAsync_ShouldPersistHold();

	[Fact]
	public Task SaveHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() =>
		SaveHoldAsync_NullHold_ShouldThrowArgumentNullException();

	[Fact]
	public Task SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException_Test() =>
		SaveHoldAsync_DuplicateHoldId_ShouldThrowInvalidOperationException();

	#endregion Save

	#region Update

	[Fact]
	public Task UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue_Test() =>
		UpdateHoldAsync_ExistingHold_ShouldUpdateAndReturnTrue();

	[Fact]
	public Task UpdateHoldAsync_NonExistent_ShouldReturnFalse_Test() =>
		UpdateHoldAsync_NonExistent_ShouldReturnFalse();

	[Fact]
	public Task UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException_Test() =>
		UpdateHoldAsync_NullHold_ShouldThrowArgumentNullException();

	#endregion Update

	#region Get

	[Fact]
	public Task GetHoldAsync_ExistingHold_ShouldReturnHold_Test() => GetHoldAsync_ExistingHold_ShouldReturnHold();

	[Fact]
	public Task GetHoldAsync_NonExistent_ShouldReturnNull_Test() => GetHoldAsync_NonExistent_ShouldReturnNull();

	#endregion Get

	#region Query — the predicates that had no coverage at all

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching_Test() =>
		GetActiveHoldsForDataSubjectAsync_ActiveHolds_ShouldReturnMatching();

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException_Test() =>
		GetActiveHoldsForDataSubjectAsync_NullDataSubjectIdHash_ShouldThrowArgumentException();

	[Fact]
	public Task GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		GetActiveHoldsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching_Test() =>
		GetActiveHoldsForTenantAsync_ActiveTenantHolds_ShouldReturnMatching();

	[Fact]
	public Task GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException_Test() =>
		GetActiveHoldsForTenantAsync_NullTenantId_ShouldThrowArgumentException();

	[Fact]
	public Task ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc_Test() =>
		ListActiveHoldsAsync_AllActive_ShouldReturnNonExpiredOrderedByCreatedAtDesc();

	[Fact]
	public Task ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		ListActiveHoldsAsync_WithTenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll_Test() =>
		ListAllHoldsAsync_IncludesReleasedHolds_ShouldReturnAll();

	[Fact]
	public Task ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly_Test() =>
		ListAllHoldsAsync_DateRangeFilters_ShouldFilterCorrectly();

	#endregion Query

	#region Expiry — the arms that decide whether a hold still preserves

	[Fact]
	public Task GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration_Test() =>
		GetExpiredHoldsAsync_ShouldReturnActiveHoldsWithPassedExpiration();

	[Fact]
	public Task GetExpiredHoldsAsync_ShouldExcludeReleasedHolds_Test() =>
		GetExpiredHoldsAsync_ShouldExcludeReleasedHolds();

	#endregion Expiry
}
