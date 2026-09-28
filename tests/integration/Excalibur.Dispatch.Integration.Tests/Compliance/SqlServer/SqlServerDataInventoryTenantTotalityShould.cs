// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Runtime.CompilerServices;

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds the SQL Server data-inventory store's tenant totality against the schema the package SHIPS —
/// and the fail-fast an upgrading consumer's pre-tenant database still gets.
/// </summary>
/// <remarks>
/// <para>
/// <b>Two defects, two independent arms.</b> The disclosure is a READ defect, closed by the predicate.
/// The overwrite is a WRITE defect, closed only by the tenant term entering the table's natural KEY. A
/// suite that asserted only the read would go green over a live cross-tenant overwrite, so the key half
/// is bound on its own, by behaviour
/// (<see cref="NotLetOneTenantsRegistrationOverwriteAnothers_OnTheShippedSchema"/>).
/// </para>
/// <para>
/// <b>Safety is paired with liveness throughout.</b> "Tenant B does not see tenant A's rows" is fully
/// satisfied by a store that returns nothing to anybody. Each safety arm here has a twin asserting the
/// rightful owner is still served.
/// </para>
/// <para>
/// <b>There is no in-place upgrade from the pre-tenant shape</b>, and that is a deliberate contract
/// rather than an omission: the package ships one CREATE script per provider, already at the final
/// shape, and a consumer holding an older database re-provisions from it. What remains testable — and
/// what <see cref="FailFastNamingTheRemedy_WhenTheTenantColumnIsAbsent"/> binds — is that such a
/// database is refused at the boundary with a message naming the remedy, rather than dying later on a
/// raw provider error about an unknown column.
/// </para>
/// <para>
/// <b>Provisioned from the script the package ships</b>, never from fixture DDL. A hand-written CREATE
/// TABLE here could drift ahead of the shipped file and pass against a schema no consumer will ever
/// run — silently, which is worse than drifting behind.
/// </para>
/// <para>
/// <b>Arms are isolated by registration identity, not by table.</b> These arms share the default table
/// names, because those are the names a consumer gets. Each arm therefore seeds under its own
/// <c>TableName</c> (taken from the calling member) so one arm's rows can never satisfy another arm's
/// assertion — the failure mode that makes a leak look real when arms run in company but not alone.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerDataInventoryTenantTotalityShould : IntegrationTestBase
{
	private const string SubjectId = "subject-inventory-totality";

	private readonly SqlServerFixture _fixture;

	public SqlServerDataInventoryTenantTotalityShould(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// Restores the shared inventory tables to the shipped shape after every arm, by dropping them and
	/// re-provisioning from the create script.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Not tidiness. These two tables carry the DEFAULT names — they have to, because those are the names
	/// a consumer gets — so every other arm in this collection reads the same tables. The pre-tenant shape
	/// is one no current store will accept: it fails fast on the absent tenant column, which is the
	/// behaviour <see cref="FailFastNamingTheRemedy_WhenTheTenantColumnIsAbsent"/> exists to bind. An arm
	/// that regressed and did not restore would therefore hand the next suite a database its store refuses
	/// to start against, and that suite would report a schema error naming neither this file nor the arm
	/// that caused it. Per-arm rather than per-class, so the window in which the shared schema is
	/// pre-tenant never outlives the single arm that needs it.
	/// </para>
	/// <para>
	/// DROP then create, rather than an ALTER chain, because that IS the shipped remedy: the create script
	/// guards on table existence, so on its own it does nothing to a table that is already there — which
	/// is the whole premise of the fail-fast arm.
	/// </para>
	/// </remarks>
	public override async ValueTask DisposeAsync()
	{
		// Guarded on the fixture rather than run unconditionally: when the container never came up the arm
		// has already failed on its own assertion, and a teardown that then threw a connection error would
		// replace that diagnosis with a less useful one.
		if (_fixture.DockerAvailable)
		{
			await ShippedComplianceSchema.ReprovisionDataInventoryAsync(
				_fixture.ConnectionString, CancellationToken.None);
		}

		await base.DisposeAsync();
	}

	/// <summary>
	/// THE FAIL-FAST ARM. Against a database that still has the pre-tenant shape, the store must refuse to
	/// start and name the remedy.
	/// </summary>
	/// <remarks>
	/// Before the check this binds, both provisioning paths reported success on this database — verify
	/// asked only whether the TABLE existed, and auto-create skips a table that is already there. The
	/// store then initialized cleanly and died on first use with a raw provider error about an unknown
	/// column, far from its cause and naming no remedy. This arm is what makes an upgrading consumer's
	/// failure diagnosable at startup rather than at the first subject-access request.
	/// </remarks>
	[Fact]
	public async Task FailFastNamingTheRemedy_WhenTheTenantColumnIsAbsent()
	{
		await ArrangePreTenantAsync();

		var store = CreateStore();

		var thrown = await Should.ThrowAsync<InvalidOperationException>(
			() => store.SaveRegistrationAsync(CreateRegistration(), TestCancellationToken));

		// Asserted with an explicit comparison rather than a string-containment matcher: a string is also
		// an IEnumerable of char, so the matcher's element overload is a candidate and the assertion can
		// bind to a predicate over characters instead of the substring intended.
		thrown.Message.Contains("Re-provision this table from the shipped create script", StringComparison.Ordinal)
			.ShouldBeTrue(
				"an upgrading consumer cannot act on a failure that does not name the remedy. "
				+ $"Message was: {thrown.Message}");
	}

	/// <summary>
	/// THE SAFETY ARM for the disclosure half. On the shipped schema a tenant that registered nothing must
	/// receive nothing.
	/// </summary>
	[Fact]
	public async Task NotDiscloseAnotherTenantsRegistration_OnTheShippedSchema()
	{
		var table = CurrentArmTable();
		await ArrangeShippedAsync();

		var owner = NewTenant();
		await CreateStore(owner).SaveRegistrationAsync(CreateRegistration(table), TestCancellationToken);

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
	public async Task ReturnATenantsOwnRegistration_OnTheShippedSchema()
	{
		var table = CurrentArmTable();
		await ArrangeShippedAsync();

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
	/// This arm is RED unless the tenant term is in the table's natural KEY. Without it both tenants
	/// address one row, so the second save takes the upsert's UPDATE branch and the first tenant's
	/// registration is destroyed in place, leaving no trace. Scoping the read alone closes the disclosure
	/// and leaves this defect running.
	/// </remarks>
	[Fact]
	public async Task NotLetOneTenantsRegistrationOverwriteAnothers_OnTheShippedSchema()
	{
		var table = CurrentArmTable();
		await ArrangeShippedAsync();

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
	/// Provisions the schema in the shape the package ships, which is the shape every consumer of a
	/// current package gets.
	/// </summary>
	private async Task ArrangeShippedAsync()
	{
		RequireServer();

		await ShippedComplianceSchema.EnsureCreatedAsync(_fixture.ConnectionString, TestCancellationToken);
	}

	/// <summary>
	/// Provisions the shipped schema and then returns the two inventory tables to the pre-tenant shape an
	/// upgrading consumer still holds, which is the only input that can reach the store's fail-fast.
	/// </summary>
	private async Task ArrangePreTenantAsync()
	{
		RequireServer();

		await ShippedComplianceSchema.EnsureCreatedAsync(_fixture.ConnectionString, TestCancellationToken);
		await ShippedComplianceSchema.RegressDataInventoryToPreTenantAsync(
			_fixture.ConnectionString, TestCancellationToken);
	}

	/// <summary>
	/// Never skip-gated. An arm that answers "does the store refuse this database" by not running is not
	/// evidence, and the server is the only thing that can answer it.
	/// </summary>
	private void RequireServer() =>
		_fixture.DockerAvailable.ShouldBeTrue(
			_fixture.InitializationError
			?? "SQL Server must be reachable: these arms assert server-enforced schema behaviour.");

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
			TenantIdColumn = "TenantId",
			Description = description,
		};

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options NAMESPACE
	// in this file's scope, not to Microsoft's static class.
	private SqlServerDataInventoryStore CreateStore(string? ambientTenant = null) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = "DataInventoryRegistrations",
			DiscoveredLocationsTableName = "DiscoveredDataLocations",

			// FALSE deliberately. Auto-create would not repair a pre-tenant table either — it guards on
			// table existence — but leaving it on would blur which mechanism these arms are binding. A
			// consumer upgrading with auto-create ENABLED reaches the same fail-fast, and that equivalence
			// is the point of putting the column check on both paths.
			AutoCreateSchema = false,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<SqlServerDataInventoryStore>(),
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
