// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;
using Excalibur.Testing.Conformance;


namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Derives the retention-declaration ledger contract against real SQL Server.
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
[Collection(ContainerCollections.SqlServer)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
public sealed class SqlServerErasureRetentionDeclarationLedgerConformanceTests
	: ErasureRetentionDeclarationLedgerConformanceTestKit, IAsyncLifetime
{
	private readonly SqlServerFixture _fixture;
	private readonly string _suffix = $"Rdl{Guid.NewGuid():N}"[..16];

	public SqlServerErasureRetentionDeclarationLedgerConformanceTests(SqlServerFixture fixture) =>
		_fixture = fixture;

	/// <summary>
	/// Provisions the table once, through the erasure store's own path, before any arm runs.
	/// </summary>
	/// <returns>A task that completes when the schema exists.</returns>
	public async ValueTask InitializeAsync()
	{
		var store = new SqlServerErasureStore(
			Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: true)),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlServerErasureStore>(),
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
		new SqlServerErasureRetentionDeclarationLedger(Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: false)));

	/// <inheritdoc />
	protected override IErasureRetentionWithdrawalAuthority CreateWithdrawalAuthority() =>
		new SqlServerErasureRetentionWithdrawalAuthority(Microsoft.Extensions.Options.Options.Create(StoreOptions(autoCreateSchema: false)));

	private SqlServerErasureStoreOptions StoreOptions(bool autoCreateSchema) =>
		new()
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",

			// PascalCase deliberately: SQL Server does not fold unquoted identifiers, and matching that
			// convention is part of what distinguishes this fixture from its Postgres sibling.
			RequestsTableName = $"ErasureRequests{_suffix}",
			CertificatesTableName = $"ErasureCertificates{_suffix}",
			DestroyedKeysTableName = $"ErasureDestroyedKeys{_suffix}",
			DestructionIntentsTableName = $"ErasureDestructionIntents{_suffix}",
			MintedKeysTableName = $"ErasureMintedKeys{_suffix}",
			TokenFingerprintTableName = $"ErasureTokenFingerprint{_suffix}",
			RetentionDeclarationsTableName = $"ErasureRetentionDeclarations{_suffix}",
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
