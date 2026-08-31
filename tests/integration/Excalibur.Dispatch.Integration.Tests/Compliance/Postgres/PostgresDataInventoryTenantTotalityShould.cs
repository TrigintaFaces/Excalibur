// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Runtime.CompilerServices;

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// Binds the PostgreSQL data-inventory tenant-totality migration against the shape every UPGRADING
/// consumer actually holds — no tenant column, and primary keys with no tenant term.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this suite exists.</b> The tenant discriminator reached these two tables by editing 001 in
/// place. Both provisioning paths guard on table EXISTENCE — <c>CREATE TABLE IF NOT EXISTS</c> and the
/// store's own auto-create — so neither adds a column to a table that is already there. A consumer who
/// upgrades the package keeps the old shape while every statement the new store issues names
/// <c>tenant_id</c>: their disclosure is not closed and their store does not work. Every other suite
/// provisions a FRESH schema, so none of them can observe this.
/// </para>
/// <para>
/// <b>Why the SQL Server twin is not evidence for this engine.</b> The two dialects need genuinely
/// different migrations — SQL Server must state a binary collation to reach the case behaviour Postgres
/// has by default, and its clustered key rebuild has costs this one does not. Two scripts means two
/// chances to be wrong, and a guarantee proved on one engine says nothing about the other.
/// </para>
/// <para>
/// <b>Two defects, two independent arms; safety always paired with liveness.</b> The disclosure is a
/// READ defect closed by the predicate; the overwrite is a WRITE defect closed only by the tenant term
/// entering the PRIMARY KEY. Both are bound here, and each safety arm has a twin asserting the rightful
/// owner is still served — "tenant B sees nothing of tenant A's" is otherwise satisfied by a store that
/// serves nobody.
/// </para>
/// <para>
/// <b>Provisioned and migrated from the scripts the package ships</b>, never from fixture DDL. Arms
/// share the default table names because those are the names the migration targets, so each arm seeds
/// under its own <c>table_name</c> and cannot satisfy another arm's assertion.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresDataInventoryTenantTotalityShould : IntegrationTestBase
{
	private const string Sentinel = "__untenanted__";
	private const string SubjectId = "subject-inventory-totality";

	private readonly PostgresFixture _fixture;

	public PostgresDataInventoryTenantTotalityShould(PostgresFixture fixture) => _fixture = fixture;

	/// <summary>
	/// THE FAIL-FAST ARM. Against a database that still has the legacy shape, the store must refuse to
	/// start and say which script repairs it.
	/// </summary>
	/// <remarks>
	/// Before the check this binds, both provisioning paths reported success on this database — verify
	/// asked only whether the TABLE existed, and auto-create skips a table that is already there. The
	/// store then initialized cleanly and died on first use with a raw provider error about an unknown
	/// column, far from its cause and naming no remedy.
	/// </remarks>
	[Fact]
	public async Task FailFastNamingTheMigration_WhenTheTenantColumnIsAbsent()
	{
		await ArrangeLegacyAsync();

		var store = CreateStore();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => store.SaveRegistrationAsync(CreateRegistration(), TestCancellationToken));

		// Asserted with an explicit comparison rather than a string-containment matcher: `string` is also
		// `IEnumerable<char>`, so the matcher's element overload is a candidate and the assertion can bind to
		// a predicate over characters instead of the substring intended.
		thrown.Message.Contains("003_MakeDataInventoryTenantTotal.sql", StringComparison.Ordinal).ShouldBeTrue(
			"an upgrading consumer cannot act on a failure that does not name the script that fixes it. "
			+ $"Message was: {thrown.Message}");
	}

	/// <summary>
	/// LIVENESS for the backfill. A registration written before the tenant column existed must still be
	/// READABLE after the migration, as an untenanted row.
	/// </summary>
	/// <remarks>
	/// A migration that made every pre-existing registration unreachable would satisfy every safety
	/// property in this file while silently emptying the compliance data map — and a registration is how
	/// the erasure path knows a field holds personal data, so a row that stops being returned is a field
	/// that stops being erased.
	/// </remarks>
	[Fact]
	public async Task KeepALegacyRegistrationReadable_AsUntenanted_AfterTheMigration()
	{
		var table = CurrentArmTable();
		await ArrangeLegacyAsync();
		await SeedLegacyRegistrationAsync(table);

		await MigrateAsync();

		var stored = await QuerySingleAsync<string>(
			"""SELECT tenant_id FROM "compliance"."data_inventory_registrations" WHERE table_name = @Table""",
			table);

		stored.ShouldBe(Sentinel, "a row written before tenancy existed is untenanted, not unknown.");

		var readBack = await CreateStore().FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, null, TestCancellationToken);

		readBack.ShouldContain(
			r => r.TableName == table,
			"the migrated legacy registration must still reach an untenanted caller.");
	}

	/// <summary>
	/// THE SAFETY ARM for the disclosure half. After the migration a tenant that registered nothing must
	/// receive nothing.
	/// </summary>
	[Fact]
	public async Task NotDiscloseAnotherTenantsRegistration_AfterTheMigration()
	{
		var table = CurrentArmTable();
		await ArrangeLegacyAsync();
		await MigrateAsync();

		await CreateStore(NewTenant()).SaveRegistrationAsync(
			CreateRegistration(table), TestCancellationToken);

		var disclosed = await CreateStore(NewTenant()).FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, null, TestCancellationToken);

		disclosed.ShouldNotContain(
			r => r.TableName == table,
			"a tenant that owns nothing must receive nothing; any row here is a disclosure of the PII inventory.");
	}

	/// <summary>
	/// LIVENESS twin of the arm above: the owner must still be served.
	/// </summary>
	[Fact]
	public async Task ReturnATenantsOwnRegistration_AfterTheMigration()
	{
		var table = CurrentArmTable();
		await ArrangeLegacyAsync();
		await MigrateAsync();

		var owner = NewTenant();
		await CreateStore(owner).SaveRegistrationAsync(CreateRegistration(table), TestCancellationToken);

		var found = await CreateStore(owner).FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, null, TestCancellationToken);

		found.ShouldContain(
			r => r.TableName == table,
			"scoping that also hides a tenant's own registrations is not isolation, it is an outage.");
	}

	/// <summary>
	/// THE SAFETY ARM for the overwrite half — the defect a read-side fix alone would ship over.
	/// </summary>
	/// <remarks>
	/// RED unless the migration widens the KEY. With the narrow key both tenants address one row, so the
	/// second save takes the upsert's DO UPDATE branch and the first tenant's registration is destroyed
	/// in place, leaving no trace.
	/// </remarks>
	[Fact]
	public async Task NotLetOneTenantsRegistrationOverwriteAnothers_AfterTheMigration()
	{
		var table = CurrentArmTable();
		await ArrangeLegacyAsync();
		await MigrateAsync();

		var owner = NewTenant();
		var other = NewTenant();

		await CreateStore(owner).SaveRegistrationAsync(
			CreateRegistration(table, "owned by the first tenant"), TestCancellationToken);
		await CreateStore(other).SaveRegistrationAsync(
			CreateRegistration(table, "owned by the second tenant"), TestCancellationToken);

		var ownersView = await CreateStore(owner).FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, null, TestCancellationToken);

		ownersView.ShouldContain(
			r => r.TableName == table && r.Description == "owned by the first tenant",
			"the second tenant's write must not overwrite the first tenant's registration.");
	}

	/// <summary>
	/// Binds the key change in the CATALOGUE, independently of any read or write path.
	/// </summary>
	/// <remarks>
	/// Both tables are checked; adding the term to one and forgetting the other is the natural half-fix.
	/// </remarks>
	[Fact]
	public async Task WidenBothPrimaryKeys_ToIncludeTheTenantTerm()
	{
		await ArrangeLegacyAsync();
		await MigrateAsync();

		(await PrimaryKeyContainsTenantIdAsync("data_inventory_registrations")).ShouldBeTrue(
			"without the tenant term in this key, two tenants registering the same table and field are one row.");

		(await PrimaryKeyContainsTenantIdAsync("discovered_data_locations")).ShouldBeTrue(
			"without the tenant term in this key, two tenants discovering the same record are one row.");
	}

	/// <summary>
	/// The migration is guarded and re-runnable: applying it to a converged database changes nothing and
	/// does not throw.
	/// </summary>
	/// <remarks>
	/// An operator whose first attempt failed part-way — or who cannot tell whether it ran — has to be
	/// able to run it again, and a script that throws on a converged database makes "did this apply?"
	/// unanswerable without reading the catalogue by hand.
	/// </remarks>
	[Fact]
	public async Task BeReRunnable_AgainstAnAlreadyConvergedDatabase()
	{
		var table = CurrentArmTable();
		await ArrangeLegacyAsync();
		await SeedLegacyRegistrationAsync(table);

		await MigrateAsync();
		await MigrateAsync();

		(await PrimaryKeyContainsTenantIdAsync("data_inventory_registrations")).ShouldBeTrue(
			"a second run must leave the widened key in place.");

		var stored = await QuerySingleAsync<string>(
			"""SELECT tenant_id FROM "compliance"."data_inventory_registrations" WHERE table_name = @Table""",
			table);

		stored.ShouldBe(Sentinel, "a second run must not disturb the rows the first one converged.");
	}

	/// <summary>
	/// Provisions the shipped schema and returns the two inventory tables to the pre-migration shape.
	/// </summary>
	private async Task ArrangeLegacyAsync()
	{
		// Never skip-gated. An arm that answers "does the migration repair this database" by not running
		// is not evidence, and the server is the only thing that can answer it.
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "PostgreSQL must be reachable: these arms assert server-enforced schema behaviour.");

		await ShippedCompliancePostgresSchema.EnsureCreatedAsync(
			_fixture.ConnectionString, TestCancellationToken);
		await ShippedCompliancePostgresSchema.RegressDataInventoryToLegacyAsync(
			_fixture.ConnectionString, TestCancellationToken);
	}

	private Task MigrateAsync() =>
		ShippedCompliancePostgresSchema.MigrateDataInventoryAsync(
			_fixture.ConnectionString, TestCancellationToken);

	private static string NewTenant() => $"tenant-{Guid.NewGuid():N}";

	/// <summary>
	/// The calling arm's name, used as the registration's TableName so each arm addresses rows no other
	/// arm can match. Uniqueness is a property of the helper rather than a convention each new arm has to
	/// remember.
	/// </summary>
	private static string CurrentArmTable([CallerMemberName] string armName = "") => armName;

	private static DataLocationRegistration CreateRegistration(
		string? tableName = null,
		string description = "inventory totality arm",
		[CallerMemberName] string armName = "") => new()
		{
			TableName = tableName ?? armName,
			FieldName = "EmailAddress",
			DataCategory = "ContactInformation",
			DataSubjectIdColumn = "CustomerId",
			IdType = DataSubjectIdType.UserId,
			KeyIdColumn = "Id",

			// The NAME of a column in a consumer's table. Set deliberately to a tenant-shaped value: the
			// defect this suite covers was a predicate that read THIS field as though it were the tenant,
			// so leaving it null would let an arm pass by never reaching the confusion it exists to catch.
			TenantIdColumn = "tenant_id",
			Description = description,
		};

	/// <summary>
	/// Seeds a registration through raw SQL rather than the store, because the legacy shape has no tenant
	/// column and the store can no longer write a row without one.
	/// </summary>
	private async Task SeedLegacyRegistrationAsync(string tableName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			"""
			INSERT INTO "compliance"."data_inventory_registrations"
				(table_name, field_name, data_category, data_subject_id_column, id_type, key_id_column,
				 tenant_id_column, description, created_at, updated_at)
			VALUES
				(@Table, 'EmailAddress', 'ContactInformation', 'CustomerId', 0, 'Id',
				 'tenant_id', 'seeded before the migration', NOW(), NOW())
			""",
			new { Table = tableName },
			cancellationToken: TestCancellationToken));
	}

	/// <summary>
	/// Reads the key's COMPOSITION from the catalogue rather than testing for the constraint's name: a
	/// database can carry a correctly-named key that is still missing the tenant term, which is exactly
	/// the half-migrated state worth detecting. The name is not assumed at all here — 001 declares these
	/// keys inline, so PostgreSQL generates it.
	/// </summary>
	private async Task<bool> PrimaryKeyContainsTenantIdAsync(string tableName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);

		return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
			"""
			SELECT EXISTS (
				SELECT 1
				FROM pg_constraint con
				JOIN pg_class rel ON rel.oid = con.conrelid
				JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
				JOIN unnest(con.conkey) AS k(attnum) ON TRUE
				JOIN pg_attribute att ON att.attrelid = con.conrelid AND att.attnum = k.attnum
				WHERE nsp.nspname = 'compliance'
				  AND rel.relname = @Table
				  AND con.contype = 'p'
				  AND att.attname = 'tenant_id'
			)
			""",
			new { Table = tableName },
			cancellationToken: TestCancellationToken));
	}

	private async Task<T> QuerySingleAsync<T>(string sql, string tableName)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);

		return await connection.QuerySingleAsync<T>(new CommandDefinition(
			sql,
			new { Table = tableName },
			cancellationToken: TestCancellationToken));
	}

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options NAMESPACE
	// in this file's scope, not to Microsoft's static class.
	private PostgresDataInventoryStore CreateStore(string? ambientTenant = null) => new(
		Microsoft.Extensions.Options.Options.Create(new PostgresDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = "data_inventory_registrations",
			DiscoveredLocationsTableName = "discovered_data_locations",

			// FALSE deliberately. Auto-create would not repair a legacy table either — it guards on table
			// existence — but leaving it on would blur which mechanism these arms are binding. A consumer
			// upgrading with auto-create ENABLED reaches the same fail-fast, and that equivalence is the
			// point of putting the column check on both paths.
			AutoCreateSchema = false,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<PostgresDataInventoryStore>(),
		// A single-tenant host never receives an ABSENT context: the framework registers its own
		// single-tenant default, so GetRequiredService always resolves one. Passing null here would
		// assert against a state no deployment reaches, and the store rejects it precisely so that
		// "deliberately untenanted" cannot be confused with "a context was forgotten". The untenanted
		// arms therefore stand in the real default; the mode flag below is what distinguishes them.
		new FixedTenantContext(ambientTenant ?? TenantDefaults.DefaultTenantId),
		// The mode follows the arm's own parameter rather than being fixed: supplying an ambient tenant IS
		// the multi-tenant case these arms exercise, and omitting it is the untenanted one. Hard-coding
		// either value would make half the arms assert against a deployment mode they never intended.
		Microsoft.Extensions.Options.Options.Create(
			new TenantContextOptions { RequireTenant = ambientTenant is not null }));

	/// <summary>
	/// Implements <see cref="ITenantContext"/> DIRECTLY and inherits no first-party base, so these arms
	/// bind the store's own resolution of an ambient tenant rather than re-testing a shared helper that
	/// already supplies the behaviour under test.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	/// <summary>
	/// Implements <see cref="IDataSubjectHasher"/> directly and inherits no first-party base. Hashing is
	/// not the property under test, and a stable identity keeps the seeded row findable without making
	/// the assertion depend on a hash algorithm.
	/// </summary>
	private sealed class PassThroughDataSubjectHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
