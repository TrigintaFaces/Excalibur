// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Locks that an activity-group decision is confined to the deciding tenant — including on the WILDCARD
/// path, which reads the estate-wide catalogue.
/// </summary>
/// <remarks>
/// <para>
/// The store returns the catalogue for the whole estate. The wildcard branch once walked all of it,
/// recovered a bare group name from each composed key, and matched a wildcard grant against that name
/// alone — so a user holding <c>*</c> in their OWN tenant was authorized for any activity conferred by a
/// same-named group in ANY tenant. No race, no name collision, no privileged actor: it needed only that
/// the user hold a wildcard activity-group grant.
/// </para>
/// <para>
/// <b>Why this shipped green for so long, and what that dictates about these arms.</b> On the SQL
/// providers the whole group path was inert — a type test rejected the value they returned — so every
/// confinement assertion passed while authorizing nothing at all. <b>A component that authorizes NOTHING
/// satisfies every safety arm perfectly.</b> That is why the liveness arms below are not decoration: they
/// are what stops this suite from going green on an inert path a second time.
/// </para>
/// <para>
/// Every arm asserts at the DECISION level, <see cref="AuthorizationPolicy.IsAuthorized"/>, never against
/// the store. The store already composed the tenant into its keys while the escalation was live, so a
/// store-level assertion is a proxy that does not imply the property being claimed.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyTenantConfinementShould
{
	private const string TenantA = "tenant-a";
	private const string TenantB = "tenant-b";
	private const string GroupName = "Support";
	private const string Activity = "IssueRefund";

	/// <summary>
	/// L1 — LIVENESS, and it gates the rest: an EXACT activity-group grant authorizes inside its own
	/// tenant.
	/// </summary>
	/// <remarks>
	/// If this is red, the safety arms below are REFUSE rather than PASS — they would be passing because
	/// nothing authorizes at all, which is precisely the state five green releases were in.
	/// </remarks>
	[Fact]
	public void AuthorizeAnExactGrantWithinItsOwnTenant()
	{
		var policy = PolicyFor(
			tenant: TenantB,
			qualifier: GroupName,
			catalogue: CatalogueOf((TenantB, GroupName, Activity)));

		policy.IsAuthorized(Activity, resourceId: null).ShouldBeTrue(
			"a user granted their own tenant's group must be authorized for the activities it confers; if "
			+ "this fails the suite is green only because nothing authorizes");
	}

	/// <summary>
	/// L2 — LIVENESS for the WILDCARD path specifically. This is the arm that makes the safety arm below
	/// non-vacuous.
	/// </summary>
	/// <remarks>
	/// The safety arm asserts a wildcard grant does NOT reach another tenant's group. A wildcard branch
	/// that matched nothing whatsoever — deleted, or short-circuited — would satisfy it completely. This
	/// arm pins the branch as live, so the safety arm can only be satisfied by confinement rather than by
	/// inaction.
	/// </remarks>
	[Fact]
	public void AuthorizeAWildcardGrantWithinItsOwnTenant()
	{
		var policy = PolicyFor(
			tenant: TenantB,
			qualifier: "*",
			catalogue: CatalogueOf((TenantB, GroupName, Activity)));

		policy.IsAuthorized(Activity, resourceId: null).ShouldBeTrue(
			"a full wildcard activity-group grant must still authorize through the user's OWN groups — "
			+ "without this, the confinement arm below passes against a wildcard path that does nothing");
	}

	/// <summary>
	/// S2 — SAFETY: a wildcard grant must NOT be satisfiable by another tenant's group membership.
	/// </summary>
	/// <remarks>
	/// The catalogue holds exactly one group and it belongs to tenant B. The user is in tenant A, holds a
	/// full wildcard in their own tenant, and tenant A has no activity group at all. The only way to
	/// authorize is to read tenant B's entry.
	/// </remarks>
	[Fact]
	public void RefuseAWildcardGrantReachingAnotherTenantsGroup()
	{
		var policy = PolicyFor(
			tenant: TenantA,
			qualifier: "*",
			catalogue: CatalogueOf((TenantB, GroupName, Activity)));

		policy.IsAuthorized(Activity, resourceId: null).ShouldBeFalse(
			"the activity is conferred only by tenant B's catalogue and the user is in tenant A; "
			+ "authorizing here is a cross-tenant privilege escalation");
	}

	/// <summary>
	/// S3 — SAFETY: an EXACT grant naming a group that exists only in another tenant must not authorize.
	/// </summary>
	/// <remarks>
	/// The sibling of S2 on the non-wildcard path. The two paths compose the tenant in different places,
	/// so a fix to one does not imply the other.
	/// </remarks>
	[Fact]
	public void RefuseAnExactGrantNamingAnotherTenantsGroup()
	{
		var policy = PolicyFor(
			tenant: TenantA,
			qualifier: GroupName,
			catalogue: CatalogueOf((TenantB, GroupName, Activity)));

		policy.IsAuthorized(Activity, resourceId: null).ShouldBeFalse(
			"the grant names a group by bare name, and the only group with that name belongs to tenant B");
	}

	/// <summary>
	/// S4 — SAFETY: same group NAME in two tenants must not let one tenant's members leak into the other.
	/// </summary>
	/// <remarks>
	/// Both tenants own a group called Support. Tenant A's confers nothing; tenant B's confers the
	/// activity. A name-keyed lookup returns whichever entry it reaches first and would authorize.
	/// </remarks>
	[Fact]
	public void RefuseWhenBothTenantsOwnAGroupOfTheSameName()
	{
		var policy = PolicyFor(
			tenant: TenantA,
			qualifier: GroupName,
			catalogue: CatalogueOf(
				(TenantA, GroupName, "ViewTicket"),
				(TenantB, GroupName, Activity)));

		policy.IsAuthorized(Activity, resourceId: null).ShouldBeFalse(
			"tenant A's Support group confers ViewTicket and nothing else; IssueRefund belongs to tenant "
			+ "B's group of the same name");

		policy.IsAuthorized("ViewTicket", resourceId: null).ShouldBeTrue(
			"control: tenant A's OWN group still authorizes, so the refusal above is confinement and not "
			+ "a policy that denies everything");
	}

	#region Helpers

	/// <summary>
	/// Builds the estate-wide catalogue exactly as the stores produce it: keyed by the composed
	/// (tenant, name) pair, carrying every tenant's groups.
	/// </summary>
	private static Dictionary<string, IReadOnlyCollection<string>> CatalogueOf(
		params (string Tenant, string Group, string Activity)[] entries)
	{
		var catalogue = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal);

		foreach (var (tenant, group, activity) in entries)
		{
			var key = SegmentedKey.Compose(tenant, group);

			catalogue[key] = catalogue.TryGetValue(key, out var existing)
				? [.. existing, activity]
				: [activity];
		}

		return catalogue;
	}

	/// <summary>
	/// Builds a policy for a user in <paramref name="tenant"/> holding a single activity-group grant with
	/// the given qualifier, over the supplied estate-wide catalogue.
	/// </summary>
	private static AuthorizationPolicy PolicyFor(
		string tenant,
		string qualifier,
		IReadOnlyDictionary<string, IReadOnlyCollection<string>> catalogue)
	{
		var grants = new Dictionary<string, object>(StringComparer.Ordinal)
		{
			[SegmentedKey.Compose(tenant, GrantType.ActivityGroup, qualifier)] = new object(),
		};

		var tenantContext = A.Fake<ITenantContext>();
		_ = A.CallTo(() => tenantContext.TenantId).Returns(tenant);

		return new AuthorizationPolicy(grants, catalogue, tenantContext, "user-1");
	}

	#endregion
}
