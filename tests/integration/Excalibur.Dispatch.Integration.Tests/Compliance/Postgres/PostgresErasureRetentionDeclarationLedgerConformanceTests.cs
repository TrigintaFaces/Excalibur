// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.Postgres.Erasure;
using Excalibur.Testing.Conformance;


namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Derives the retention-declaration ledger contract against real PostgreSQL.
/// </summary>
/// <remarks>
/// <para>
/// <b>Never skip-gated, because the properties under test are SUBSTRATE behaviour.</b> Whether an
/// insert-if-absent is atomic, whether a duplicate is a no-op, whether a tombstone survives a later
/// advance and whether two types differing only in case are two rows are all answered by the engine and
/// its collation — a fake would answer them the way the fake's author expected, which is exactly the
/// agreement that makes a mocked lock worthless here.
/// </para>
/// <para>
/// The ledger and the withdrawal authority are constructed separately, over the same options, which is
/// also how a host gets them: the withdrawal authority is deliberately NOT part of the erasure store's
/// registration, so that withdrawing a declaration is not expressible by the code that deploys one.
/// </para>
/// <para>
/// The erasure store provisions the table, so this suite declares no DDL of its own. A fixture-declared
/// copy of a shipped table is the drift this subsystem has already paid for; letting the store own its
/// own DDL makes that divergence inexpressible here.
/// </para>
/// </remarks>
[Collection(ContainerCollections.Postgres)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class PostgresErasureRetentionDeclarationLedgerConformanceTests
	: ErasureRetentionDeclarationLedgerConformanceTestKit, IAsyncLifetime
{
	private readonly PostgresFixture _fixture;
	private readonly string _suffix = $"rdl_{Guid.NewGuid():N}"[..16];

	public PostgresErasureRetentionDeclarationLedgerConformanceTests(PostgresFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// Provisions the table once, through the erasure store's own path, before any arm runs.
	/// </summary>
	/// <returns>A task that completes when the schema exists.</returns>
	public async ValueTask InitializeAsync()
	{
		var store = new PostgresErasureStore(
			Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: true)),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<PostgresErasureStore>(),
			TenantContextHolder.AmbientContext,
			tenantContextOptions: Microsoft.Extensions.Options.Options.Create(new TenantContextOptions()));

		// Any funnelled read provisions the schema. Asking for a status is the cheapest one.
		_ = await store.GetStatusAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);
		store.Dispose();
	}

	/// <inheritdoc />
	public ValueTask DisposeAsync() => ValueTask.CompletedTask;

	/// <inheritdoc />
	protected override IErasureRetentionDeclarationLedger CreateLedger() =>
		new PostgresErasureRetentionDeclarationLedger(Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: false)));

	/// <inheritdoc />
	protected override IErasureRetentionWithdrawalAuthority CreateWithdrawalAuthority() =>
		new PostgresErasureRetentionWithdrawalAuthority(Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: false)));

	private PostgresErasureStoreOptions StoreOptions(bool autoCreateSchema) =>
		new()
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",

			// Lower-case deliberately: Postgres folds unquoted identifiers, and matching that convention
			// is part of what distinguishes this fixture from its SqlServer sibling.
			RequestsTableName = $"erasure_requests_{_suffix}",
			CertificatesTableName = $"erasure_certificates_{_suffix}",
			DestroyedKeysTableName = $"erasure_destroyed_keys_{_suffix}",
			DestructionIntentsTableName = $"erasure_destruction_intents_{_suffix}",
			MintedKeysTableName = $"erasure_minted_keys_{_suffix}",
			TokenFingerprintTableName = $"erasure_token_fingerprint_{_suffix}",
			RetentionDeclarationsTableName = $"erasure_retention_declarations_{_suffix}",
			CommandTimeoutSeconds = 30,
			AutoCreateSchema = autoCreateSchema
		};

	#region Advance

	[Fact]
	public Task RecordDeclaredRetentionsAsync_RecordingTheSamePairTwice_ShouldBeANoOp_Test() =>
		RecordDeclaredRetentionsAsync_RecordingTheSamePairTwice_ShouldBeANoOp();

	[Fact]
	public Task RecordDeclaredRetentionsAsync_WithAnAlreadyRecordedPairFirst_ShouldStillRecordTheRest_Test() =>
		RecordDeclaredRetentionsAsync_WithAnAlreadyRecordedPairFirst_ShouldStillRecordTheRest();

	[Fact]
	public Task RecordDeclaredRetentionsAsync_ShouldNotResurrectAWithdrawnDeclaration_Test() =>
		RecordDeclaredRetentionsAsync_ShouldNotResurrectAWithdrawnDeclaration();

	[Fact]
	public Task RecordDeclaredRetentionsAsync_ShouldTreatTypesDifferingOnlyInCaseAsDistinct_Test() =>
		RecordDeclaredRetentionsAsync_ShouldTreatTypesDifferingOnlyInCaseAsDistinct();

	[Fact]
	public Task RecordDeclaredRetentionsAsync_WithNoDeclarations_ShouldRecordNothing_Test() =>
		RecordDeclaredRetentionsAsync_WithNoDeclarations_ShouldRecordNothing();

	[Fact]
	public Task RecordDeclaredRetentionsAsync_WhenOnePairCannotBeRecorded_ShouldRecordTheRestAndFail_Test() =>
		RecordDeclaredRetentionsAsync_WhenOnePairCannotBeRecorded_ShouldRecordTheRestAndFail();

	#endregion Advance

	#region Read

	[Fact]
	public Task ListDeclaredRetentionsAsync_ShouldReturnOnlyTheRequestedTenantsDeclarations_Test() =>
		ListDeclaredRetentionsAsync_ShouldReturnOnlyTheRequestedTenantsDeclarations();

	#endregion Read

	#region Withdrawal

	[Fact]
	public Task WithdrawDeclaredRetentionAsync_ShouldTombstoneRatherThanDelete_Test() =>
		WithdrawDeclaredRetentionAsync_ShouldTombstoneRatherThanDelete();

	[Fact]
	public Task WithdrawDeclaredRetentionAsync_ShouldDistinguishRepeatFromNeverDeclared_Test() =>
		WithdrawDeclaredRetentionAsync_ShouldDistinguishRepeatFromNeverDeclared();

	#endregion Withdrawal

	#region Suite Wiring

	/// <summary>
	/// Fails if this suite stops exposing any arm the kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail — in the results it
	/// is indistinguishable from one that passed.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion Suite Wiring
}
