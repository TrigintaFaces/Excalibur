// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Binds the mhr0kh fix: a self-provisioning compliance store's startup check must distinguish a
/// correctly-shaped table from a wrong-shaped one of the same name -- an existence check alone passes on
/// exactly the input it exists to reject.
/// </summary>
/// <remarks>
/// <para>
/// Each arm auto-creates a fresh table (proving the shipped shape is correct), drops one column the
/// store's own statements bind (simulating a consumer's hand-provisioned, stale-copied schema), then
/// constructs a NEW store instance -- simulating a process restart -- with <c>AutoCreateSchema = true</c>.
/// That is deliberate: <c>CREATE TABLE IF NOT EXISTS</c> only creates tables that are absent, so the
/// auto-create path is the one a wrong-shaped table survives unless the shape check also runs there.
/// Before this fix, two of these three stores' shape check ran ONLY on the <c>AutoCreateSchema = false</c>
/// path; this suite pins the auto-create path specifically, not the already-covered verify-disabled path.
/// </para>
/// <para>
/// Safety is paired with liveness: a store that refused every table, correctly-shaped or not, would
/// satisfy every safety arm here and be useless. Each store gets one liveness arm confirming an untouched,
/// correctly-shaped table still initializes and answers a read.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresComplianceSchemaShapeShould : IntegrationTestBase
{
	private readonly PostgresFixture _fixture;

	public PostgresComplianceSchemaShapeShould(PostgresFixture fixture) => _fixture = fixture;

	// ---------- PostgresDataInventoryStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenDataInventoryRegistrationsTableIsMissingAColumn()
	{
		var (registrations, discovered) = InventoryTableNames();
		using (var provisioner = CreateInventoryStore(registrations, discovered, autoCreate: true))
		{
			_ = await provisioner.GetAllRegistrationsAsync(TestCancellationToken);
		}

		await DropColumnAsync(registrations, "description");

		using var restarted = CreateInventoryStore(registrations, discovered, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetAllRegistrationsAsync(TestCancellationToken));

		thrown.Message.Contains("description", StringComparison.Ordinal).ShouldBeTrue(
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

	// ---------- PostgresErasureStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenErasureRequestsTableIsMissingAColumn()
	{
		var (requests, certificates) = ErasureTableNames();
		using (var provisioner = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			_ = await provisioner.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await DropColumnAsync(requests, "error_message");

		using var restarted = CreateErasureStore(requests, certificates, autoCreate: true);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			() => restarted.GetStatusAsync(Guid.NewGuid(), TestCancellationToken));

		thrown.Message.Contains("error_message", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenErasureTablesAreCorrectlyShaped()
	{
		var (requests, certificates) = ErasureTableNames();
		using var store = CreateErasureStore(requests, certificates, autoCreate: true);

		var status = await store.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);

		status.ShouldBeNull("no request was ever saved under this id; the READ must succeed with no result.");
	}

	// ---------- PostgresLegalHoldStore ----------

	[Fact]
	public async Task FailOnTheAutoCreatePath_WhenLegalHoldTableIsMissingAColumn()
	{
		var table = LegalHoldTableName();
		using (var provisioner = CreateLegalHoldStore(table, autoCreate: true))
		{
			_ = await provisioner.GetHoldAsync(Guid.NewGuid(), TestCancellationToken);
		}

		await DropColumnAsync(table, "case_reference");

		using var restarted = CreateLegalHoldStore(table, autoCreate: true);
		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => restarted.GetHoldAsync(Guid.NewGuid(), TestCancellationToken));

		thrown.Message.Contains("case_reference", StringComparison.Ordinal).ShouldBeTrue(
			$"the failure must name the missing column so an operator can act. Message was: {thrown.Message}");
	}

	[Fact]
	public async Task StartAndAnswerARead_WhenLegalHoldTableIsCorrectlyShaped()
	{
		var table = LegalHoldTableName();
		using var store = CreateLegalHoldStore(table, autoCreate: true);

		var hold = await store.GetHoldAsync(Guid.NewGuid(), TestCancellationToken);

		hold.ShouldBeNull("no hold was ever saved under this id; the READ must succeed with no result.");
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

		await DropColumnAsync(requests, "tenant_partition");

		using var restarted = CreateErasureStore(requests, certificates, autoCreate);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await restarted.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("tenant_partition");
	}

	/// <summary>
	/// A store that provisions its own schema must recognise its own work on the NEXT startup. Postgres
	/// folds an unquoted identifier to lower case before storing it, so a guard comparing an unfolded
	/// literal against a stored constraint name finds nothing, re-adds the constraint, and fails 42710 --
	/// a store that comes up exactly once and then never opens again.
	/// </summary>
	/// <remarks>
	/// The two shape arms above RED-detect this incidentally, because their table-name helper happens to
	/// upper-case. This arm states the property directly so it survives that helper changing: a
	/// consumer-chosen table name carrying an upper-case letter -- RequestsTableName is documented as
	/// configurable -- must not turn the second startup into a hard failure. The name here is short on
	/// purpose, so truncation is not involved and CASE is the only variable.
	/// </remarks>
	/// <returns>The arm.</returns>
	[Fact]
	public async Task OpenASecondTime_WhenTheErasureTableNameCarriesUpperCase()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"EraReqCase_{suffix}";
		var certificates = $"EraCertCase_{suffix}";

		using (var first = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			_ = await first.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);
		}

		// THE STATE UNDER TEST: everything the first startup created is already there, including the
		// claim-pairing constraint, and the second startup must not try to create it again.
		using var second = CreateErasureStore(requests, certificates, autoCreate: true);

		var status = await second.GetStatusAsync(Guid.NewGuid(), TestCancellationToken);

		status.ShouldBeNull(
			"no request was ever saved under this id, so the read must SUCCEED with no result. Asserting "
			+ "the read rather than merely that the constructor did not throw is what makes this arm "
			+ "liveness as well as safety: a store that threw on every read would satisfy a no-throw "
			+ "assertion on provisioning alone.");
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

	// ---------- helpers ----------

	// Each arm gets its own table pair/name, derived from the calling test, so arms never collide over a
	// dropped column another arm still needs present.
	//
	// Case is irrelevant to these identifiers and must stay uppercase-normalized: the store's DDL and the
	// ALTER below both interpolate this SAME string into a QUOTED identifier, and PostgreSQL preserves the
	// case of a quoted identifier exactly -- so both sides agree whatever the case, and normalizing upward
	// is the direction that round-trips safely.
	private static (string Registrations, string Discovered) InventoryTableNames(
		[CallerMemberName] string armName = "") =>
		($"inv_reg_{armName}".ToUpperInvariant(), $"inv_loc_{armName}".ToUpperInvariant());

	private static (string Requests, string Certificates) ErasureTableNames(
		[CallerMemberName] string armName = "") =>
		($"era_req_{armName}".ToUpperInvariant(), $"era_cert_{armName}".ToUpperInvariant());

	private static string LegalHoldTableName([CallerMemberName] string armName = "") =>
		$"hold_{armName}".ToUpperInvariant();

	private async Task DropColumnAsync(string tableName, string columnName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$@"ALTER TABLE ""compliance"".""{tableName}"" DROP COLUMN ""{columnName}""",
			cancellationToken: TestCancellationToken));
	}

	private PostgresDataInventoryStore CreateInventoryStore(
		string registrationsTable, string discoveredTable, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = registrationsTable,
			DiscoveredLocationsTableName = discoveredTable,
			AutoCreateSchema = autoCreate,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<PostgresDataInventoryStore>(),
		UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	/// <summary>
	/// SAFETY. A mint ledger whose compared columns carry no explicit collation is REFUSED, because their
	/// determinism is then a property of the database rather than of the column.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This engine is narrower than its SQL Server sibling and the arm says so rather than claiming a
	/// folding default: equality under a DETERMINISTIC collation is byte equality here, so a database
	/// provisioned from the shipped create script -- which collates these columns <c>"C"</c> explicitly --
	/// cannot fold. What is refused is the hand-provisioned table that names no collation, where nothing
	/// readable off the column establishes that its comparisons agree with the ordinal comparison the
	/// framework performs on the same values.
	/// </para>
	/// <para>
	/// RED input: accept the <c>default</c> pseudo-collation in the verifier, or drop the column from the
	/// table's contract. Startup then accepts a ledger whose equality semantics we have not established.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when startup has refused the uncollated table.</returns>
	[Fact]
	public async Task RefuseAMintLedgerWhoseComparedColumnsCarryNoExplicitCollation()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"erareq_{suffix}";
		await PreProvisionMintLedgerAsync(requests, collate: false, primaryKeyColumns: "key_handle", withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"eracert_{suffix}", autoCreate: true);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await store.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("deterministic collation");
	}

	/// <summary>
	/// SAFETY. A mint ledger with no index covering (tenant_id, data_subject_id_hash) is refused, because
	/// that pair is the only key a data subject's handles can be found by.
	/// </summary>
	/// <remarks>
	/// <para>
	/// A handle is an opaque digest, so there is no second route to a subject's rows. The shipped create
	/// script validates this index on an existing table; until this change the runtime path did not, so
	/// the two definitions of "compatible" disagreed about the same database.
	/// </para>
	/// <para>
	/// <b>AutoCreateSchema is FALSE here, and that is a measurement rather than a preference.</b> This
	/// engine's create path issues CREATE INDEX IF NOT EXISTS unconditionally, so with creation enabled
	/// it REPAIRS a missing index before verification looks -- which is good behaviour and makes the
	/// auto-create path the wrong place to assert the refusal. Its SQL Server sibling does not repair
	/// one, which is why both twins pin the verify-only path: it is the configuration where nothing can
	/// intervene on either engine, so the two arms measure the same property.
	/// </para>
	/// </remarks>
	/// <returns>A task that completes when startup has refused the unindexed table.</returns>
	[Fact]
	public async Task RefuseAMintLedgerWithNoEnumerationIndex()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"erareq_{suffix}";
		var certificates = $"eracert_{suffix}";
		using (var provisioner = CreateErasureStore(requests, certificates, autoCreate: true))
		{
			await provisioner.ValidateSchemaAsync(TestCancellationToken);
		}

		await DropSubjectIndexAsync(requests);

		using var restarted = CreateErasureStore(requests, certificates, autoCreate: false);
		var thrown = await Should.ThrowAsync<ErasureStoreNotProvisionedException>(
			async () => await restarted.ValidateSchemaAsync(TestCancellationToken));

		thrown.Message.ShouldContain("tenant_id, data_subject_id_hash");
	}

	private async Task DropSubjectIndexAsync(string requestsTable)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			$@"DROP INDEX ""compliance"".""ix_{MintTableFor(requestsTable)}_subject""",
			cancellationToken: TestCancellationToken));
	}

	/// <summary>
	/// SAFETY. A mint ledger keyed on more than the handle is refused, because the handle alone is what
	/// makes recording a mint a single-row decision.
	/// </summary>
	/// <remarks>
	/// The insert names the handle as its conflict target, so a wider key is not merely untidy -- it makes
	/// the statement illegal, and the failure would otherwise arrive on a personal-data write rather than
	/// at startup. Keyed on (key_handle, tenant_id) the same handle could also hold two rows under two
	/// owners, which is what the owner guard exists to refuse.
	/// </remarks>
	/// <returns>A task that completes when startup has refused the over-wide key.</returns>
	[Fact]
	public async Task RefuseAMintLedgerKeyedOnMoreThanTheHandle()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"erareq_{suffix}";
		await PreProvisionMintLedgerAsync(requests, collate: true, primaryKeyColumns: "key_handle, tenant_id", withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"eracert_{suffix}", autoCreate: true);
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
	/// table it did not itself create -- which is the whole population the check exists for.
	/// </remarks>
	/// <returns>A task that completes when the correctly-shaped table has been accepted and used.</returns>
	[Fact]
	public async Task AcceptAndUseAHandProvisionedMintLedgerOfTheShippedShape()
	{
		var suffix = Guid.NewGuid().ToString("N")[..8];
		var requests = $"erareq_{suffix}";
		await PreProvisionMintLedgerAsync(requests, collate: true, primaryKeyColumns: "key_handle", withSubjectIndex: true);

		using var store = CreateErasureStore(requests, $"eracert_{suffix}", autoCreate: true);
		await store.ValidateSchemaAsync(TestCancellationToken);

		var owner = new TenantId("liveness-owner");
		await store.RecordMintIntentAsync("handle-liveness", owner, "subject-liveness", TestCancellationToken);
		await store.MarkMintedAsync("handle-liveness", TestCancellationToken);

		var snapshot = await store.ListHandlesAsync(owner, "subject-liveness", TestCancellationToken);
		snapshot.Minted.ShouldBe(["handle-liveness"]);
		snapshot.PendingMint.ShouldBeEmpty();
	}

	// Each arm's mint ledger, created BY THE TEST rather than by the store, with one representation
	// property parameterised. Creating it ahead of the store is what reaches the case the check exists
	// for: CREATE TABLE IF NOT EXISTS only creates tables that are absent, so a table that already exists
	// keeps whatever shape it was given.
	private async Task PreProvisionMintLedgerAsync(
		string requestsTable,
		bool collate,
		string primaryKeyColumns,
		bool withSubjectIndex)
	{
		var mint = MintTableFor(requestsTable);
		var collation = collate ? @" COLLATE ""C""" : string.Empty;
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		await connection.OpenAsync(TestCancellationToken);

		_ = await connection.ExecuteAsync(new CommandDefinition(
			@"CREATE SCHEMA IF NOT EXISTS ""compliance""",
			cancellationToken: TestCancellationToken));

		_ = await connection.ExecuteAsync(new CommandDefinition(
			$@"CREATE TABLE ""compliance"".""{mint}"" (
				key_handle TEXT{collation} NOT NULL,
				tenant_id TEXT{collation} NOT NULL,
				data_subject_id_hash TEXT{collation} NOT NULL,
				intent_at TIMESTAMPTZ NOT NULL,
				minted_at TIMESTAMPTZ NULL,
				PRIMARY KEY ({primaryKeyColumns})
			)",
			cancellationToken: TestCancellationToken));

		if (withSubjectIndex)
		{
			_ = await connection.ExecuteAsync(new CommandDefinition(
				$@"CREATE INDEX ix_{mint}_subject ON ""compliance"".""{mint}"" (tenant_id, data_subject_id_hash)",
				cancellationToken: TestCancellationToken));
		}
	}

	private static string MintTableFor(string requestsTable) => SiblingTable(requestsTable, "minted");

	/// <summary>
	/// Derives a sibling table's name from the requests table's, shortening the base so the result fits
	/// PostgreSQL's identifier limit.
	/// </summary>
	/// <remarks>
	/// NOT just string concatenation, and the reason is a failure this harness actually produced.
	/// PostgreSQL truncates an identifier at 63 bytes, and <see cref="ErasureTableNames"/> yields names
	/// that are already exactly that long -- so a suffixed name truncated back to the REQUESTS table's
	/// own name. Every sibling then resolved to the requests table: the create guarded on existence and
	/// skipped, and the destruction index was created against a table with no key_handle column. The
	/// index name derived from these is suffixed too, so the base has to leave room for the longest of
	/// them rather than for this one.
	/// </remarks>
	/// <param name="requestsTable">The arm's requests table name.</param>
	/// <param name="suffix">The sibling's role, appended after an underscore.</param>
	/// <returns>An identifier PostgreSQL will not truncate.</returns>
	private static string SiblingTable(string requestsTable, string suffix)
	{
		// 63 is the engine's limit; the margin covers the "ix_" prefix and "_subject" suffix the store
		// adds when it derives an INDEX name from the table name this returns.
		const int Budget = 63 - 3 - 8;
		var room = Budget - suffix.Length - 1;
		var baseName = requestsTable.Length <= room ? requestsTable : requestsTable[..room];
		return $"{baseName}_{suffix}";
	}

	private PostgresErasureStore CreateErasureStore(
		string requestsTable,
		string certificatesTable,
		bool autoCreate,
		ITenantContext? tenantContext = null) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = requestsTable,
			CertificatesTableName = certificatesTable,
			// Named PER ARM, like the tables beside it. Falling back to the shipped default made one
			// shared table the subject of every arm in every class, so an arm provisioning a deliberately
			// wrong one would have broken all of them.
			DestroyedKeysTableName = SiblingTable(requestsTable, "destroyed"),
			DestructionIntentsTableName = SiblingTable(requestsTable, "intents"),
			MintedKeysTableName = MintTableFor(requestsTable),
			AutoCreateSchema = autoCreate,
		}),
		ConformanceDataSubjectHasher.Instance,
		EnabledTestLogger.Create<PostgresErasureStore>(),
		tenantContext ?? UntenantedContext.Instance,
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = false }));

	private PostgresLegalHoldStore CreateLegalHoldStore(string table, bool autoCreate) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			TableName = table,
			AutoCreateSchema = autoCreate,
		}),
		EnabledTestLogger.Create<PostgresLegalHoldStore>(),
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
