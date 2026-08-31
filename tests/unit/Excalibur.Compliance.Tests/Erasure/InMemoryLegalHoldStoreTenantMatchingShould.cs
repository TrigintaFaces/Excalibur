// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Compliance.Erasure;
using Excalibur.Dispatch;

using Microsoft.Extensions.Options;

namespace Excalibur.Compliance.Tests.Erasure;

/// <summary>
/// Binds the in-memory legal-hold store's ambient tenant term to the same contract the SQL providers
/// implement: a scoped tenant sees its own holds and every GLOBAL hold, and no other tenant's.
/// </summary>
/// <remarks>
/// <para>
/// These arms exist because the store admitted only one of the two spellings of "untenanted". Both SQL
/// providers match <c>TenantId IN (@Ambient, @Untenanted) OR TenantId IS NULL</c>; this store matched
/// null-or-equal and had no sentinel arm at all. A global hold carrying the sentinel — which is what a
/// hold holds after any round trip through a SQL provider — was therefore read here as belonging to a
/// tenant literally named by that reserved string: visible to every tenant on SQL and to none in memory.
/// </para>
/// <para>
/// The liveness arms are the load-bearing ones and they are the reason this class is not simply an
/// isolation test. A store that returns nothing to anybody satisfies every isolation assertion perfectly
/// and erases everything, because a legal hold that cannot be seen does not block the erasure it was filed
/// to prevent. Losing a hold does not fail safe.
/// </para>
/// <para>
/// Every arm runs under a SCOPED ambient tenant. Constructed without a tenant context the store's scope is
/// unscoped, the ambient term short-circuits to true, and all of this passes without exercising anything —
/// which is exactly why the pre-existing suites for this store could not see the defect.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Compliance")]
public sealed class InMemoryLegalHoldStoreTenantMatchingShould
{
	private const string AmbientTenant = "tenant-a";
	private const string OtherTenant = "tenant-b";
	private const string SubjectHash = "hash-subject-1";

	/// <summary>
	/// An <see cref="ITenantContext"/> implemented directly, whose ambient tenant can be moved between the
	/// seeding phase and the reading phase of an arm.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Implements the interface from scratch rather than deriving from a framework base, so the assertions
	/// bind the interface's own contract instead of re-testing a base class that already supplies it.
	/// </para>
	/// <para>
	/// It is MUTABLE for a reason that is easy to get wrong, and getting it wrong makes these arms pass
	/// while testing nothing. <c>SaveHoldAsync</c> treats the ambient term as authoritative on write and
	/// stamps it onto the hold, deliberately — a tenant must not be able to file a hold into another
	/// tenant's partition, nor to create a global one. So a hold seeded through a store that is ALREADY
	/// scoped to the reading tenant comes back stamped with that tenant, and an arm that seeds a "global" or
	/// "foreign" hold that way is really asserting that a tenant can see its own hold. Three arms here did
	/// exactly that before this fixture existed, and passed against the unfixed store.
	/// </para>
	/// <para>
	/// A null tenant resolves to an UNSCOPED scope, which is the estate-level path by which a genuinely
	/// global hold is created. Seeding happens there; the tenant is then moved to a named one for the read.
	/// </para>
	/// </remarks>
	private sealed class MovableTenantContext : ITenantContext
	{
		public string? TenantId { get; set; }

		public bool HasTenant => !string.IsNullOrEmpty(TenantId);
	}

	/// <summary>
	/// Seeds each hold under an ambient scope equal to the tenant that hold belongs to, then scopes the store
	/// to <see cref="AmbientTenant"/> and returns the hold ids that tenant can see.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The ambient tenant is moved per hold because <c>SaveHoldAsync</c> STAMPS the ambient term onto the row
	/// and ignores the tenant on the object handed to it — deliberately, so a tenant cannot file a hold into
	/// another tenant's partition. Seeding every row through one already-scoped store therefore produces rows
	/// that all belong to that one tenant, and arms built that way assert only that a tenant can see its own
	/// hold. Three arms in this class did exactly that until the safety arm failed and exposed it.
	/// </para>
	/// <para>
	/// A GLOBAL hold is seeded by scoping to <see cref="TenantScope.UntenantedSentinel"/>, which is how one is
	/// actually created: under <c>RequireTenant</c> a null ambient tenant is refused outright by
	/// <c>TenantScope.Scoped</c>, so the sentinel is the only reachable spelling of "no tenant" on the write
	/// path. The predicate still admits a null row for a database that predates the migration, but no such row
	/// can be produced through this store's public surface, so it is not asserted here.
	/// </para>
	/// </remarks>
	private static async Task<List<Guid>> SeedThenReadAsAmbientTenantAsync(params LegalHold[] holds)
	{
		var context = new MovableTenantContext { TenantId = AmbientTenant };

		// RequireTenant MUST be set. Without it AmbientScope short-circuits to TenantScope.Untenanted whatever the
		// context says, the ambient term matches everything, and every arm in this class passes against a
		// store with no tenant matching at all. This options object is the difference between a suite that
		// binds the predicate and one that only appears to.
		var store = new InMemoryLegalHoldStore(
			context,
			Options.Create(new TenantContextOptions { RequireTenant = true }));

		foreach (var hold in holds)
		{
			context.TenantId = hold.TenantId;
			await store.SaveHoldAsync(hold, CancellationToken.None).ConfigureAwait(false);
		}

		context.TenantId = AmbientTenant;

		var visible = await store
			.GetActiveHoldsForDataSubjectAsync(SubjectHash, null, CancellationToken.None)
			.ConfigureAwait(false);

		return visible.Select(h => h.HoldId).ToList();
	}

	private static LegalHold CreateHold(string? tenantId) => new()
	{
		HoldId = Guid.NewGuid(),
		DataSubjectIdHash = SubjectHash,
		IdType = DataSubjectIdType.UserId,
		TenantId = tenantId,
		Basis = LegalHoldBasis.LitigationHold,
		CaseReference = $"CASE-{Guid.NewGuid():N}",
		Description = "Conformance hold",
		IsActive = true,
		CreatedBy = "admin",
		CreatedAt = DateTimeOffset.UtcNow
	};

	/// <summary>
	/// LIVENESS, and the arm that was RED: a global hold carrying the reserved sentinel must be visible to a
	/// scoped tenant, because it blocks that tenant's erasures too.
	/// </summary>
	[Fact]
	public async Task Show_a_scoped_tenant_a_global_hold_stored_under_the_untenanted_sentinel()
	{
		var globalHold = CreateHold(TenantScope.UntenantedSentinel);

		var visible = await SeedThenReadAsAmbientTenantAsync(globalHold).ConfigureAwait(false);

		visible.ShouldContain(
			globalHold.HoldId,
			"a global hold stored under the untenanted sentinel must block erasure for every tenant, so a "
			+ "scoped read must return it. Both SQL providers match the sentinel; a store that does not "
			+ "silently drops the hold, and the erasure it exists to prevent proceeds.");
	}

	/// <summary>
	/// LIVENESS: a tenant's own hold is still returned. Without this arm a store that returns nothing at all
	/// satisfies the safety arm below perfectly.
	/// </summary>
	[Fact]
	public async Task Show_a_scoped_tenant_its_own_hold()
	{
		var ownHold = CreateHold(AmbientTenant);

		var visible = await SeedThenReadAsAmbientTenantAsync(ownHold).ConfigureAwait(false);

		visible.ShouldContain(
			ownHold.HoldId,
			"a tenant must see its own legal hold, or its own erasures are no longer blocked.");
	}

	/// <summary>
	/// SAFETY: another tenant's hold is never returned. The sentinel arm must widen the term to GLOBAL holds
	/// only — not to every hold.
	/// </summary>
	[Fact]
	public async Task Hide_another_tenants_hold_from_a_scoped_read()
	{
		var foreignHold = CreateHold(OtherTenant);

		var visible = await SeedThenReadAsAmbientTenantAsync(foreignHold).ConfigureAwait(false);

		visible.ShouldNotContain(
			foreignHold.HoldId,
			"a scoped read must never surface another tenant's legal hold. Identity is asserted on the "
			+ "hold's own HoldId rather than on the returned TenantId, so a store that leaks the row while "
			+ "rewriting its tenant label cannot evade this.");
	}

	/// <summary>
	/// SAFETY and LIVENESS together, on one store holding all four shapes at once — the arrangement a real
	/// deployment presents, and the one where a predicate that is too wide or too narrow shows up as a count.
	/// </summary>
	[Fact]
	public async Task Return_exactly_the_holds_a_scoped_tenant_may_see()
	{
		var sentinelGlobal = CreateHold(TenantScope.UntenantedSentinel);
		var own = CreateHold(AmbientTenant);
		var foreign = CreateHold(OtherTenant);

		var visible = await SeedThenReadAsAmbientTenantAsync(sentinelGlobal, own, foreign)
			.ConfigureAwait(false);

		visible.ShouldBe(
			[sentinelGlobal.HoldId, own.HoldId],
			ignoreOrder: true,
			"a scoped tenant sees the global hold and its own, and nothing else. A count that is short "
			+ "means the global hold went missing; a count that is long means isolation broke.");
	}
}
