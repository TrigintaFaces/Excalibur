// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using Microsoft.Extensions.Options;

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds tenant isolation on the SQL Server data-inventory read path — a GDPR data-map surface, so the
/// rows a leak discloses are the estate's PII inventory.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists at all.</b> Every pre-existing test of this contract runs against the
/// in-memory store, which filters in C# and filters correctly. The defect is in a SQL predicate, and an
/// in-memory store never executes it — so that suite passes with a perfect score and is STRUCTURALLY
/// incapable of observing the fault, however many arms it grows. No amount of unit coverage substitutes
/// for one arm that runs the real statement against a real server.
/// </para>
/// <para>
/// <b>The property under test, and why it is not the mechanism.</b> These arms assert only what a
/// consumer can observe: a scoped read must not hand back another tenant's rows. They deliberately do
/// NOT assert how that is achieved — not "a parameter is bound", not "column X is compared". The schema
/// shape is an open architectural question at the time of writing (the registrations table has no
/// tenant VALUE column; <c>TenantIdColumn</c> stores the NAME of a tenant column and is metadata), and a
/// lock written against an assumed mechanism goes vacuous the moment a different one is chosen. A lock
/// on the property survives any fix that actually works.
/// </para>
/// <para>
/// <b>Expected to be RED when written.</b> The predicate under test asks only whether a registration
/// declares a tenant column; the caller's tenantId is never bound to any parameter, so every tenant
/// receives every tenant's rows. If <see cref="NotReturnAnotherTenantsRegistration_ToAScopedRead"/> is
/// GREEN before a fix has landed, the premise is wrong and it must be re-derived — NOT adjusted until
/// it agrees.
/// </para>
/// <para>
/// <b>THE KNOWN GAP RECORDED HERE IS NOW CLOSED.</b> This file previously stated that the ideal liveness
/// twin — "tenant A's scoped read DOES see A's own registration" — was not expressible, because no column
/// recorded which tenant owned a registration, and it instructed that the arm be added "when a tenant
/// value column exists". It exists: the registrations table now carries a real <c>TenantId</c> value
/// column (BIN2, NOT NULL, sentinel default) which is part of the primary key, and the store binds the
/// ambient tenant to it. <see cref="ReturnATenantsOwnRegistration_ToItsOwnScopedRead" /> is that arm.
/// The unscoped-read substitute is RETAINED rather than replaced — it catches a different failure (a
/// store that serves nobody), so the two are not redundant.
/// </para>
/// <para>
/// <b>Three operations, not one.</b> The read path was always a third of the surface. A registration can
/// also be destroyed by another tenant's WRITE (the MERGE matched on table+field alone, taking the UPDATE
/// branch) and by another tenant's DELETE (the predicate named table+field alone). Each now has a safety
/// arm and a liveness twin, because each admits the same degenerate "fix": scope so tightly that the
/// operation stops working for its rightful owner too.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerDataInventoryStoreTenantIsolationShould : IntegrationTestBase
{
	private const string SubjectId = "subject-isolation";
	private const string OwningTenant = "tenant-owning";
	private const string ForeignTenant = "tenant-foreign-owns-nothing";

	private readonly SqlServerFixture _fixture;

	public SqlServerDataInventoryStoreTenantIsolationShould(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// THE SAFETY ARM. A read scoped to a tenant that registered nothing must come back without another
	/// tenant's registration.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The foreign tenant is named for what makes it decisive: it owns NOTHING. There is no filter, no
	/// schema and no fix under which it is correct for it to receive a row — so this arm needs no
	/// knowledge of how ownership is modelled in order to be right. Any row it returns is a disclosure.
	/// </para>
	/// <para>
	/// <b>The tenant is AMBIENT, and this arm binds it that way deliberately.</b> An earlier version of
	/// this arm seeded and read through a single store and passed <c>ForeignTenant</c> as the
	/// <c>tenantId</c> <i>argument</i>, expecting that argument to scope the read. The shipped store
	/// discards it — a caller must not be able to redirect a read by naming another tenant, and honouring
	/// the argument would reintroduce the authorisation hole the scoping fix closes. That version was
	/// therefore asserting a contract the fix deliberately removed, and it was <b>doubly</b> wrong: with
	/// no ambient context on either operation, the seed and the read both landed in the untenanted
	/// partition, so there was only ever ONE partition and the arm could not have detected a cross-tenant
	/// disclosure under any implementation. Two stores with distinct ambient tenants is what makes the
	/// assertion bind something real.
	/// </para>
	/// </remarks>
	[Fact]
	public async Task NotReturnAnotherTenantsRegistration_ToAScopedRead()
	{
		var owner = CreateStore(OwningTenant);
		var registration = CreateRegistration();
		await owner.SaveRegistrationAsync(registration, TestCancellationToken);

		var foreigner = CreateStore(ForeignTenant);
		var disclosed = await foreigner.FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, ForeignTenant, TestCancellationToken);

		disclosed.ShouldNotContain(
			r => r.TableName == registration.TableName && r.FieldName == registration.FieldName,
			"a tenant that has registered nothing must never be handed another tenant's data-map entry — "
			+ "this is a GDPR inventory read path, so the disclosed row describes where that tenant's PII "
			+ "lives, and the caller cannot tell a leaked row from one it is entitled to.");
	}

	/// <summary>
	/// THE LIVENESS ARM. The store must still return the registration on an unscoped read.
	/// </summary>
	/// <remarks>
	/// Without this, the safety arm above is satisfied completely by a store that returns an empty list
	/// to every caller forever — the cheapest way to leak nothing is to serve nothing, and it would pass
	/// as a fix while destroying the data map. This arm is the reason a green safety result means
	/// anything at all.
	/// </remarks>
	[Fact]
	public async Task StillReturnTheRegistration_ToAnUnscopedRead()
	{
		var store = CreateStore();
		var registration = CreateRegistration();
		await store.SaveRegistrationAsync(registration, TestCancellationToken);

		var found = await store.FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, tenantId: null, TestCancellationToken);

		found.ShouldContain(
			r => r.TableName == registration.TableName && r.FieldName == registration.FieldName,
			"an unscoped read must still see the registration — a store that returns nothing to anybody "
			+ "would satisfy the isolation arm perfectly while silently emptying the compliance data map.");
	}

	/// <summary>
	/// CLOSES THIS FILE'S OWN KNOWN GAP. A tenant's scoped read must still see the registration IT owns.
	/// </summary>
	/// <remarks>
	/// The remarks above recorded this arm as not-expressible and said to add it "when a tenant value
	/// column exists". It exists now: the registrations table carries a real <c>TenantId</c> value column
	/// (BIN2, NOT NULL, sentinel default) and the store binds the ambient tenant to it. Ownership is
	/// therefore seedable, so the honest liveness twin can finally be written.
	///
	/// This is the arm the unscoped-read substitute could not provide: a store that returns rows to
	/// EVERYONE also passes "an unscoped read sees the row". Only a scoped read by the owner distinguishes
	/// correct scoping from no scoping at all.
	/// </remarks>
	[Fact]
	public async Task ReturnATenantsOwnRegistration_ToItsOwnScopedRead()
	{
		var owner = CreateStore(OwningTenant);
		var registration = CreateRegistration();
		await owner.SaveRegistrationAsync(registration, TestCancellationToken);

		var found = await owner.FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, OwningTenant, TestCancellationToken);

		found.ShouldContain(
			r => r.TableName == registration.TableName && r.FieldName == registration.FieldName,
			"a tenant must see its OWN registration on a scoped read. Without this arm, a store that "
			+ "scopes by returning nothing to anybody passes every isolation assertion in this file while "
			+ "silently emptying the compliance data map for its rightful owner.");
	}

	/// <summary>
	/// SAFETY — WRITE. One tenant registering a table+field must not overwrite another tenant's row.
	/// </summary>
	/// <remarks>
	/// The write path is a MERGE. Before the tenant term entered the key and the match condition, a second
	/// tenant registering the same table+field matched the first tenant's row and took the UPDATE branch —
	/// no exception, no rowcount anomaly, nothing a caller could observe. A lost registration is not merely
	/// a lost row: erasure never visits a field it has no registration for, so the observable consequence
	/// is a GDPR erasure that reports success and misses data.
	///
	/// This arm asserts the OWNER still has its registration after the foreign write, which is the property.
	/// It deliberately does not assert on the key shape or the MERGE text — a lock on the mechanism goes
	/// vacuous the moment a different correct mechanism is chosen.
	/// </remarks>
	[Fact]
	public async Task NotLetOneTenantsRegistrationOverwriteAnothers()
	{
		var owner = CreateStore(OwningTenant);
		var foreigner = CreateStore(ForeignTenant);

		var registration = CreateRegistration();
		await owner.SaveRegistrationAsync(registration, TestCancellationToken);
		await foreigner.SaveRegistrationAsync(registration, TestCancellationToken);

		var ownersView = await owner.FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, OwningTenant, TestCancellationToken);

		ownersView.ShouldContain(
			r => r.TableName == registration.TableName && r.FieldName == registration.FieldName,
			"a second tenant registering the same table+field must not consume the first tenant's row. If "
			+ "this is empty, the foreign write silently replaced the owner's registration and erasure will "
			+ "never visit that field for the owner — a compliance control failing while reporting success.");
	}

	/// <summary>
	/// SAFETY — DELETE. One tenant deregistering a table+field must not destroy another tenant's row.
	/// </summary>
	/// <remarks>
	/// The most severe of the three: <c>RemoveRegistrationAsync</c> names only table and field, so before
	/// the tenant term entered the DELETE predicate a foreign deregistration destroyed every tenant's row
	/// for that field. Not disclosure and not an overwrite — destruction, by a caller who cannot even
	/// express which tenant they meant.
	///
	/// The signature still takes no tenant, deliberately: the tenant is AMBIENT, resolved from the context
	/// the store was constructed with. That is why this arm builds two stores rather than passing two
	/// arguments — the isolation lives in construction, and an arm that passed a tenant parameter would be
	/// testing an API that does not exist.
	/// </remarks>
	[Fact]
	public async Task NotLetOneTenantsDeregistrationDestroyAnothersRow()
	{
		var owner = CreateStore(OwningTenant);
		var foreigner = CreateStore(ForeignTenant);

		var registration = CreateRegistration();
		await owner.SaveRegistrationAsync(registration, TestCancellationToken);

		_ = await foreigner.RemoveRegistrationAsync(registration.TableName, registration.FieldName, TestCancellationToken);

		var survivors = await owner.FindRegistrationsForDataSubjectAsync(
			SubjectId, DataSubjectIdType.UserId, OwningTenant, TestCancellationToken);

		survivors.ShouldContain(
			r => r.TableName == registration.TableName && r.FieldName == registration.FieldName,
			"a foreign tenant's deregistration must not delete the owner's registration. If this is empty, "
			+ "one tenant destroyed another tenant's compliance record by naming a table and a field it "
			+ "does not own — cross-tenant data destruction, and the owner receives no error at any point.");
	}

	/// <summary>
	/// LIVENESS for the delete path. The owner's own deregistration must still work.
	/// </summary>
	/// <remarks>
	/// Pairs with the arm above. Scoping the DELETE by tenant is trivially "safe" if it deletes nothing for
	/// anybody; this proves the predicate still matches the caller's own row. Without it, a fix that bound
	/// the wrong term — or bound the sentinel unconditionally — would pass the safety arm and quietly break
	/// deregistration for every tenant.
	/// </remarks>
	[Fact]
	public async Task StillDeleteTheOwnersOwnRegistration()
	{
		var owner = CreateStore(OwningTenant);
		var registration = CreateRegistration();
		await owner.SaveRegistrationAsync(registration, TestCancellationToken);

		var removed = await owner.RemoveRegistrationAsync(registration.TableName, registration.FieldName, TestCancellationToken);

		removed.ShouldBeTrue(
			"a tenant must be able to deregister its OWN field. A DELETE scoped so tightly that it matches "
			+ "nothing satisfies the cross-tenant arm perfectly while making deregistration impossible.");
	}

	// The registration's identity is the CALLING ARM's name, so every arm seeds a row no other arm can
	// match. Previously all six arms shared one hardcoded TableName/FieldName pair while differing only in
	// the tenant they saved under, and the arms assert on that same pair -- so a row seeded by one arm was
	// indistinguishable from the row another arm was written to detect. The unscoped arm writes under the
	// untenanted sentinel, and untenanted registrations are returned to every scope by ruled design, so
	// whenever that arm ran first the cross-tenant arm found ITS seed and reported a disclosure that had
	// not happened. The arm passed alone and failed in company, which reads exactly like a real leak.
	//
	// CallerMemberName rather than a per-arm constant on purpose: uniqueness is then a property of the
	// helper, not a convention each new arm has to remember. An arm added later cannot reintroduce the
	// collision by copying the call, which is how this was written in the first place.
	private static DataLocationRegistration CreateRegistration([CallerMemberName] string tableName = "") => new()
	{
		TableName = tableName,
		FieldName = "EmailAddress",
		DataCategory = "ContactInformation",
		DataSubjectIdColumn = "CustomerId",
		IdType = DataSubjectIdType.UserId,
		KeyIdColumn = "Id",
		// Set deliberately: the predicate under test keys on this column being non-null, so leaving it
		// null would make the arm pass by never reaching the faulty branch — green for the wrong reason.
		TenantIdColumn = "TenantId",
		Description = $"registered by {OwningTenant}",
	};

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options
	// NAMESPACE in this file's scope, not to Microsoft's static class.
	private SqlServerDataInventoryStore CreateStore(string? ambientTenant = null) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerDataInventoryStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RegistrationsTableName = "DataInventoryRegistrations",
			AutoCreateSchema = true,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<SqlServerDataInventoryStore>(),
		ambientTenant is null ? null : new FixedTenantContext(ambientTenant));

	/// <summary>
	/// Implements <see cref="ITenantContext"/> DIRECTLY and inherits no first-party base, so these arms
	/// bind the store's own resolution of an ambient tenant rather than re-testing a shared helper that
	/// already supplies the behaviour under test.
	/// </summary>
	/// <remarks>
	/// The tenant reaches the store through CONSTRUCTION, not through a method parameter — which is why
	/// the isolation arms build two stores instead of passing two arguments. An arm written the other way
	/// would be testing an API the contract does not have.
	/// </remarks>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	/// <summary>
	/// Implements <see cref="IDataSubjectHasher"/> directly and inherits no first-party base, so these
	/// arms bind the store's own behaviour rather than re-testing a shared helper. The production hasher
	/// is internal; hashing is not the property under test, and a stable identity keeps the seeded row
	/// findable without making the assertion depend on a hash algorithm.
	/// </summary>
	private sealed class PassThroughDataSubjectHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
