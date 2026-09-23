// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization;
using Excalibur.A3.Authorization.Grants;
using Excalibur.Dispatch;

using FakeItEasy;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// The policy reads a scope key with the same parser that writes it, so a grant it was given is a grant it
/// honours.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The policy re-derived the scope parse instead of calling the type that owns the
/// format, and drifted from it three ways: it passed <c>RemoveEmptyEntries</c> (which does not blank an
/// empty segment, it REMOVES it and shifts the rest left), it split on a literal <c>':'</c> rather than the
/// shared separator, and it never unescaped. Every divergence fails the same direction — the scope does not
/// parse, or parses into the wrong roles, and both call sites treat that as "skip".
/// </para>
/// <para>
/// <b>WHY NOTHING CAUGHT IT.</b> A skipped grant is a DENIAL, and a denial is what a correctly-restrictive
/// policy produces too. The failure is indistinguishable from the intended behaviour at the boundary, so no
/// arm asserting "access refused" could ever have told the two apart — such an arm passes BECAUSE of the
/// defect. These arms therefore assert the GRANT IS HONOURED, which is the only outcome the defect cannot
/// produce.
/// </para>
/// <para>
/// <b>Separator-bearing identifiers are ordinary, not adversarial</b> — URN tenant ids, OIDC subject
/// claims, SAML name ids. Nothing in the framework rejects one.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthorizationPolicyParsesScopesThroughTheirOwnerShould
{
    private const string TenantWithSeparator = "urn:acme";
    private const string PlainTenant = "acme";
    private const string Activity = "orders:read";
    private const string ActivityGroupName = "order-reader";

    private static AuthorizationPolicy CreatePolicy(string tenantId, IDictionary<string, object> grants)
    {
        var tenantContext = A.Fake<ITenantContext>();
        A.CallTo(() => tenantContext.TenantId).Returns(tenantId);

        return new AuthorizationPolicy(grants, new Dictionary<string, IReadOnlyCollection<string>>(), tenantContext, "user-1");
    }

    /// <summary>
    /// Composes the key the way the framework composes it — through the owner — so the arm binds the
    /// round trip rather than this test's guess about the format.
    /// </summary>
    private static IDictionary<string, object> GrantFor(string tenantId, string grantType, string qualifier) =>
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [new GrantScope(tenantId, grantType, qualifier).ToString()] = true,
        };

    /// <summary>
    /// SAFETY, and the arm the defect is about. A tenant id containing the separator must be read back as
    /// ITSELF. Asserted as a grant HONOURED, because a grant lost is a denial and a denial proves nothing.
    /// </summary>
    [Fact]
    public void HonourAGrantWhoseTenantIdContainsTheSeparator()
    {
        var grants = GrantFor(TenantWithSeparator, GrantType.Activity, Activity);
        var policy = CreatePolicy(TenantWithSeparator, grants);

        policy.IsAuthorized(Activity).ShouldBeTrue(
            "the grant was composed for this exact tenant and the policy was constructed for it, so the only "
            + "thing that can lose it is the read. A count-limited split reads 'urn:acme:activity:orders:read' "
            + "as tenant 'urn', type 'acme' — a grant for one tenant silently becomes a grant for another, "
            + "and the subject is denied access they were granted");
    }

    /// <summary>
    /// SAFETY. The other divergence, reached by an ordinary value rather than a separator: a term the writer
    /// ESCAPED must be unescaped on the way back, or it compares unequal to itself.
    /// </summary>
    [Fact]
    public void HonourAGrantWhoseQualifierContainsTheSeparator()
    {
        var grants = GrantFor(PlainTenant, GrantType.Activity, Activity);
        var policy = CreatePolicy(PlainTenant, grants);

        policy.IsAuthorized(Activity).ShouldBeTrue(
            "the qualifier carries a separator, so the writer escaped it. A reader that never unescapes "
            + "produces a scope that looks well-formed and matches nothing — not a parse failure, a silent "
            + "mismatch");
    }

    /// <summary>
    /// LIVENESS. The controls matter more than usual here: every assertion above is satisfied by a policy
    /// that authorises EVERYTHING, which is the catastrophic direction. This pins that an activity nobody
    /// was granted is still refused.
    /// </summary>
    [Fact]
    public void StillRefuseAnActivityThatWasNeverGranted()
    {
        var grants = GrantFor(TenantWithSeparator, GrantType.Activity, Activity);
        var policy = CreatePolicy(TenantWithSeparator, grants);

        policy.IsAuthorized("orders:delete").ShouldBeFalse(
            "a policy that authorises anything would satisfy every arm above while removing authorization "
            + "entirely");
    }

    /// <summary>
    /// LIVENESS. A grant belonging to a DIFFERENT tenant must not be honoured — the isolation the parse
    /// feeds. Without this, "read the tenant correctly" could be satisfied by ignoring the tenant.
    /// </summary>
    [Fact]
    public void StillRefuseAGrantBelongingToAnotherTenant()
    {
        var grants = GrantFor("urn:other", GrantType.Activity, Activity);
        var policy = CreatePolicy(TenantWithSeparator, grants);

        policy.IsAuthorized(Activity).ShouldBeFalse(
            "the grant names a different tenant, and the parse exists to keep that distinction. A reader "
            + "that dropped the tenant term would pass the safety arms and cross the boundary they protect");
    }

    /// <summary>
    /// SAFETY, and the only arm here that is NON-VACUOUS against the defect. Measured: restoring the
    /// pre-fix parser leaves the five arms around it GREEN and turns this one RED.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>WHY THE OTHER SAFETY ARMS CANNOT SEE THE DEFECT.</b> For a non-wildcard grant the policy stores
    /// <c>exactGrants[key] = value</c> — keyed by the RAW key — and discards the parsed scope entirely. The
    /// parse result reaches an observable decision only where the scope is retained: the activity-group
    /// path, which compares the parsed <c>TenantId</c> against the policy's tenant. An arm built on an
    /// exact grant therefore asserts a lookup that never consults the parser, and passes whatever the
    /// parser does.
    /// </para>
    /// <para>
    /// So the fixture must be an ACTIVITY GROUP grant. A reader that does not unescape yields the tenant
    /// as <c>urn%3Aacme</c>, which compares unequal to <c>urn:acme</c>, and the grant is silently dropped.
    /// </para>
    /// </remarks>
    [Fact]
    public void HonourAnActivityGroupGrantWhoseTenantIdContainsTheSeparator()
    {
        var grants = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [new GrantScope(TenantWithSeparator, GrantType.ActivityGroup, ActivityGroupName).ToString()] = true,
        };
        var groups = new Dictionary<string, IReadOnlyCollection<string>>(StringComparer.Ordinal)
        {
            // Composed through the owner, exactly as the store now composes it: a tenant containing the
            // separator is escaped on both sides, so the pair still addresses one group and no other.
            [SegmentedKey.Compose(TenantWithSeparator, ActivityGroupName)] = new List<string> { Activity },
        };

        var tenantContext = A.Fake<ITenantContext>();
        A.CallTo(() => tenantContext.TenantId).Returns(TenantWithSeparator);

        var policy = new AuthorizationPolicy(grants, groups, tenantContext, "user-1");

        policy.IsAuthorized(Activity).ShouldBeTrue(
            "the activity-group path compares the PARSED scope's TenantId against the policy's tenant, so a "
            + "reader that never unescapes produces 'urn%3Aacme', compares it unequal to 'urn:acme', and "
            + "drops a grant the subject was given");
    }

    /// <summary>
    /// LIVENESS control for the ordinary case, which is what every pre-existing arm exercises: a plain
    /// identifier with no separator still works. If this ever fails, the fix broke the common path.
    /// </summary>
    [Fact]
    public void StillHonourAnOrdinaryGrantWithNoSeparatorInAnyTerm()
    {
        var grants = GrantFor(PlainTenant, GrantType.Activity, "orders");
        var policy = CreatePolicy(PlainTenant, grants);

        policy.IsAuthorized("orders").ShouldBeTrue("the encoding is the identity on ordinary identifiers");
    }
}
