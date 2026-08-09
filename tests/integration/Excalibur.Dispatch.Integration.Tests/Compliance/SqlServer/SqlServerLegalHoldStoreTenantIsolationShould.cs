// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance;
using Excalibur.Compliance.SqlServer.Erasure;

using System.Runtime.CompilerServices;

namespace Excalibur.Dispatch.Integration.Tests.Compliance.SqlServer;

/// <summary>
/// Binds tenant isolation on the SQL Server legal-hold store — the control that BLOCKS erasure, which makes
/// its failure modes asymmetric in a way the erasure store's are not.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file runs against a real server.</b> The defect is in a SQL predicate: the reads branched on
/// a caller-supplied nullable tenant, and one of them built two entire statements — the one without a tenant
/// argument carried no tenant term at all. An in-memory store filters in C# and never executes either.
/// </para>
/// <para>
/// <b>The asymmetry that governs this file.</b> A legal hold PREVENTS an erasure. Losing one therefore does
/// not fail safe — it erases data a court order says to keep, and the erasure reports success. That is why
/// the liveness arms here are not merely the usual guard against a store that serves nobody: a hold that
/// stops being visible is itself a compliance failure, not just an availability one. It is also why a hold
/// with no tenant — a GLOBAL hold, applying across the estate — must stay visible to a scoped read, and has
/// its own arm below.
/// </para>
/// <para>
/// <b>The tenant is AMBIENT</b>, reaching the store through construction, so every arm builds two stores
/// rather than passing two arguments. The <c>tenantId</c> argument is exercised too and must only ever
/// narrow: a caller that could widen a read by naming a tenant it does not own would defeat the isolation.
/// </para>
/// </remarks>
[IntegrationTest]
[Collection(ContainerCollections.SqlServer)]
[Trait("Category", TestCategories.Integration)]
[Trait("Component", TestComponents.Compliance)]
[Trait("Infrastructure", TestInfrastructure.SqlServer)]
public sealed class SqlServerLegalHoldStoreTenantIsolationShould : IntegrationTestBase
{
	private const string OwningTenant = "hold-tenant-owning";
	private const string ForeignTenant = "hold-tenant-foreign-owns-nothing";

	private readonly SqlServerFixture _fixture;

	public SqlServerLegalHoldStoreTenantIsolationShould(SqlServerFixture fixture) => _fixture = fixture;

	/// <summary>
	/// SAFETY — THE ORIGINAL DEFECT. A data-subject hold lookup with NO tenant argument must not return
	/// another tenant's holds.
	/// </summary>
	/// <remarks>
	/// This read previously built two entire SQL statements and chose between them on whether the caller
	/// passed a tenant. The no-tenant statement carried no tenant term at all, so it returned every tenant's
	/// holds for that data subject — disclosing that another tenant's customer is under legal hold, and on
	/// what basis.
	/// </remarks>
	[Fact]
	public async Task NotDiscloseAnotherTenantsHold_WhenTheCallerSuppliesNoTenantArgument()
	{
		var hold = CreateHold();
		await CreateStore(OwningTenant).SaveHoldAsync(hold, TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).GetActiveHoldsForDataSubjectAsync(
			hold.DataSubjectIdHash!, tenantId: null, TestCancellationToken);

		disclosed.ShouldNotContain(
			h => h.HoldId == hold.HoldId,
			"omitting the tenant argument must not remove the tenant predicate. This is the defect verbatim: "
			+ "the read chose a statement with no tenant term, so a caller who passed nothing was handed "
			+ "every tenant's legal holds for that data subject.");
	}

	/// <summary>
	/// SAFETY. Naming another tenant in the argument must not redirect the read to that tenant's holds.
	/// </summary>
	[Fact]
	public async Task NotDiscloseAnotherTenantsHold_WhenTheCallerNamesThatTenant()
	{
		var hold = CreateHold();
		await CreateStore(OwningTenant).SaveHoldAsync(hold, TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).GetActiveHoldsForTenantAsync(OwningTenant, TestCancellationToken);

		disclosed.ShouldNotContain(
			h => h.HoldId == hold.HoldId,
			"a caller must not reach another tenant's holds by naming that tenant. The argument can only "
			+ "narrow the ambient scope; it can never replace or widen it.");
	}

	/// <summary>
	/// LIVENESS twin, and a compliance control in its own right. A tenant must still see its OWN hold.
	/// </summary>
	/// <remarks>
	/// For a blocking control this is not merely the usual guard against a store that serves nobody. A hold
	/// the store stops returning is a hold that stops blocking: the erasure proceeds, reports success, and
	/// destroys data that was under legal preservation.
	/// </remarks>
	[Fact]
	public async Task StillReturnATenantsOwnHold_ToItsOwnScopedRead()
	{
		var owner = CreateStore(OwningTenant);
		var hold = CreateHold();
		await owner.SaveHoldAsync(hold, TestCancellationToken);

		var found = await owner.GetActiveHoldsForDataSubjectAsync(
			hold.DataSubjectIdHash!, tenantId: null, TestCancellationToken);

		found.ShouldContain(
			h => h.HoldId == hold.HoldId,
			"a tenant must see its own legal hold. If this is empty the hold no longer blocks anything, and "
			+ "the erasure it exists to prevent will run and report success.");
	}

	/// <summary>
	/// LIVENESS — GLOBAL HOLDS. A hold with no tenant applies across the estate and must remain visible to a
	/// tenant-scoped read.
	/// </summary>
	/// <remarks>
	/// This arm exists because the obvious scoping — a bare tenant equality — is wrong here in a way it is
	/// not wrong for the erasure store. A global hold carries no tenant, so equality drops it, and dropping
	/// it fails OPEN on a control whose entire purpose is to refuse: every tenant's erasure would proceed
	/// past a preservation order that nothing reported as missing. The predicate is therefore "matches this
	/// tenant OR carries no tenant", which still excludes every other tenant's holds.
	/// </remarks>
	[Fact]
	public async Task StillReturnAGlobalHold_ToATenantScopedRead()
	{
		// Seeded through an unscoped store: once a tenant is ambient the write stamps it, so a genuinely
		// global hold is an estate-level act and cannot be created from a tenant-facing path.
		var globalHold = CreateHold();
		await CreateUnscopedStore().SaveHoldAsync(globalHold, TestCancellationToken);

		var seenByTenant = await CreateStore(OwningTenant).GetActiveHoldsForDataSubjectAsync(
			globalHold.DataSubjectIdHash!, tenantId: null, TestCancellationToken);

		seenByTenant.ShouldContain(
			h => h.HoldId == globalHold.HoldId,
			"an estate-wide legal hold must still block erasure for every tenant. If a bare tenant equality "
			+ "drops it, the control fails OPEN — erasures run past a preservation order and nothing reports "
			+ "that a hold was missed.");
	}

	/// <summary>
	/// SAFETY — THE READ/MUTATE ASYMMETRY. A tenant must not be able to re-home a global hold onto itself.
	/// </summary>
	/// <remarks>
	/// The subtle one, and the reason reads and mutations use different predicates. A global hold is visible
	/// to every tenant by design, so a mutation reusing the read predicate would MATCH it — and the update
	/// writes the ambient tenant into the row, quietly converting an estate-wide preservation order into one
	/// tenant's private hold. Nothing errors; every other tenant simply stops seeing it, and their next
	/// erasure proceeds past a court-ordered preservation and reports success. Mutations therefore match
	/// strict ownership, so a global hold is untouchable from any tenant-facing path.
	/// </remarks>
	[Fact]
	public async Task NotLetATenantReHomeAGlobalHoldOntoItself()
	{
		var globalHold = CreateHold();
		await CreateUnscopedStore().SaveHoldAsync(globalHold, TestCancellationToken);

		var reHomed = await CreateStore(OwningTenant).UpdateHoldAsync(
			globalHold with { Description = "re-homed by a single tenant" }, TestCancellationToken);

		var stillVisibleToEveryoneElse = await CreateStore(ForeignTenant).GetActiveHoldsForDataSubjectAsync(
			globalHold.DataSubjectIdHash!, tenantId: null, TestCancellationToken);

		reHomed.ShouldBeFalse("a tenant-facing update must not match a global hold at all.");
		stillVisibleToEveryoneElse.ShouldContain(
			h => h.HoldId == globalHold.HoldId,
			"the global hold must still block every other tenant's erasures. If it has vanished, one tenant "
			+ "converted an estate-wide preservation order into its own and silently lifted it for the rest.");
	}

	/// <summary>
	/// SAFETY. A foreign tenant must not read another tenant's hold by id.
	/// </summary>
	[Fact]
	public async Task NotDiscloseAnotherTenantsHold_ToAScopedReadById()
	{
		var hold = CreateHold();
		await CreateStore(OwningTenant).SaveHoldAsync(hold, TestCancellationToken);

		var disclosed = await CreateStore(ForeignTenant).GetHoldAsync(hold.HoldId, TestCancellationToken);

		disclosed.ShouldBeNull(
			"a tenant that holds nothing must not read another tenant's legal hold by id — the row names the "
			+ "case reference and the basis on which another tenant's customer data is preserved.");
	}

	/// <summary>
	/// SAFETY — WRITE. A foreign tenant must not be able to release another tenant's hold.
	/// </summary>
	/// <remarks>
	/// The most severe arm in this file. The update named the hold id alone, so any tenant could deactivate
	/// another tenant's legal hold — removing a preservation control by naming an identifier it does not own,
	/// after which the next erasure destroys the data and reports success.
	/// </remarks>
	[Fact]
	public async Task NotLetAForeignTenantReleaseAnothersHold()
	{
		var owner = CreateStore(OwningTenant);
		var hold = CreateHold();
		await owner.SaveHoldAsync(hold, TestCancellationToken);

		var released = await CreateStore(ForeignTenant).UpdateHoldAsync(
			hold with { IsActive = false, ReleasedBy = "a tenant that does not own this hold", ReleasedAt = DateTimeOffset.UtcNow },
			TestCancellationToken);

		var survivors = await owner.GetActiveHoldsForDataSubjectAsync(
			hold.DataSubjectIdHash!, tenantId: null, TestCancellationToken);

		released.ShouldBeFalse("a foreign tenant's release must not match another tenant's hold.");
		survivors.ShouldContain(
			h => h.HoldId == hold.HoldId,
			"the owner's hold must survive a foreign release. If it is gone, one tenant lifted another "
			+ "tenant's legal preservation order and the next erasure will destroy the data it protected.");
	}

	/// <summary>
	/// LIVENESS twin for the write path. The owner must still be able to release its OWN hold.
	/// </summary>
	[Fact]
	public async Task StillLetATenantReleaseItsOwnHold()
	{
		var owner = CreateStore(OwningTenant);
		var hold = CreateHold();
		await owner.SaveHoldAsync(hold, TestCancellationToken);

		var released = await owner.UpdateHoldAsync(
			hold with { IsActive = false, ReleasedBy = "the owning tenant", ReleasedAt = DateTimeOffset.UtcNow },
			TestCancellationToken);

		released.ShouldBeTrue(
			"a tenant must be able to release its own hold. An UPDATE scoped so tightly it matches nothing "
			+ "passes the cross-tenant arm and makes every hold in the estate permanent.");
	}

	// The data subject is the CALLING ARM's name so every arm seeds a hold no other arm can match. Sharing
	// one identifier makes a hold seeded by one arm indistinguishable from the hold another arm exists to
	// detect, which reads exactly like a leak that has not happened.
	private static LegalHold CreateHold([CallerMemberName] string dataSubjectIdHash = "") => new()
	{
		HoldId = Guid.NewGuid(),
		DataSubjectIdHash = dataSubjectIdHash,
		IdType = DataSubjectIdType.UserId,
		Basis = LegalHoldBasis.LegalObligation,
		CaseReference = "tenant-isolation-arm",
		Description = "Seeded by a tenant-isolation arm.",
		IsActive = true,
		CreatedBy = "tenant-isolation-arm",
		CreatedAt = DateTimeOffset.UtcNow,
	};

	private SqlServerLegalHoldStore CreateStore(string ambientTenant) =>
		Build(new FixedTenantContext(ambientTenant), requireTenant: true);

	// The non-multi-tenant shape: no ambient tenant, no predicate emitted. Used only to seed a genuinely
	// global hold, which a tenant-facing path can no longer create.
	private SqlServerLegalHoldStore CreateUnscopedStore() => Build(tenantContext: null, requireTenant: false);

	// Fully qualified: an unqualified `Options.Create` binds to the Excalibur.Dispatch.Options NAMESPACE in
	// this file's scope, not to Microsoft's static class.
	private SqlServerLegalHoldStore Build(ITenantContext? tenantContext, bool requireTenant) => new(
		Microsoft.Extensions.Options.Options.Create(new SqlServerLegalHoldStoreOptions
		{
			ConnectionString = _fixture.ConnectionString,
			SchemaName = "compliance",
			TableName = "LegalHoldsTenantIsolation",
			AutoCreateSchema = true,
		}),
		EnabledTestLogger.Create<SqlServerLegalHoldStore>(),
		tenantContext,
		// RequireTenant is what AddMultiTenancy sets, and it is the multi-tenant deployment mode. Without it
		// the store resolves the non-multi-tenant shape and emits no predicate at all, so every arm here
		// would be asserting against a store that was never asked to scope.
		Microsoft.Extensions.Options.Options.Create(new TenantContextOptions { RequireTenant = requireTenant }));

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
}
