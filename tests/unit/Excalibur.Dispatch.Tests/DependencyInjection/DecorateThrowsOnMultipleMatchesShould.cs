// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Tests.DependencyInjection;

/// <summary>
/// Author != implementer regression lock for the generic <c>Decorate&lt;TService, TDecorator&gt;</c> helper's
/// multi-registration guard (egm9wd, S893).
/// </summary>
/// <remarks>
/// <para>
/// <b>The defect this locks.</b> At committed HEAD, <c>ServiceCollectionDecoratorExtensions.Decorate&lt;T&gt;</c>
/// collects every matching descriptor, then silently decorates only the <b>last</b> one
/// (<c>matches[^1]</c>) — leaving co-registered siblings RAW. The helper's own comment already calls this
/// "a KNOWN, UNCLOSED GAP … for a tenant-scoping decorator that is a cross-tenant leak." A generic helper
/// cannot know which of N registrations is canonical; guessing is the leak. The ruling makes silently
/// decorating one-of-N <b>inexpressible</b>: on more than one match the helper MUST fail fast and direct the
/// caller to a key-targeted / all-descriptor path.
/// </para>
/// <para>
/// <b>Both arms, deliberately (testing-patterns §3).</b> The SAFETY arm — "&gt;1 match throws" — is satisfied
/// by a helper that throws on EVERYTHING, which would break every legitimate single-registration decoration in
/// the framework. It is paired with LIVENESS arms — a single registration still decorates, and a single KEYED
/// registration still decorates AND remains resolvable by its key. Safety without liveness certifies a helper
/// that does nothing useful.
/// </para>
/// <para>
/// <b>Non-vacuity.</b> The SAFETY arm is RED at committed HEAD: there is no <c>matches.Count &gt; 1</c> guard,
/// so the two-registration case decorates the last match and does not throw. It goes GREEN only when the guard
/// is added. The LIVENESS arms are GREEN at HEAD and pin that the guard does not over-correct into rejecting
/// valid single-match decorations. This class uses local fakes with exactly one concrete registration under
/// test, so it binds the <i>generic helper's</i> contract — not any provider-specific registration shape.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DecorateThrowsOnMultipleMatchesShould
{
    /// <summary>
    /// SAFETY. More than one matching registration must fail fast rather than silently decorate one.
    /// </summary>
    /// <remarks>
    /// RED at HEAD: <c>Decorate</c> picks <c>matches[^1]</c> and returns, decorating the last registration and
    /// leaving the first RAW — the cross-tenant-leak shape when the decorator is a tenant scope. A mutant that
    /// keeps last-wins (or that decorates every match without failing fast) leaves this arm RED.
    /// </remarks>
    [Fact]
    public void FailFast_WhenMoreThanOneRegistrationMatches()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThing>(new Thing("first"));
        services.AddSingleton<IThing>(new Thing("second"));

        var ex = Should.Throw<InvalidOperationException>(
            () => services.Decorate<IThing, ThingDecorator>(),
            "A generic Decorate<T> cannot know which of several registrations is canonical. Silently decorating " +
            "the last match leaves every other registration undecorated — for a tenant-scoping decorator that " +
            "is a cross-tenant leak. On more than one match it MUST fail fast and direct the caller to a " +
            "key-targeted / all-descriptor decoration path.");

        ex.Message.ShouldContain(
            nameof(IThing),
            Case.Insensitive,
            "The failure must name the ambiguous service type so the caller knows which registration to " +
            "disambiguate.");
    }

    /// <summary>
    /// LIVENESS. Exactly one (unkeyed) registration must still be decorated — the guard must not reject valid
    /// single-match decoration.
    /// </summary>
    /// <remarks>
    /// GREEN at HEAD. Without this arm, the safety arm above is satisfied by a helper that throws on every
    /// input, which would break every legitimate decoration in the framework.
    /// </remarks>
    [Fact]
    public void StillDecorate_WhenExactlyOneUnkeyedRegistrationMatches()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IThing>(new Thing("only"));

        _ = services.Decorate<IThing, ThingDecorator>();

        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<IThing>().ShouldBeOfType<ThingDecorator>(
            "A single matching registration is unambiguous and must be decorated. If this is RED the guard " +
            "over-corrected and now rejects valid single-match decoration.");
    }

    /// <summary>
    /// LIVENESS. Exactly one KEYED registration must be decorated AND remain resolvable by its key.
    /// </summary>
    /// <remarks>
    /// GREEN at HEAD. A keyed descriptor satisfies the service-type match; the helper must re-register the
    /// decorated descriptor WITH the same key, or a keyed lookup resolves nothing. This is the single-match
    /// counterpart to the multi-match safety arm — the guard changes the count check, not the keyed-preservation
    /// behaviour, and this pins that it stays intact.
    /// </remarks>
    [Fact]
    public void PreserveTheServiceKey_WhenExactlyOneKeyedRegistrationMatches()
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IThing>("primary", (_, _) => new Thing("keyed"));

        _ = services.Decorate<IThing, ThingDecorator>();

        using var provider = services.BuildServiceProvider();

        provider.GetKeyedService<IThing>("primary").ShouldBeOfType<ThingDecorator>(
            "A single keyed registration is unambiguous: it must be decorated and stay resolvable by its key. " +
            "If the decorated descriptor drops the ServiceKey, GetKeyedService(\"primary\") resolves nothing.");
    }

    private interface IThing
    {
        string Name { get; }
    }

    private sealed class Thing(string name) : IThing
    {
        public string Name { get; } = name;
    }

    private sealed class ThingDecorator(IThing inner) : IThing
    {
        public string Name => inner.Name;
    }
}
