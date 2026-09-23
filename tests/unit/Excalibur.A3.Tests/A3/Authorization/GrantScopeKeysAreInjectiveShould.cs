// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.A3.Authorization.Grants;

namespace Excalibur.Tests.A3.Authorization;

/// <summary>
/// One key addresses one scope. Two distinct key strings never decode to the same grant.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT.</b> The parse used the count-limited <c>Split</c> overload, which does not reject a key
/// carrying more segments than expected — it collapses the surplus into the final term, which then still
/// holds a raw separator. A key written with a raw separator and the escaped key that names the same terms
/// therefore decoded to one scope, so two distinct strings addressed one grant.
/// </para>
/// <para>
/// <b>WHY IT MATTERS.</b> Grant keys are how one principal's authority is told apart from another's. A
/// composition that is not injective lets one key answer for a grant it does not name, and the failure is
/// silent: both spellings parse, neither errors, and the policy simply consults the wrong entry.
/// </para>
/// <para>
/// <b>THE FIX IS A DELEGATION, NOT A REWRITE.</b> The format's owner splits completely and compares the
/// length, rejecting a surplus segment rather than absorbing it — and it is sound precisely because the
/// escape guarantees no encoded segment can contain the separator. The parse now calls it instead of
/// re-deriving a weaker version beside it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class GrantScopeKeysAreInjectiveShould
{
    /// <summary>
    /// SAFETY, and the arm the defect is about. RED against the count-limited split, which decodes both
    /// spellings to the same scope.
    /// </summary>
    [Fact]
    public void RefuseAKeyCarryingASurplusSeparatorRatherThanAbsorbingItIntoTheLastTerm()
    {
        _ = Should.Throw<ArgumentException>(() => GrantScope.FromString("acme:Activity:a:b"),
            "a key with four segments is not a spelling of a three-term scope. Absorbing the surplus into "
            + "the qualifier makes it decode identically to 'acme:Activity:a%3Ab', so two distinct keys "
            + "address one grant and one principal's authority can answer for another's");
    }

    /// <summary>
    /// LIVENESS. The escaped form — the only shape the composer emits — must still round-trip, including
    /// the separator it was escaped to carry. Without this the safety arm is satisfied by a parser that
    /// rejects everything.
    /// </summary>
    [Fact]
    public void StillDecodeTheEscapedFormBackToItsOriginalTerms()
    {
        var scope = GrantScope.FromString("acme:Activity:a%3Ab");

        scope.TenantId.ShouldBe("acme");
        scope.GrantType.ShouldBe("Activity");
        scope.Qualifier.ShouldBe("a:b", "the escaped separator must survive the round trip as a literal");
    }

    /// <summary>
    /// LIVENESS. The property stated directly: what the type writes, the type reads back.
    /// </summary>
    [Fact]
    public void RoundTripEveryTermThroughItsOwnComposer()
    {
        var original = new GrantScope("urn:acme", GrantType.Activity, "orders:read");

        var parsed = GrantScope.FromString(original.ToString());

        parsed.TenantId.ShouldBe(original.TenantId);
        parsed.GrantType.ShouldBe(original.GrantType);
        parsed.Qualifier.ShouldBe(original.Qualifier);
    }

    /// <summary>
    /// LIVENESS. An ordinary separator-free key is unaffected — the common path must not have been
    /// tightened into a rejection.
    /// </summary>
    [Fact]
    public void StillDecodeAnOrdinaryKeyWithNoEscapedTerm()
    {
        var scope = GrantScope.FromString("acme:Activity:orders");

        scope.TenantId.ShouldBe("acme");
        scope.GrantType.ShouldBe("Activity");
        scope.Qualifier.ShouldBe("orders");
    }

    /// <summary>
    /// SAFETY. A missing segment must fail the length check rather than shift the remaining terms left,
    /// which would promote the grant type into the tenant position and address a different grant.
    /// </summary>
    [Fact]
    public void RefuseAKeyWithTooFewSegments()
    {
        _ = Should.Throw<ArgumentException>(() => GrantScope.FromString("acme:Activity"));
    }
}
