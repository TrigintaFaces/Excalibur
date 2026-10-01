// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Dapper;

using Excalibur.Compliance;
using Excalibur.Compliance.Postgres.Erasure;

using Npgsql;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.Postgres;

/// <summary>
/// The Postgres half of the coupled guarantee that makes <c>compliance.legal_holds.tenant_id</c> total:
/// the shipped column shape AND the read predicate that has to match it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Both engines carry the same guarantee, so both are bound.</b> A global legal hold — one belonging to
/// no tenant — is stored as the reserved sentinel, never as NULL. A read that says
/// <c>tenant = @Ambient OR tenant IS NULL</c> matches nothing over a NOT NULL column, so the predicate
/// silently stops returning global holds.
/// </para>
/// <para>
/// <b>And going quiet is not a fail-safe.</b> A legal hold BLOCKS erasure. A hold that stops being visible
/// does not cause an erasure to be refused — it causes one to PROCEED, against data a court order says to
/// keep, and to report success. Testing this on one engine only would leave half the shipped surface
/// carrying that path uncovered.
/// </para>
/// <para>
/// <b>Real Postgres, provisioned from the script the package SHIPS</b>
/// (<see cref="ShippedCompliancePostgresSchema"/>), and never skip-gated. Whether a DEFAULT fires on an
/// omitted column, whether a column refuses a NULL, and whether a predicate matches a row are answered by
/// the server and by nothing else.
/// </para>
/// <para>
/// <b>There is no in-place upgrade from the pre-tenant shape.</b> The package ships one CREATE script per
/// provider, already at the final shape, so the only database these arms can meaningfully describe is the
/// one a consumer actually provisions. Arms that asserted about converting a legacy database were removed
/// with the migration scripts rather than left asserting against a path that no longer exists.
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
	/// LIVENESS. The scoped tenant sees its OWN hold.
	/// </summary>
	/// <remarks>
	/// Paired with the safety arm below on purpose. A predicate that returns nothing to anybody satisfies
	/// every isolation assertion in this file; only this arm fails when the read goes inert.
	/// </remarks>
	[Fact]
	public async Task KeepATenantsOwnHoldVisibleToIt_OnTheShippedSchema()
	{
		var subject = await ArrangeShippedAsync();
		var tenant = NewTenant();

		await SeedHoldAsync(subject, tenantId: tenant);

		var visible = await CreateStore(tenant)
			.GetActiveHoldsForDataSubjectAsync(subject, tenantId: null, TestCancellationToken);

		visible.ShouldHaveSingleItem().TenantId.ShouldBe(tenant);
	}

	/// <summary>
	/// SAFETY. The predicate matches the sentinel — it must not match ANOTHER TENANT's hold.
	/// </summary>
	/// <remarks>
	/// The foreign tenant is seeded as a case-variant of the reader's own, matching the SQL Server arm so
	/// the two engines are held to one rule. This dialect's default collations are deterministic, where
	/// SQL Server has to state a binary collation on the column to get there — asserting it here keeps
	/// that parity from silently diverging.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsHoldToAScopedTenant_OnTheShippedSchema()
	{
		var subject = await ArrangeShippedAsync();
		var tenant = NewTenant();
		var foreignTenant = tenant.ToUpperInvariant();

		await SeedHoldAsync(subject, tenantId: foreignTenant);

		var visible = await CreateStore(tenant)
			.GetActiveHoldsForDataSubjectAsync(subject, tenantId: null, TestCancellationToken);

		visible.ShouldBeEmpty(
			"tenant terms are compared ordinally by the framework, so a case-variant is a DIFFERENT "
			+ "tenant. If this arm returns the row, the tenant predicate is failing open.");
	}

	/// <summary>
	/// The column carries a DEFAULT, so a writer that omits the tenant entirely still produces the
	/// sentinel rather than failing or storing nothing.
	/// </summary>
	[Fact]
	public async Task DefaultAnOmittedTenantToTheSentinel_OnTheShippedSchema()
	{
		var subject = await ArrangeShippedAsync();

		await ExecuteAsync(
			"""
			INSERT INTO "compliance"."legal_holds"
				(hold_id, data_subject_id_hash, id_type, basis_v2, case_reference, description, is_active,
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
	/// The column REFUSES a NULL, so the other spelling of "no tenant" cannot come back through a writer
	/// that was never updated.
	/// </summary>
	[Fact]
	public async Task RefuseANullTenant_OnTheShippedSchema()
	{
		var subject = await ArrangeShippedAsync();

		var refused = await Should.ThrowAsync<PostgresException>(async () => await ExecuteAsync(
			"""
			INSERT INTO "compliance"."legal_holds"
				(hold_id, data_subject_id_hash, id_type, tenant_id, basis_v2, case_reference, description,
				 is_active, created_by, created_at)
			VALUES
				(@HoldId, @Subject, 0, NULL, 0, 'explicit-null', 'explicit NULL tenant', TRUE,
				 'totality-arm', now())
			""",
			subject));

		refused.SqlState.ShouldBe(PostgresErrorCodes.NotNullViolation);
	}

	/// <summary>
	/// THE WRITE HALF. A store with no ambient tenant, saving a hold that names none either, must STAMP
	/// the sentinel — not bind the NULL the column refuses.
	/// </summary>
	/// <remarks>
	/// RED against a binding that read <c>tenant.IsScoped ? tenant.TenantId : hold.TenantId</c>: with
	/// neither side supplying a term it binds NULL, and every global hold a single-tenant deployment
	/// created is rejected outright by the column. That is the half the read arms cannot see, because
	/// they supply a tenant explicitly.
	/// </remarks>
	[Fact]
	public async Task StampTheSentinel_WhenAnUnscopedStoreSavesAHoldWithNoTenant()
	{
		var subject = await ArrangeShippedAsync();

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
			+ "reserved sentinel, because the column does not accept the other spelling.");
	}

	// ---- arrangement -------------------------------------------------------------------------------

	/// <summary>
	/// Provisions the shipped schema, then yields a unique data subject so arms sharing the container
	/// cannot see each other's rows.
	/// </summary>
	private async Task<string> ArrangeShippedAsync()
	{
		// Never skip-gated. An arm that answers "was the DEFAULT applied" by not running is not evidence,
		// and this suite exists precisely because the server is the only thing that can answer it.
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "Postgres must be reachable: these arms assert server-enforced schema behaviour.");

		await ShippedCompliancePostgresSchema.EnsureCreatedAsync(_fixture.ConnectionString, TestCancellationToken);

		return $"subject-{Guid.NewGuid():N}";
	}

	private static string NewTenant() => $"tenant-{Guid.NewGuid():N}";

	/// <summary>
	/// Seeds a hold through raw SQL rather than the store, so the arm controls the stored tenant term
	/// exactly — including a case-variant the store's own write path would never produce.
	/// </summary>
	private Task SeedHoldAsync(string dataSubjectIdHash, string tenantId) => ExecuteAsync(
		"""
		INSERT INTO "compliance"."legal_holds"
			(hold_id, data_subject_id_hash, id_type, tenant_id, basis_v2, case_reference, description,
			 is_active, created_by, created_at)
		VALUES
			(@HoldId, @Subject, 0, @TenantId, 0, 'seeded-hold', 'seeded directly through SQL', TRUE,
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
			// The DEFAULT table name, deliberately: it is the name the shipped script creates, and an arm
			// run against a custom-named table would be asserting about a table no script provisions.
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
