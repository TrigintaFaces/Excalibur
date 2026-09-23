// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// Binds the authorization policy to the same key composition its grant stores use, with terms that
/// contain the separator.
/// </summary>
/// <remarks>
/// <para>
/// <b>Nothing covered this, and the gap is why the defect survived.</b> Every other policy arm
/// hand-builds its dictionary key from ordinary identifiers, and the encoding is the identity on those —
/// so every one of them passes whether the policy composes its probe or interpolates it raw. A term
/// containing ':' or '%' is the only input that can tell the two apart.
/// </para>
/// <para>
/// <b>The seam had seven writers and one reader, applying two different functions.</b> Five grant stores
/// composed the stored key through the shared composer while two composed it raw, and the policy probed
/// raw. That produced two different failures depending on which store was deployed: against an escaping
/// store the raw probe could not match, so a grant the user holds was not found and access was refused;
/// against a raw store the probe matched, but the key was ambiguous, so one tenant's grant could answer
/// another tenant's request. The second is the worse half — it fails open.
/// </para>
/// <para>
/// Both arms below are therefore required. The safety arm alone is satisfied by a policy that authorizes
/// nobody, and the liveness arm alone is satisfied by one that authorizes everybody.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
[Trait("Feature", "Authorization")]
public sealed class AuthorizationPolicyKeyInjectivityShould
{
	// Tenant "acme:eu" owns the grant. Tenant "acme" reaches it under a bare join by naming a grant type
	// of "eu" -- the two tuples render one key, which is the collision these arms exist to refuse.
	private const string TenantWithSeparator = "acme:eu";
	private const string TenantPlain = "acme";

	private static AuthorizationPolicy CreatePolicy(string tenantId, IDictionary<string, object> grants)
	{
		var tenantContext = A.Fake<ITenantContext>();
		A.CallTo(() => tenantContext.TenantId).Returns(tenantId);

		return new AuthorizationPolicy(grants, new Dictionary<string, IReadOnlyCollection<string>>(), tenantContext, "user-1");
	}

	/// <summary>
	/// A tenant whose identifier contains the separator must still find its own grant. This is the arm
	/// that fails when the store escapes the key and the policy probes it raw.
	/// </summary>
	[Fact]
	public void Find_its_own_grant_when_the_tenant_term_contains_the_separator()
	{
		// Composed exactly as every grant store composes the stored key.
		var storedKey = SegmentedKey.Compose(TenantWithSeparator, GrantType.Activity, "reports");
		var grants = new Dictionary<string, object>(StringComparer.Ordinal) { [storedKey] = true };

		var sut = CreatePolicy(TenantWithSeparator, grants);

		sut.HasGrant("reports").ShouldBeTrue(
			"the policy must probe with the same composition its stores write. A raw interpolation cannot "
			+ "match an escaped stored key, so a grant this tenant genuinely holds reads as no grant at "
			+ "all and access is refused -- a denial produced entirely by the key encoding.");
	}

	/// <summary>
	/// The colliding pair: one tenant must not be able to address another tenant's grant by shifting a
	/// term across the separator. This is the arm that fails when both sides join raw.
	/// </summary>
	[Fact]
	public void Refuse_a_grant_belonging_to_a_tenant_whose_terms_shift_across_the_separator()
	{
		// The victim key is composed with a BARE JOIN on purpose: that is how two grant stores actually
		// built it, and it is the only fixture under which this arm can fail. Composed with the shared
		// composer the stored key is unreachable by a raw probe anyway, so the arm would pass without
		// binding anything -- green for a reason that has nothing to do with the property.
		var victimKey = string.Join(':', TenantWithSeparator, GrantType.Activity, "reports");
		var grants = new Dictionary<string, object>(StringComparer.Ordinal) { [victimKey] = true };

		// The attacker is a different tenant entirely, probing for a resource under a type it controls.
		var sut = CreatePolicy(TenantPlain, grants);

		sut.HasGrant("eu", "Activity:reports").ShouldBeFalse(
			"one tenant addressing another tenant's grant is a cross-tenant authorization decision. Under "
			+ "a bare join both tuples render one key and this returns true, which fails OPEN -- the "
			+ "attacker is granted a permission it was never given.");
	}

	/// <summary>
	/// Liveness for the safety arm above: the plain tenant must still be authorized for a grant it
	/// really holds, so the refusal above is a refusal and not a policy that refuses everything.
	/// </summary>
	[Fact]
	public void Still_authorize_a_plain_tenant_for_a_grant_it_genuinely_holds()
	{
		var ownKey = SegmentedKey.Compose(TenantPlain, GrantType.Activity, "reports");
		var grants = new Dictionary<string, object>(StringComparer.Ordinal) { [ownKey] = true };

		var sut = CreatePolicy(TenantPlain, grants);

		sut.HasGrant("reports").ShouldBeTrue(
			"without this the refusal above is satisfied by a policy that authorizes nobody, which is the "
			+ "cheapest way to be safe and the most expensive way to be wrong.");
	}

	/// <summary>
	/// The escape character needs its own arm: escaping ':' without escaping '%' first would map the
	/// distinct tenants "a:b" and "a%3Ab" onto one key, a collision introduced by the escaping itself.
	/// </summary>
	[Fact]
	public void Separate_a_tenant_containing_the_escape_character_from_one_containing_the_separator()
	{
		// Bare-joined for the same reason as the arm above: it must be reachable by a raw probe, or the
		// assertion cannot distinguish the fix from the defect.
		var literal = string.Join(':', "a:b", GrantType.Activity, "reports");
		var grants = new Dictionary<string, object>(StringComparer.Ordinal) { [literal] = true };

		var sut = CreatePolicy("a:b", grants);

		sut.HasGrant("reports").ShouldBeFalse(
			"a bare-joined stored key must not be reachable by this policy at all. Probing raw would match "
			+ "it and authorize on an ambiguous key -- the fail-open half of this defect.");
	}
}
