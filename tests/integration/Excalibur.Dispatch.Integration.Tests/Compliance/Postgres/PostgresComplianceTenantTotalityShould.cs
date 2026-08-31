// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// The Postgres half of the coupled change that makes <c>compliance.legal_holds.tenant_id</c> and
/// <c>compliance.erasure_requests.tenant_id</c> total: the schema migration AND the read predicate that
/// has to move with it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both engines carry the same guarantee, so both are bound.</b> A global legal hold — one belonging to
/// no tenant — is stored as NULL before the migration and as the reserved sentinel after it. The read that
/// shows a scoped tenant its applicable holds used to say <c>tenant = @Ambient OR tenant IS NULL</c>; over
/// a backfilled NOT NULL column that second arm matches nothing, so the predicate silently stops returning
/// global holds.
/// </para>
/// <para>
/// <b>And going quiet is not a fail-safe.</b> A legal hold BLOCKS erasure. A hold that stops being visible
/// does not cause an erasure to be refused — it causes one to PROCEED, against data a court order says to
/// keep, and to report success. Testing this on one engine only would leave half the shipped surface
/// carrying that path uncovered.
/// </para>
/// <para>
/// <b>Real Postgres, provisioned and migrated from the scripts the package SHIPS</b>
/// (<see cref="ShippedCompliancePostgresSchema"/>), and never skip-gated. Each arm regresses the shipped
/// schema to the shape a real upgrading consumer holds, seeds rows through raw SQL as legacy data (the
/// store's write path can no longer produce a NULL tenant, which is the point), then runs the shipped
/// migration and asserts against the store.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.Postgres)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.Postgres)]
public sealed class PostgresComplianceTenantTotalityShould : IntegrationTestBase
{
	private const string Sentinel = "__untenanted__";

	private readonly PostgresFixture _fixture;

	public PostgresComplianceTenantTotalityShould(PostgresFixture fixture) => _fixture = fixture;

	/// <summary>
	/// THE HEADLINE ARM. A global hold stored as a legacy NULL is STILL VISIBLE to a scoped tenant after
	/// the migration rewrites it to the sentinel.
	/// </summary>
	/// <remarks>
	/// RED against the predicate this change replaced: with <c>OR tenant_id IS NULL</c> as the only global
	/// arm, the migrated row matches neither disjunct and this read returns nothing. That silent empty
	/// result is indistinguishable from "there is no hold" to every caller above it — including the
	/// erasure path, which would then proceed.
	/// </remarks>
	[Fact]
	public async Task KeepALegacyGlobalHoldVisibleToAScopedTenant_AfterTheMigration()
	{
		var subject = await ArrangeLegacyAsync();
		var tenant = NewTenant();

		await SeedLegacyHoldAsync(subject, tenantId: null);
		await MigrateAsync();

		var visible = await CreateStore(tenant)
			.GetActiveHoldsForDataSubjectAsync(subject, tenantId: null, TestCancellationToken);

		visible.ShouldHaveSingleItem().TenantId.ShouldBe(
			Sentinel,
			"a hold belonging to no tenant is a GLOBAL hold that blocks erasure for every tenant. After the "
			+ "migration it is spelled with the sentinel rather than NULL, and a scoped read must still "
			+ "return it — a hold that goes invisible does not refuse an erasure, it allows one.");
	}

	/// <summary>
	/// LIVENESS. The scoped tenant still sees its OWN hold after the migration.
	/// </summary>
	/// <remarks>
	/// Paired with the safety arm below on purpose. A predicate that returns nothing to anybody satisfies
	/// every isolation assertion in this file; only this arm fails when the read goes inert.
	/// </remarks>
	[Fact]
	public async Task KeepATenantsOwnHoldVisibleToIt_AfterTheMigration()
	{
		var subject = await ArrangeLegacyAsync();
		var tenant = NewTenant();

		await SeedLegacyHoldAsync(subject, tenantId: tenant);
		await MigrateAsync();

		var visible = await CreateStore(tenant)
			.GetActiveHoldsForDataSubjectAsync(subject, tenantId: null, TestCancellationToken);

		visible.ShouldHaveSingleItem().TenantId.ShouldBe(tenant);
	}

	/// <summary>
	/// SAFETY. The widened predicate matches the sentinel — it must not have widened to match ANOTHER
	/// TENANT's hold.
	/// </summary>
	/// <remarks>
	/// The foreign tenant is seeded as a case-variant of the reader's own, matching the SQL Server arm so
	/// the two engines are held to one rule. This dialect's default collations are deterministic, so it
	/// starts where SQL Server only arrives after the migration states a binary collation — asserting it
	/// here keeps that parity from silently diverging.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsHoldToAScopedTenant_AfterTheMigration()
	{
		var subject = await ArrangeLegacyAsync();
		var tenant = NewTenant();
		var foreignTenant = tenant.ToUpperInvariant();

		await SeedLegacyHoldAsync(subject, tenantId: foreignTenant);
		await MigrateAsync();

		var visible = await CreateStore(tenant)
			.GetActiveHoldsForDataSubjectAsync(subject, tenantId: null, TestCancellationToken);

		visible.ShouldBeEmpty(
			"tenant terms are compared ordinally by the framework, so a case-variant is a DIFFERENT "
			+ "tenant. If this arm returns the row, the tenant predicate is failing open.");
	}

	/// <summary>
	/// The migration rewrites a legacy NULL tenant to the sentinel rather than leaving two spellings of
	/// "no tenant" in the same column.
	/// </summary>
	[Fact]
	public async Task RewriteALegacyNullTenantToTheSentinel()
	{
		var subject = await ArrangeLegacyAsync();

		await SeedLegacyHoldAsync(subject, tenantId: null);
		await MigrateAsync();

		var stored = await QuerySingleAsync<string>(
			@"SELECT tenant_id FROM ""compliance"".""legal_holds"" WHERE data_subject_id_hash = @Subject",
			subject);

		stored.ShouldBe(Sentinel);
	}

	/// <summary>
	/// After the migration the column carries a DEFAULT, so a writer that omits the tenant entirely still
	/// produces the sentinel rather than failing or storing nothing.
	/// </summary>
	[Fact]
	public async Task DefaultAnOmittedTenantToTheSentinel_AfterTheMigration()
	{
		var subject = await ArrangeLegacyAsync();
		await MigrateAsync();

		await ExecuteAsync(
			"""
			INSERT INTO "compliance"."legal_holds"
				(hold_id, data_subject_id_hash, id_type, basis, case_reference, description, is_active,
				 created_by, created_at)
			VALUES
				(@HoldId, @Subject, 0, 0, 'omitted-tenant', 'tenant column omitted entirely', TRUE,
				 'totality-arm', now())
			""",
			subject);

		var stored = await QuerySingleAsync<string>(
			@"SELECT tenant_id FROM ""compliance"".""legal_holds"" WHERE data_subject_id_hash = @Subject",
			subject);

		stored.ShouldBe(Sentinel);
	}

	/// <summary>
	/// After the migration the column REFUSES a NULL, so the old spelling of "no tenant" cannot come back
	/// through a writer that was never updated.
	/// </summary>
	[Fact]
	public async Task RefuseANullTenant_AfterTheMigration()
	{
		var subject = await ArrangeLegacyAsync();
		await MigrateAsync();

		var refused = await Should.ThrowAsync<PostgresException>(async () => await ExecuteAsync(
			"""
			INSERT INTO "compliance"."legal_holds"
				(hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference, description,
				 is_active, created_by, created_at)
			VALUES
				(@HoldId, @Subject, 0, NULL, 0, 'explicit-null', 'explicit NULL tenant', TRUE,
				 'totality-arm', now())
			""",
			subject));

		refused.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
	}

	/// <summary>
	/// The erasure table converges too. Its reads use bare equality and were already blind to a NULL
	/// tenant, so totality is a no-op on what they return — but the column must still stop accepting two
	/// spellings, or the two compliance tables drift apart in the same schema.
	/// </summary>
	[Fact]
	public async Task RewriteALegacyNullTenantToTheSentinel_OnTheErasureTable()
	{
		var subject = await ArrangeLegacyAsync();

		await ExecuteAsync(
			"""
			INSERT INTO "compliance"."erasure_requests"
				(request_id, data_subject_id_hash, id_type, tenant_id, scope, legal_basis, requested_by,
				 requested_at, status, created_at, updated_at)
			VALUES
				(@HoldId, @Subject, 0, NULL, 0, 0, 'totality-arm', now(), 0, now(), now())
			""",
			subject);

		await MigrateAsync();

		var stored = await QuerySingleAsync<string>(
			@"SELECT tenant_id FROM ""compliance"".""erasure_requests"" WHERE data_subject_id_hash = @Subject",
			subject);

		stored.ShouldBe(Sentinel);
	}

	/// <summary>
	/// THE WRITE HALF. A store with no ambient tenant, saving a hold that names none either, must STAMP
	/// the sentinel — not bind the NULL the migrated column now refuses.
	/// </summary>
	/// <remarks>
	/// RED against the binding this change replaced. It read <c>tenant.IsScoped ? tenant.TenantId :
	/// hold.TenantId</c>, so with neither side supplying a term it bound NULL, and every global hold a
	/// single-tenant deployment created would be rejected outright by the migrated column. That is the
	/// half of this change the read arms cannot see: they seed legacy rows through raw SQL precisely
	/// because the write path can no longer produce a NULL, so without this arm nothing would fail if the
	/// normalisation were removed.
	/// </remarks>
	[Fact]
	public async Task StampTheSentinel_WhenAnUnscopedStoreSavesAHoldWithNoTenant()
	{
		var subject = await ArrangeLegacyAsync();
		await MigrateAsync();

		await CreateUnscopedStore().SaveHoldAsync(
			new LegalHold
			{
				HoldId = Guid.NewGuid(),
				DataSubjectIdHash = subject,
				IdType = DataSubjectIdType.UserId,
				Basis = LegalHoldBasis.LegalObligation,
				CaseReference = "totality-arm",
				Description = "Global hold created by a store with no ambient tenant.",
				IsActive = true,
				CreatedBy = "totality-arm",
				CreatedAt = DateTimeOffset.UtcNow,
			},
			TestCancellationToken);

		var stored = await QuerySingleAsync<string>(
			@"SELECT tenant_id FROM ""compliance"".""legal_holds"" WHERE data_subject_id_hash = @Subject",
			subject);

		stored.ShouldBe(
			Sentinel,
			"a hold naming no tenant is a GLOBAL hold. The write path must normalise 'no tenant' to the "
			+ "reserved sentinel, because the column no longer accepts the other spelling.");
	}

	// ---- arrangement -------------------------------------------------------------------------------

	/// <summary>
	/// Provisions the shipped schema and returns it to the pre-migration shape, then yields a unique data
	/// subject so arms sharing the container cannot see each other's rows.
	/// </summary>
	private async Task<string> ArrangeLegacyAsync()
	{
		// Never skip-gated. An arm that answers "was the DEFAULT applied" by not running is not evidence,
		// and this suite exists precisely because the server is the only thing that can answer it.
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Postgres must be reachable: these arms assert server-enforced schema behaviour.");

		await ShippedCompliancePostgresSchema.EnsureCreatedAsync(_fixture.ConnectionString, TestCancellationToken);
		await ShippedCompliancePostgresSchema.RegressToLegacyAsync(_fixture.ConnectionString, TestCancellationToken);

		return $"subject-{Guid.NewGuid():N}";
	}

	private Task MigrateAsync() =>
		ShippedCompliancePostgresSchema.MigrateAsync(_fixture.ConnectionString, TestCancellationToken);

	private static string NewTenant() => $"tenant-{Guid.NewGuid():N}";

	/// <summary>
	/// Seeds a hold through raw SQL rather than the store, because a NULL tenant is LEGACY data the write
	/// path can no longer produce — normalising it at the boundary is half of this change.
	/// </summary>
	private Task SeedLegacyHoldAsync(string dataSubjectIdHash, string? tenantId) => ExecuteAsync(
		"""
		INSERT INTO "compliance"."legal_holds"
			(hold_id, data_subject_id_hash, id_type, tenant_id, basis, case_reference, description,
			 is_active, created_by, created_at)
		VALUES
			(@HoldId, @Subject, 0, @TenantId, 0, 'legacy-hold', 'seeded before the migration', TRUE,
			 'totality-arm', now())
		""",
		dataSubjectIdHash,
		tenantId);

	private async Task ExecuteAsync(string sql, string dataSubjectIdHash, string? tenantId = null)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		_ = await connection.ExecuteAsync(new CommandDefinition(
			sql,
			new { HoldId = Guid.NewGuid(), Subject = dataSubjectIdHash, TenantId = tenantId },
			cancellationToken: TestCancellationToken));
	}

	private async Task<T> QuerySingleAsync<T>(string sql, string dataSubjectIdHash)
	{
		await using var connection = new NpgsqlConnection(_fixture.ConnectionString);
		return await connection.QuerySingleAsync<T>(new CommandDefinition(
			sql,
			new { Subject = dataSubjectIdHash },
			cancellationToken: TestCancellationToken));
	}

	private PostgresLegalHoldStore CreateStore(string ambientTenant) =>
		Build(new FixedTenantContext(ambientTenant), requireTenant: true);

	// The non-multi-tenant shape: no ambient tenant, no predicate emitted. This is the only store that can
	// still CREATE a global hold, which is why the write arm uses it.
	private PostgresLegalHoldStore CreateUnscopedStore() => Build(UntenantedContext.Instance, requireTenant: false);

	private PostgresLegalHoldStore Build(ITenantContext tenantContext, bool requireTenant) => new(
		// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options
		// NAMESPACE in this file's scope, not to Microsoft's static class.
		Microsoft.Extensions.Options.Options.Create(new PostgresLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			// The DEFAULT table name, deliberately: the shipped migration targets it, and an arm run
			// against a custom-named table would be asserting about a table no script migrates.
			TableName = "legal_holds",
			AutoCreateSchema = false,
		}),
		EnabledTestLogger.Create<PostgresLegalHoldStore>(),
		tenantContext,
		// RequireTenant is what AddMultiTenancy sets. Without it the store resolves the non-multi-tenant
		// shape and emits no predicate at all, so every arm here would assert against a store that was
		// never asked to scope.
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = requireTenant }));

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
}
