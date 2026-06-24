// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Delivery;

/// <summary>
/// Structural-invariant lock for the <c>pedo87</c> captive-dependency fix (Sprint 845, MS-A FR-A2/A3/A5).
/// Verifies <see cref="HandlerScopeResolver.RequiresScope(System.Type)"/> — the unchanged public seam — across
/// the transitive-scope contract: a Scoped dependency reachable through Transient intermediaries forces a scope
/// (the captive-dependency bug), while a provably root-safe closure stays root (no over-scoping), cycles
/// terminate, and the actually-activatable constructor is the one analyzed.
/// </summary>
/// <remarks>
/// <para>
/// Non-vacuity (<c>enforce-invariants-structurally</c>): <see cref="TransitiveScopedDependencyThroughTransient_RequiresScope"/>
/// is <b>RED on the pre-fix depth-1 <c>HasScopedConstructorDependency</c></b> (which inspects only the handler's
/// own constructor parameters and returns <c>Root</c> for the Transient→Transient→Scoped graph) and GREEN on the
/// hybrid recursive walk. The all-Singleton and cycle cases guard against a vacuous "always Scope" implementation.
/// </para>
/// <para>
/// These are pure registration-metadata verdicts (no construction, no timing) — deterministic by construction.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerScopeResolverScopeRequirementShould
{
    // ---- AC-A1 / FR-A2: the keystone RED lock (transitive Scoped via a Transient intermediary). ----

    [Fact]
    public void TransitiveScopedDependencyThroughTransient_RequiresScope()
    {
        // Handler(Transient) -> Middle(Transient) -> IScopedDep(Scoped):
        // depth-1 inspection sees only Middle (Transient, not Scoped) -> the pre-fix code returns Root (FALSE),
        // which is the captive-dependency bug. The transitive walk must reach IScopedDep and return Scope (TRUE).
        var resolver = CreateScopeCapableResolver(services =>
        {
            services.AddScoped<IScopedDep, ScopedDep>();
            services.AddTransient<TransientMiddle>();
            services.AddTransient<TransitivelyScopedHandler>();
        });

        resolver.RequiresScope(typeof(TransitivelyScopedHandler)).ShouldBeTrue();
    }

    // ---- Anti-vacuity: a provably root-safe closure must NOT be over-scoped (guards a hardcoded "true"). ----

    [Fact]
    public void TransientHandlerWithOnlySingletonDependencies_DoesNotRequireScope()
    {
        // Handler(Transient) -> SingletonLeaf(Singleton): no Scoped anywhere in the closure -> Root (FALSE).
        var resolver = CreateScopeCapableResolver(services =>
        {
            services.AddSingleton<SingletonLeaf>();
            services.AddTransient<RootSafeHandler>();
        });

        resolver.RequiresScope(typeof(RootSafeHandler)).ShouldBeFalse();
    }

    // ---- AC-A6 / FR-A5: a dependency cycle must terminate with a defined verdict (no stack overflow). ----

    [Fact]
    public void HandlerGraphWithDependencyCycle_TerminatesWithDefinedVerdict()
    {
        // CycleA <-> CycleB (both Transient, no Scoped reachable). The recursive walk must terminate via the
        // visited-set cycle guard and return a defined verdict (Root / FALSE — nothing scoped in the closure).
        var resolver = CreateScopeCapableResolver(services =>
        {
            services.AddTransient<CycleA>();
            services.AddTransient<CycleB>();
        });

        bool verdict = false;
        Should.NotThrow(() => verdict = resolver.RequiresScope(typeof(CycleA)));
        verdict.ShouldBeFalse();
    }

    // ---- AC-A3 / FR-A3: analyze the longest *activatable* constructor, not unconditionally the longest. ----

    [Fact]
    public void HandlerWithUnresolvableLongestConstructor_AnalyzesShorterActivatableConstructor()
    {
        // MultiCtorHandler has a longer ctor (SingletonLeaf, IUnregisteredDep) whose IUnregisteredDep is NOT
        // registered -> not activatable, and a shorter all-resolvable ctor (IScopedDep). The pre-fix code picks
        // the longest unconditionally, finds no Scoped there -> Root (FALSE). The fix must select the shorter
        // activatable ctor, reach IScopedDep -> Scope (TRUE).
        var resolver = CreateScopeCapableResolver(services =>
        {
            services.AddScoped<IScopedDep, ScopedDep>();
            services.AddSingleton<SingletonLeaf>();
            services.AddTransient<MultiCtorHandler>();
            // IUnregisteredDep intentionally NOT registered.
        });

        resolver.RequiresScope(typeof(MultiCtorHandler)).ShouldBeTrue();
    }

    // ---- Regression guard: a directly Scoped-registered handler is detected (existing behavior preserved). ----

    [Fact]
    public void ScopedRegisteredHandler_RequiresScope()
    {
        var resolver = CreateScopeCapableResolver(services => services.AddScoped<ScopedHandler>());

        resolver.RequiresScope(typeof(ScopedHandler)).ShouldBeTrue();
    }

    // ---- EC-A2: with no scope capability the bus must behave exactly as before (resolve from root). ----

    [Fact]
    public void WhenNoScopeCanBeCreated_DoesNotRequireScope()
    {
        // No IServiceScopeFactory and no IDispatchAmbientScopeAccessor -> CanCreateScope == false -> the resolver
        // must short-circuit to FALSE (never throw, never recurse) so the singleton bus resolves from root.
        var root = A.Fake<IServiceProvider>(); // every GetService(...) returns null by default.
        var resolver = new HandlerScopeResolver(root);

        resolver.RequiresScope(typeof(TransitivelyScopedHandler)).ShouldBeFalse();
    }

    // ---- EC-A1: a handler with no public (activatable) constructor → Root (existing behavior preserved). ----

    [Fact]
    public void HandlerWithNoPublicConstructor_DoesNotRequireScope()
    {
        // No public constructor → the resolver cannot select an activatable ctor to inspect → it must fall
        // back to Root (false) rather than over-scope. Preserves the pre-fix EC-A1 behavior under the walk.
        var resolver = CreateScopeCapableResolver(services => services.AddTransient<NoPublicCtorHandler>());

        resolver.RequiresScope(typeof(NoPublicCtorHandler)).ShouldBeFalse();
    }

    /// <summary>
    /// Builds a scope-capable <see cref="HandlerScopeResolver"/>: a real provider (so <c>IServiceScopeFactory</c>
    /// is present → <c>CanCreateScope == true</c>) plus a <see cref="HandlerLifetimeRegistry"/> built from the
    /// same registration snapshot, so the transitive walk can resolve dependency lifetimes by type.
    /// </summary>
    private static HandlerScopeResolver CreateScopeCapableResolver(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection();
        configure(services);
        var registry = new HandlerLifetimeRegistry(services);
        services.AddSingleton(registry);
        var provider = services.BuildServiceProvider();
        return new HandlerScopeResolver(provider);
    }

    private interface IScopedDep;

    private sealed class ScopedDep : IScopedDep;

    private sealed class TransientMiddle
    {
        public TransientMiddle(IScopedDep dep) => _ = dep;
    }

    private sealed class TransitivelyScopedHandler
    {
        public TransitivelyScopedHandler(TransientMiddle middle) => _ = middle;
    }

    private sealed class SingletonLeaf;

    private sealed class RootSafeHandler
    {
        public RootSafeHandler(SingletonLeaf leaf) => _ = leaf;
    }

    private sealed class CycleA
    {
        public CycleA(CycleB b) => _ = b;
    }

    private sealed class CycleB
    {
        public CycleB(CycleA a) => _ = a;
    }

    private interface IUnregisteredDep;

    private sealed class MultiCtorHandler
    {
        public MultiCtorHandler(IScopedDep scoped) => _ = scoped;

        public MultiCtorHandler(SingletonLeaf leaf, IUnregisteredDep missing)
        {
            _ = leaf;
            _ = missing;
        }
    }

    private sealed class ScopedHandler;

    private sealed class NoPublicCtorHandler
    {
        private NoPublicCtorHandler()
        {
        }
    }
}
