// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.Erasure;
using Excalibur.Compliance.SqlServer.Erasure;

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds tenant isolation on the SQL Server erasure-request store — the record of who asked to be erased,
/// when, on what legal basis, and whether it happened.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file runs against a real server.</b> The defect is in a SQL predicate: the reads branched on
/// a caller-supplied nullable tenant, so a caller who passed nothing got no predicate at all. An in-memory
/// store filters in C# and never executes that statement, so a unit suite over it can pass with a perfect
/// score and remain structurally incapable of observing the fault.
/// </para>
/// <para>
/// <b>The property under test, not the mechanism.</b> These arms assert only what a consumer can observe —
/// a scoped read must not hand back another tenant's request, and must still hand back its own. They do not
/// assert that a particular parameter is bound or a particular column compared; a lock written against an
/// assumed mechanism goes vacuous the moment a different correct one is chosen.
/// </para>
/// <para>
/// <b>The tenant is AMBIENT.</b> It reaches the store through construction, which is why every arm builds
/// two stores rather than passing two arguments. Passing a foreign tenant as the <c>tenantId</c> argument is
/// tested too, and must NOT redirect the read — a caller able to name another tenant would reintroduce the
/// authorisation hole this scoping closes.
/// </para>
/// <para>
/// <b>Every safety arm here has a liveness twin, deliberately.</b> A store that returns nothing to anybody
/// satisfies every cross-tenant assertion in this file perfectly, and would be a silent, total failure of
/// the erasure record — the surface a regulator asks to see.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerErasureStoreTenantIsolationShould : IntegrationTestBase
{
	private const string OwningTenant = "erasure-tenant-owning";
	private const string ForeignTenant = "erasure-tenant-foreign-owns-nothing";

	private readonly SqlServerFixture _fixture;

	public SqlServerErasureStoreTenantIsolationShould(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY. A tenant that filed nothing must not be able to read another tenant's erasure request by id.
	/// </summary>
	/// <remarks>
	/// The foreign tenant is named for what makes this decisive: it owns NOTHING, so there is no schema and
	/// no fix under which returning it a row is correct. The disclosed row carries the pseudonymised data
	/// subject, the legal basis, and the erasure timeline for another tenant's customer.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsRequest_ToAScopedReadById()
	{
		var request = CreateRequest();
		await CreateStore(OwningTenant).SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).GetStatusAsync(request.RequestId, TestCancellationToken);

		disclosed.ShouldBeNull(
			"a tenant that has filed no erasure request must never read another tenant's by id. The row "
			+ "discloses who asked to be erased, on what legal basis, and whether it has happened yet.");
	}

	/// <summary>
	/// LIVENESS twin of the arm above. The owning tenant must still read its OWN request by id.
	/// </summary>
	/// <remarks>
	/// This is the arm that makes the safety result mean something: a store scoped so tightly it matches
	/// nothing satisfies every isolation assertion here while making the erasure record unreadable to the
	/// tenant that filed it — and to the regulator who asks that tenant for it.
	/// </remarks>
	[Fact]
	public async Task StillReturnATenantsOwnRequest_ToItsOwnScopedRead()
	{
		var owner = CreateStore(OwningTenant);
		var request = CreateRequest();
		await owner.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var found = await owner.GetStatusAsync(request.RequestId, TestCancellationToken);

		found.ShouldNotBeNull(
			"a tenant must read its own erasure request. If this is null the store scopes by returning "
			+ "nothing to anybody, which passes every cross-tenant arm in this file and destroys the record.");
	}

	/// <summary>
	/// SAFETY — THE ORIGINAL DEFECT. Listing with NO tenant argument must not return another tenant's rows.
	/// </summary>
	/// <remarks>
	/// This is the exact shape of the bug: the list read branched on the caller-supplied nullable, so
	/// omitting it removed the tenant predicate entirely and returned every tenant's erasure history. The
	/// argument is passed as <see langword="null"/> here on purpose — the ambient tenant, not the argument,
	/// must be what scopes the read.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsRequest_WhenTheCallerSuppliesNoTenantArgument()
	{
		var request = CreateRequest();
		await CreateStore(OwningTenant).SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).ListRequestsAsync(
			status: null, tenantId: null, fromDate: null, toDate: null, pageNumber: 1, pageSize: 100, TestCancellationToken);

		disclosed.ShouldNotContain(
			r => r.RequestId == request.RequestId,
			"omitting the tenant argument must not remove the tenant predicate. This is the defect verbatim: "
			+ "the read branched on a caller-supplied nullable, so a caller who passed nothing was handed "
			+ "every tenant's erasure history.");
	}

	/// <summary>
	/// SAFETY. Naming another tenant in the argument must not redirect the read to that tenant's rows.
	/// </summary>
	/// <remarks>
	/// The other half of the defect. A caller able to widen a read by naming a tenant it does not own is an
	/// authorisation hole regardless of what the ambient scope says; the argument may only narrow.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsRequest_WhenTheCallerNamesThatTenant()
	{
		var request = CreateRequest();
		await CreateStore(OwningTenant).SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).ListRequestsAsync(
			status: null, tenantId: OwningTenant, fromDate: null, toDate: null, pageNumber: 1, pageSize: 100, TestCancellationToken);

		disclosed.ShouldNotContain(
			r => r.RequestId == request.RequestId,
			"a caller must not reach another tenant's rows by naming that tenant in the argument. The "
			+ "argument can only narrow the ambient scope; it can never replace or widen it.");
	}

	/// <summary>
	/// LIVENESS twin for the list path. A tenant listing with no argument must still see its OWN requests.
	/// </summary>
	[Fact]
	public async Task StillListATenantsOwnRequests_WhenTheCallerSuppliesNoTenantArgument()
	{
		var owner = CreateStore(OwningTenant);
		var request = CreateRequest();
		await owner.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var found = await owner.ListRequestsAsync(
			status: null, tenantId: null, fromDate: null, toDate: null, pageNumber: 1, pageSize: 100, TestCancellationToken);

		found.ShouldContain(
			r => r.RequestId == request.RequestId,
			"a tenant listing its own erasure requests without naming itself must still see them — the "
			+ "ambient scope supplies the tenant, so omitting the argument narrows nothing.");
	}

	/// <summary>
	/// SAFETY — WRITE. A foreign tenant must not be able to mutate another tenant's request.
	/// </summary>
	/// <remarks>
	/// Reads were only part of the surface. The status update named the request id alone, so any tenant
	/// could drive another tenant's erasure request to Cancelled or Failed — a compliance control defeated
	/// by a caller who cannot even express which tenant it meant.
	/// </remarks>
	[Fact]
	public async Task NotLetAForeignTenantUpdateAnothersRequestStatus()
	{
		var request = CreateRequest();
		await CreateStore(OwningTenant).SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var mutated = await CreateStore(ForeignTenant).UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.Failed, "set by a tenant that does not own this request", TestCancellationToken);

		mutated.ShouldBeFalse(
			"a foreign tenant must not mutate another tenant's erasure request. If this is true, one tenant "
			+ "can fail or cancel another tenant's right-to-erasure and neither party is told.");
	}

	/// <summary>
	/// LIVENESS twin for the write path. The owner must still be able to update its OWN request.
	/// </summary>
	[Fact]
	public async Task StillLetATenantUpdateItsOwnRequestStatus()
	{
		var owner = CreateStore(OwningTenant);
		var request = CreateRequest();
		await owner.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var mutated = await owner.UpdateStatusAsync(
			request.RequestId, ErasureRequestStatus.InProgress, errorMessage: null, TestCancellationToken);

		mutated.ShouldBeTrue(
			"a tenant must be able to advance its own erasure request. An UPDATE scoped so tightly that it "
			+ "matches nothing passes the cross-tenant arm and freezes every erasure in the estate.");
	}

	/// <summary>
	/// SAFETY. A request filed while tenant A is ambient must be stored as A's, not as whatever tenant the
	/// caller wrote onto the request object.
	/// </summary>
	/// <remarks>
	/// The write side of the same hole. The row used to be stamped with the request's own TenantId, so a
	/// caller could file into another tenant's partition — and because every scoped read matches the ambient
	/// term, the planted row would then be visible only to the tenant it was planted on, and invisible to
	/// the one that created it.
	/// </remarks>
	[Fact]
	public async Task StampTheAmbientTenant_NotTheTenantNamedOnTheRequest()
	{
		var owner = CreateStore(OwningTenant);
		var request = CreateRequest() with { TenantId = ForeignTenant };
		await owner.SaveRequestAsync(request, DateTimeOffset.UtcNow.AddDays(30), TestCancellationToken);

		var plantedOnTheForeignTenant = await CreateStore(ForeignTenant).GetStatusAsync(request.RequestId, TestCancellationToken);
		var visibleToItsAuthor = await owner.GetStatusAsync(request.RequestId, TestCancellationToken);

		plantedOnTheForeignTenant.ShouldBeNull(
			"naming another tenant on the request must not file the row into that tenant's partition.");
		visibleToItsAuthor.ShouldNotBeNull(
			"the row belongs to the tenant that filed it and must be readable by them.");
	}

	// The data subject is the CALLING ARM's name so every arm seeds a row no other arm can match. Sharing
	// one identifier across arms makes a row seeded by one arm indistinguishable from the row another arm
	// exists to detect, which reads exactly like a leak that has not happened.
	private static ErasureRequest CreateRequest([CallerMemberName] string dataSubjectId = "") => new()
	{
		RequestId = Guid.NewGuid(),
		DataSubjectId = dataSubjectId,
		IdType = DataSubjectIdType.UserId,
		Scope = ErasureScope.User,
		LegalBasis = ErasureLegalBasis.ConsentWithdrawal,
		RequestedBy = "tenant-isolation-arm",
		RequestedAt = DateTimeOffset.UtcNow,
	};

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options NAMESPACE in
	// this file's scope, not to Microsoft's static class.
	private SqlServerErasureStore CreateStore(string ambientTenant) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerErasureStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			RequestsTableName = "ErasureRequestsTenantIsolation",
			CertificatesTableName = "ErasureCertificatesTenantIsolation",
			AutoCreateSchema = true,
		}),
		new PassThroughDataSubjectHasher(),
		EnabledTestLogger.Create<SqlServerErasureStore>(),
		new FixedTenantContext(ambientTenant),
		// RequireTenant is what AddMultiTenancy sets, and it is the multi-tenant deployment mode. Without it
		// the store resolves the non-multi-tenant shape and emits no predicate at all — every arm in this
		// file would then be asserting against a store that was never asked to scope, and the safety arms
		// would fail for a reason that has nothing to do with isolation.
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = true }));

	/// <summary>
	/// Implements <see cref="ITenantContext"/> DIRECTLY and inherits no first-party base, so these arms bind
	/// the store's own resolution of an ambient tenant rather than re-testing a shared helper that already
	/// supplies the behaviour under test.
	/// </summary>
	private sealed class FixedTenantContext(string tenantId) : ITenantContext
	{
		public string? TenantId { get; } = tenantId;

		public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);
	}

	/// <summary>
	/// Implements <see cref="IDataSubjectHasher"/> directly and inherits no first-party base. Hashing is not
	/// the property under test, and a stable identity keeps the seeded row findable without making the
	/// assertion depend on a hash algorithm.
	/// </summary>
	private sealed class PassThroughDataSubjectHasher : IDataSubjectHasher
	{
		public string HashDataSubjectId(string dataSubjectId) => dataSubjectId;
	}
}
