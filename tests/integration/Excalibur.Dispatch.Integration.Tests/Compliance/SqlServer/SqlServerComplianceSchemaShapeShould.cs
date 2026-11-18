// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Data.SqlClient;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the mhr0kh fix for <see cref="SqlServerDataInventoryStore"/>: startup schema verification must
/// distinguish a correctly-shaped table from a wrong-shaped one of the same name, on BOTH initialization
/// paths -- an existence check (or a shape check wired to only one path) passes on exactly the input it
/// exists to reject. Postgres twin: <c>PostgresComplianceSchemaShapeShould</c>.
/// </summary>
/// <remarks>
/// <para>
/// Each safety arm auto-creates a fresh table (proving the shipped shape is correct), drops one column
/// the store's own statements bind (simulating a consumer's hand-provisioned, stale-copied schema), then
/// constructs a NEW store instance -- simulating a process restart -- with <c>AutoCreateSchema = true</c>.
/// That is deliberate: <c>CREATE TABLE IF NOT EXISTS</c>-equivalent guarding only creates tables that are
/// absent, so the auto-create path is the one a wrong-shaped table survives unless the shape check also
/// runs there.
/// </para>
/// <para>
/// Safety is paired with liveness: a store that refused every table, correctly-shaped or not, would
/// satisfy every safety arm here and be useless. Each table pair gets one liveness arm confirming an
/// untouched, correctly-shaped table still initializes and answers a read.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerComplianceSchemaShapeShould : IntegrationTestBase
{
	private readonly SqlServerFixture _fixture;

	public SqlServerComplianceSchemaShapeShould(SqlServerFixture fixture) => _fixture = fixture;

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenDataInventoryRegistrationsTableIsMissingAColumn()
	{
		var (registrations, discovered) = InventoryTableNames();
		using (var provisioner = CreateInventoryStore(registrations, discovered, autoCreate: true))
		{
			_ = await provisioner.GetAllRegistrationsAsync(TestCancellationToken);
		}

		await DropColumnAsync(registrations, "Description");

		using var restarted = CreateInventoryStore(registrations, discovered, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetAllRegistrationsAsync(TestCancellationToken));

		thrown.Message.Contains("Description", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenDataInventoryTablesAreCorrectlyShaped()
	{
		var (registrations, discovered) = InventoryTableNames();
		using var store = CreateInventoryStore(registrations, discovered, autoCreate: true);

		var result = await store.GetAllRegistrationsAsync(TestCancellationToken);

		result.ShouldNotBeNull();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public async Task RejectAnErasureSchemaWithoutTenantPartition(bool autoCreate)
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReq_{suffix}";
		var certificates = $"EraCert_{suffix}";
		using (var provisioner = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			await provisioner.ValidateSchemaAsync(TestCancellationToken);
		}

		await DropColumnAsync(requests, "TenantPartition");

		using var restarted = CreateErasureStore(requests, certificates, autoCreate);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await restarted.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("TenantPartition");
	}

	[Fact]
	public async Task PersistOwnerAndPartitionSeparatelyInAnAutoCreatedErasureSchema()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		using var store = CreateErasureStore(
			$"EraReq_{suffix}",
			$"EraCert_{suffix}",
			autoCreate: true,
			tenantContext: new NamedOwnerContext("explicit-key-owner"));
		var request = new ErasureRequest
		{
			DataSubjectId = "subject-for-schema-roundtrip",
			IdType = DataSubjectIdType.UserId,
			// DELIBERATELY UNSET. The owner now comes from the ambient context below; naming a tenant
			// here that disagrees with the resolved one is refused outright, because an erasure derives
			// its key handle from the owner and a handle the write path never used would destroy nothing
			// while reporting a completed erasure. The separation this arm is about is unchanged -- it is
			// just expressed where the framework actually takes it from.
			LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
			RequestedBy = "schema-test",
		};

		await store.SaveRequestAsync(request, DateTimeOffset.UtcNow, TestCancellationToken);
		var status = await store.GetStatusAsync(request.RequestId, TestCancellationToken);

		status.ShouldNotBeNull();
		status.TenantId.ShouldBe("explicit-key-owner");
		status.TenantPartition.ShouldBe(KeyedTenantPartition.Untenanted);
	}

	/// <summary>
	/// SAFETY, and the one with no symptom. A mint ledger whose compared columns inherited this server's
	/// case-insensitive default collation is REFUSED at startup, rather than folding one tenant's rows
	/// into another's enumeration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The table has every column name the store's statements bind, the right primary key and the right
	/// index. Only the collation differs, and a check over column names cannot see it -- which is what
	/// made this reachable: the store's CREATE TABLE supplies a binary collation, but it supplies it only
	/// when the table is ABSENT, so a hand-provisioned or inherited table keeps the server default. That
	/// default is case-INSENSITIVE on SQL Server.
	/// </para>
	/// <para>
	/// What follows if startup accepts it: an erasure enumerates a data subject's handles with
	/// <c>WHERE TenantId = @TenantId AND DataSubjectIdHash = @SubjectHash</c>. Under a folding collation a
	/// different owner's rows satisfy that predicate, every handle returned is added to the destruction
	/// set, and the keys are destroyed. The requesting tenant's certificate then reports a completed
	/// erasure while ANOTHER tenant's data has been rendered permanently unreadable, and nothing raises.
	/// </para>
	/// <para>
	/// RED input: drop the collation check from the verifier's table contract, or widen its predicate to
	/// accept a non-binary collation. The store then initializes over this table and the assertion fails.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when startup has refused the folding table.</returns>
	[Fact]
	public async Task RefuseAMintLedgerThatComparesCaseInsensitively()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReq_{suffix}";
		await PreProvisionMintLedgerAsync(
			requests,
			collation: "SQL_Latin1_General_CP1_CI_AS",
			primaryKeyColumns: "KeyHandle",
			withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"EraCert_{suffix}", autoCreate: true);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await store.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("binary collation");
		thrown.Message.ShouldContain("SQL_Latin1_General_CP1_CI_AS");
	}

	/// <summary>
	/// SAFETY. A mint ledger with no index covering (TenantId, DataSubjectIdHash) is refused, because that
	/// pair is the only key a data subject's handles can be found by.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A handle is an opaque digest, so there is no second route to a subject's rows. The shipped create
	/// script validates this index on an existing table and its error says the erasure "cannot work without
	/// it"; until this change the runtime path left that sentence unenforced on exactly the tables
	/// validation exists for.
	/// </para>
	/// <para>
	/// <b>AutoCreateSchema is FALSE here so the twin arms measure the same property.</b> This engine's
	/// create path puts CREATE INDEX inside the table-absent block, so it cannot repair a missing index on
	/// an existing table -- but the Postgres sibling's runs unconditionally and DOES repair one. The
	/// verify-only path is the configuration where nothing intervenes on either engine.
	/// </para>
	/// <para>
	/// RED input: remove the enumeration key from this table's contract. Startup then accepts a ledger
	/// every erasure must scan end to end.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when startup has refused the unindexed table.</returns>
	[Fact]
	public async Task RefuseAMintLedgerWithNoEnumerationIndex()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReq_{suffix}";
		var certificates = $"EraCert_{suffix}";
		using (var provisioner = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			await provisioner.ValidateSchemaAsync(TestCancellationToken);
		}

		await DropSubjectIndexAsync(requests);

		using var restarted = CreateErasureStore(requests, certificates, autoCreate: false);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await restarted.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("TenantId, DataSubjectIdHash");
	}

	private async Task DropSubjectIndexAsync(string requestsTable)
	{
		var mint = MintTableFor(requestsTable);
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$"DROP INDEX IX_{mint}_Subject ON [compliance].[{mint}]",
			cancellationToken: TestCancellationToken));
	}

	/// <summary>
	/// SAFETY. A mint ledger keyed on more than the handle is refused, because the handle alone is what
	/// makes recording a mint a single-row decision.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The insert records the intent to mint before the key exists, and it must either create the row or
	/// find the one already there. Keyed on (KeyHandle, TenantId) the same handle can hold two rows under
	/// two owners, so the guard that refuses a handle already owned by someone else has nothing to refuse
	/// against -- and the confirmation that moves a row from pending to minted can land on either of them.
	/// </para>
	/// <para>
	/// RED input: compare the primary key as a SET rather than in order, or drop the check. A key of
	/// (KeyHandle, TenantId) then passes.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when startup has refused the over-wide key.</returns>
	[Fact]
	public async Task RefuseAMintLedgerKeyedOnMoreThanTheHandle()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReq_{suffix}";
		await PreProvisionMintLedgerAsync(
			requests,
			collation: "Latin1_General_BIN2",
			primaryKeyColumns: "KeyHandle, TenantId",
			withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"EraCert_{suffix}", autoCreate: true);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await store.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("primary key");
	}

	/// <summary>
	/// LIVENESS, and it is what keeps the three arms above from being satisfiable by a store that refuses
	/// everything. A hand-provisioned mint ledger of the shipped shape starts and records a mint.
	/// </summary>
	/// <remarks>
	/// The table is created by this test rather than by the store, so it proves the verifier accepts a
	/// table it did not itself create -- which is the whole population the check exists for. A store that
	/// could only accept its own output would pass every safety arm and be useless to a consumer who
	/// provisions out of band.
	/// </remarks>
	/// <returns>A task that completes when the correctly-shaped table has been accepted and used.</returns>
	[Fact]
	public async Task AcceptAndUseAHandProvisionedMintLedgerOfTheShippedShape()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReq_{suffix}";
		await PreProvisionMintLedgerAsync(
			requests,
			collation: "Latin1_General_BIN2",
			primaryKeyColumns: "KeyHandle",
			withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"EraCert_{suffix}", autoCreate: true);
		await store.ValidateSchemaAsync(TestCancellationToken);

		var owner = new TenantId("liveness-owner");
		await store.RecordMintIntentAsync("handle-liveness", owner, "subject-liveness", TestCancellationToken);
		await store.MarkMintedAsync("handle-liveness", TestCancellationToken);

		var snapshot = await store.ListHandlesAsync(owner, "subject-liveness", TestCancellationToken);
		snapshot.Minted.ShouldBe(["handle-liveness"]);
		snapshot.PendingMint.ShouldBeEmpty();
	}

	// ---------- helpers ----------

	// Each arm gets its own table pair, derived from the calling test, so arms never collide over a
	// dropped column another arm still needs present.
	private static (string Registrations, string Discovered) InventoryTableNames(
		[CallerMemberName] string armName = "") =>
		($"InvReg{armName}", $"InvLoc{armName}");

	private async Task DropColumnAsync(string tableName, string columnName)
	{
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$"ALTER TABLE [compliance].[{tableName}] DROP COLUMN [{columnName}]",
			cancellationToken: TestCancellationToken));
	}

	// The mint ledger is named PER ARM, like the destruction tables beside it. It used to fall back to
	// the shipped default, which made one shared table the subject of every arm in every class -- and an
	// arm that provisions a deliberately wrong one would then have broken all of them.
	private SqlServerErasureStore CreateErasureStore(
		string requestsTable,
		string certificatesTable,
		bool autoCreate,
		ITenantContext? tenantContext = null) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = requestsTable,
			CertificatesTableName = certificatesTable,
			DestroyedKeysTableName = $"{requestsTable}_Destroyed",
			DestructionIntentsTableName = $"{requestsTable}_Intents",
			MintedKeysTableName = MintTableFor(requestsTable),
			AutoCreateSchema = autoCreate,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<SqlServerErasureStore>(),
		tenantContext ?? UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	// Each arm's mint ledger, created BY THE TEST rather than by the store, with one representation
	// property parameterised. Creating it ahead of the store is what reaches the case the check exists
	// for: creation guards on the table being absent, so a table that already exists keeps whatever shape
	// it was given -- including the server's case-insensitive default collation.
	private async Task PreProvisionMintLedgerAsync(
		string requestsTable,
		string collation,
		string primaryKeyColumns,
		bool withSubjectIndex)
	{
		var mint = MintTableFor(requestsTable);
		await using var connection = new SqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			"IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'compliance') EXEC('CREATE SCHEMA [compliance]')",
			cancellationToken: TestCancellationToken));

		_ = await connection.ExecuteAsync(new CommandDefinition(
			$@"CREATE TABLE [compliance].[{mint}] (
				KeyHandle NVARCHAR(256) COLLATE {collation} NOT NULL,
				TenantId NVARCHAR(256) COLLATE {collation} NOT NULL,
				DataSubjectIdHash NVARCHAR(256) COLLATE {collation} NOT NULL,
				IntentAt DATETIMEOFFSET NOT NULL,
				MintedAt DATETIMEOFFSET NULL,
				CONSTRAINT PK_{mint} PRIMARY KEY ({primaryKeyColumns})
			)",
			cancellationToken: TestCancellationToken));

		if (withSubjectIndex)
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$"CREATE INDEX IX_{mint}_Subject ON [compliance].[{mint}] (TenantId, DataSubjectIdHash)",
				cancellationToken: TestCancellationToken));
		}
	}

	private static string MintTableFor(string requestsTable) => $"{requestsTable}_Minted";

	private SqlServerDataInventoryStore CreateInventoryStore(
		string registrationsTable, string discoveredTable, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = registrationsTable,
			DiscoveredLocationsTableName = discoveredTable,
			AutoCreateSchema = autoCreate,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<SqlServerDataInventoryStore>(),
		UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// A tenant context that reports one fixed identity, so an arm can establish an OWNER that differs
	/// from the storage PARTITION.
	/// </summary>
	/// <remarks>
	/// The two are genuinely different axes and the default single-tenant deployment proves it: the owner
	/// is the context's identity and the partition is the untenanted sentinel whenever multi-tenancy is
	/// off. An arm that wants to see them persisted separately has to supply the owner the way the
	/// framework takes it -- from the ambient context -- because naming it on the request is refused.
	/// </remarks>
	/// <param name="owner">The identity this context reports.</param>
	private sealed class NamedOwnerContext(string owner) : ITenantContext
	{
		public string? TenantId => owner;

		public bool HasTenant => true;
	}
}
