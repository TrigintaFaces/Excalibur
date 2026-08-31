// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Excalibur.Dispatch;
using Excalibur.Testing.Conformance;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Runs the published data-inventory conformance kit against the SQL Server store, on a real server.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> The kit had exactly one subclass — the in-memory store — so all of its
/// arms ran against a store that filters in C#. The tenant isolation the kit exists to prove is enforced
/// in a SQL predicate, and an in-memory store never executes one. That is how a cross-tenant disclosure
/// present in BOTH durable providers survived a kit written to catch it: the kit was not wrong, it was
/// simply never pointed at the code that was.
/// </para>
/// <para>
/// A conformance kit with one implementation is a unit test with extra ceremony. Its whole value is
/// holding several implementations to one contract, which requires that each of them derive from it.
/// This subclass and its Postgres sibling are what make the kit's tenant arm mean something for the two
/// stores a consumer actually deploys.
/// </para>
/// <para>
/// <b>Per-arm table isolation.</b> One xUnit instance per test, so the suffix gives each arm its own
/// pair of tables on the shared container. Arms that assert on a whole-table read therefore cannot be
/// perturbed by residue another arm left behind — the kit's tenant arm reads every registration for a
/// subject, so a shared table would let one arm's seed satisfy another arm's assertion.
/// </para>
/// <para>
/// <b>No hand-written DDL.</b> The store provisions its own schema, which is the same DDL the package
/// ships, so these arms cannot pass against a shape no consumer will ever have.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait(TraitNames.Category, TestCategories.Integration)]
[Trait(TraitNames.Component, TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1707:Identifiers should not contain underscores", Justification = "Test method naming convention")]
public sealed class SqlServerDataInventoryStoreConformanceTests : DataInventoryStoreConformanceTestKit
{
	private readonly SqlServerFixture _fixture;

	/// <summary>
	/// Isolates this instance's tables. One xUnit instance per test, so this is per-arm isolation.
	/// </summary>
	private readonly string _suffix = Guid.NewGuid().ToString("N")[..8];

	public SqlServerDataInventoryStoreConformanceTests(SqlServerFixture fixture) => _fixture = fixture;

	/// <inheritdoc />
	protected override IDataInventoryStore CreateStore()
	{
		var options = new SqlServerDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = $"DataInventoryRegistrations_{_suffix}",
			DiscoveredLocationsTableName = $"DiscoveredDataLocations_{_suffix}",
			CommandTimeoutSeconds = 30,

			// The store provisions its own tables. See the class remarks.
			AutoCreateSchema = true,
		};

		// Fully qualified: this file's namespace makes a bare `Options` bind to Excalibur.Dispatch.Options.
		return new SqlServerDataInventoryStore(
			Microsoft.Extensions.Options.Options.Create(options),
			ConformanceDataSubjectHasher.Instance,
			EnabledTestLogger.Create<SqlServerDataInventoryStore>(),
			new AmbientHolderTenantContext(),
			Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));
	}

	/// <inheritdoc />
	/// <remarks>
	/// Returns a real scope rather than <see langword="null"/>: this store IS multi-tenant, so declining
	/// the tenant arms would be a false statement about the implementation and would silently drop the
	/// only arms that bind the property this suite was added for.
	/// </remarks>
	protected override IDisposable EnterTenant(string tenantId) =>
		TenantContextHolder.BeginScope(tenantId);

	/// <summary>
	/// Reads the ambient tenant from <see cref="TenantContextHolder"/>, so the scope entered by
	/// <see cref="EnterTenant"/> is the scope the store under test observes.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Implements <see cref="ITenantContext"/> DIRECTLY and inherits no first-party base: a fixture that
	/// inherits the member under test re-verifies the base rather than the contract. Test-local, and
	/// widens no production visibility to reach it.
	/// </para>
	/// <para>
	/// Outside any scope this resolves the reserved untenanted sentinel rather than <see langword="null"/>.
	/// The kit's non-tenant arms run with no scope entered, and they are untenanted callers — not
	/// unresolved ones. Resolving nothing would mean "multi-tenancy active, tenant unknown", which is
	/// fail-closed by design and would throw rather than answer.
	/// </para>
	/// </remarks>
	private sealed class AmbientHolderTenantContext : ITenantContext
	{
		public string? TenantId => TenantContextHolder.Current ?? TenantScope.UntenantedSentinel;

		public bool HasTenant => !string.IsNullOrEmpty(TenantContextHolder.Current);
	}

	#region Registration Save Tests

	[Fact]
	public Task SaveRegistrationAsync_ShouldPersistRegistration_Test() =>
		SaveRegistrationAsync_ShouldPersistRegistration();

	[Fact]
	public Task SaveRegistrationAsync_DuplicateKey_ShouldUpsert_Test() =>
		SaveRegistrationAsync_DuplicateKey_ShouldUpsert();

	[Fact]
	public Task SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException_Test() =>
		SaveRegistrationAsync_NullRegistration_ShouldThrowArgumentNullException();

	#endregion Registration Save Tests

	#region Registration Remove Tests

	[Fact]
	public Task RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue_Test() =>
		RemoveRegistrationAsync_ExistingRegistration_ShouldReturnTrue();

	[Fact]
	public Task RemoveRegistrationAsync_NonExistent_ShouldReturnFalse_Test() =>
		RemoveRegistrationAsync_NonExistent_ShouldReturnFalse();

	#endregion Registration Remove Tests

	#region Registration Query Tests

	[Fact]
	public Task GetAllRegistrationsAsync_ShouldReturnAllRegistrations_Test() =>
		GetAllRegistrationsAsync_ShouldReturnAllRegistrations();

	[Fact]
	public Task FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType_Test() =>
		FindRegistrationsForDataSubjectAsync_ShouldFilterByIdType();

	#endregion Registration Query Tests

	#region Discovered Locations Save Tests

	[Fact]
	public Task RecordDiscoveredLocationAsync_ShouldPersistLocation_Test() =>
		RecordDiscoveredLocationAsync_ShouldPersistLocation();

	[Fact]
	public Task RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException_Test() =>
		RecordDiscoveredLocationAsync_NullLocation_ShouldThrowArgumentNullException();

	[Fact]
	public Task RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException_Test() =>
		RecordDiscoveredLocationAsync_NullDataSubjectId_ShouldThrowArgumentException();

	[Fact]
	public Task RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate_Test() =>
		RecordDiscoveredLocationAsync_DuplicateLocation_ShouldDeduplicate();

	#endregion Discovered Locations Save Tests

	#region Discovered Locations Query Tests

	[Fact]
	public Task GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations_Test() =>
		GetDiscoveredLocationsAsync_ExistingSubject_ShouldReturnLocations();

	[Fact]
	public Task GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList_Test() =>
		GetDiscoveredLocationsAsync_NonExistentSubject_ShouldReturnEmptyList();

	#endregion Discovered Locations Query Tests

	#region Data Map Tests

	/// <summary>
	/// Skipped against a REAL defect, not a flaky lock. The assertion is correct and is deliberately left
	/// unweakened: it must fail the moment the query is fixed without this skip being removed.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The behaviour: this store builds the data map <c>FROM</c> the registrations table with a correlated
	/// count over discovered locations, so a discovered location that has NO matching registration is
	/// never returned. The data map is the RoPA artifact handed to a regulator, and auto-discovery exists
	/// precisely to find personal data nobody registered — so the report drops exactly what discovery was
	/// added to surface. The in-memory store returns those rows, so a consumer developing against it sees
	/// a complete map and ships an incomplete one.
	/// </para>
	/// <para>
	/// Skipped rather than left red because the correct RoPA semantic is a requirements decision, not an
	/// implementer's, and a permanently-red arm in a shared repository stops being read within days —
	/// which degrades every other red alongside it. A visible skip stays in the runner's own results.
	/// Tracked as Excalibur_Dispatch-8yp9x9.
	/// </para>
	/// </remarks>
	[Fact]
	public Task GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered_Test() =>
		GetDataMapEntriesAsync_ShouldMergeRegistrationsAndDiscovered();

	[Fact]
	public Task GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse_Test() =>
		GetDataMapEntriesAsync_RegistrationsShouldSetIsAutoDiscoveredFalse();

	[Fact]
	public Task GetDataMapEntriesAsync_ShouldCalculateRecordCount_Test() =>
		GetDataMapEntriesAsync_ShouldCalculateRecordCount();

	#endregion Data Map Tests

	#region Multi-Tenant Tests

	/// <summary>
	/// The arm this whole file exists to execute: it is the one that binds cross-tenant disclosure, and
	/// until now it had never run against a SQL predicate.
	/// </summary>
	[Fact]
	public Task FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly_Test() =>
		FindRegistrationsForDataSubjectAsync_WithTenantFilter_ShouldFilterCorrectly();

	[Fact]
	public Task GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries_Test() =>
		GetDataMapEntriesAsync_NullTenant_ShouldReturnAllEntries();

	[Fact]
	public Task GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject_Test() =>
		GetDiscoveredLocationsAsync_ShouldIsolateByDataSubject();

	#endregion Multi-Tenant Tests

	#region Suite Wiring

	/// <summary>
	/// Fails if this suite stops exposing any arm the kit declares.
	/// </summary>
	/// <remarks>
	/// An arm nobody wires never executes, and an arm that never executes cannot fail - in the results it
	/// is indistinguishable from one that passed. That is why the wiring is checked rather than trusted to
	/// survive an edit: a new arm added to the shipped kit turns this red here instead of going silently
	/// unrun.
	/// </remarks>
	[Fact]
	public Task ConformanceSuite_ShouldWireEveryArm_Test() => ConformanceSuite_ShouldWireEveryArm();

	#endregion
}
