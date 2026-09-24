// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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

    // ---- EC-A1: a handler whose constructors the walk cannot inspect → Scope (uncertain, not proven safe). ----

    [Fact]
    public void HandlerWithNoInspectableConstructor_RequiresScope()
    {
        // No constructor the walk can select, so it cannot prove the closure root-safe. The walk's stated
        // invariant is "Root only on a provably root-safe closure; every uncertain branch yields Scope", and
        // this is an uncertain branch: such a type is activated by a factory registration, and a factory can
        // close over a Scoped service the walk cannot see. Under trimming the same branch is reached because
        // GetConstructors() returns empty for a type whose constructors were removed.
        //
        // This arm previously asserted the opposite and described itself as preserving pre-fix behavior. It
        // was a characterization test: Root here means the type is resolved once from the root container and
        // reused for every dispatch, which is the captive-dependency bug the walk exists to prevent.
        //
        // Its liveness twin is TransientHandlerWithOnlySingletonDependencies_DoesNotRequireScope, which still
        // demands Root for a provably root-safe closure -- so a walk that simply answered Scope for
        // everything would fail that arm rather than pass this one.
        var resolver = CreateScopeCapableResolver(services => services.AddTransient<NoPublicCtorHandler>());

        resolver.RequiresScope(typeof(NoPublicCtorHandler)).ShouldBeTrue();
    }

    [Fact]
    public void ScopedDependencyReachedThroughAnINTERFACEParameter_RequiresScope()
    {
        // THE ORDINARY SHAPE OF DI, and the one this class was silently wrong about.
        //
        // Walk recurses on the PARAMETER type, and in an interface-registered graph the parameter is the
        // interface. typeof(ISomeInterface).GetConstructors() returns EMPTY -- an interface declares no
        // instance constructors -- so the recursion lands on the uninspectable branch. Before that branch
        // yielded Scope, the walk answered Root here and the handler was resolved once from the root
        // container with a captive Scoped service inside it.
        //
        // Note why the older arms never caught this: TransitivelyScopedHandler takes TransientMiddle, a
        // CONCRETE class with a public constructor, so the walk could see through it. Swap the intermediate
        // for the interface every real registration actually uses and the walk went blind.
        var resolver = CreateScopeCapableResolver(services =>
        {
            _ = services.AddScoped<IScopedDep, ScopedDep>();
            _ = services.AddTransient<IRepoLike, RepoLike>();
            _ = services.AddTransient<HandlerDependingOnInterface>();
        });

        resolver.RequiresScope(typeof(HandlerDependingOnInterface)).ShouldBeTrue();
    }

    [Fact]
    public void OpenGenericInterfaceWhoseImplementationReachesAScopedService_RequiresScope()
    {
        // A TRIPWIRE, not a demonstration of a working mechanism -- and that distinction is the reason it
        // exists. This passes today, but by the wrong route: ResolveLifetime falls back to the open generic
        // definition, finds it Transient, and the walk then recurses into the CLOSED INTERFACE, which has no
        // constructors, and takes the uninspectable branch to Scope. Without that fallback the closed type
        // would be unresolvable, and the walk's Unknown branch would answer Scope for the right reason:
        // nothing here can prove the closure safe.
        //
        // So the correct answer is currently reached by an accident that masks a widening fallback. If the
        // registry is ever taught to name and close the implementation type, the uninspectable branch stops
        // firing for interfaces and this arm goes RED -- which is the entire point of committing it now,
        // since nothing else covers the case.
        var resolver = CreateScopeCapableResolver(services =>
        {
            _ = services.AddScoped<IScopedDep, ScopedDep>();
            _ = services.AddTransient(typeof(IGenericDep<>), typeof(GenericDep<>));
            _ = services.AddTransient<HandlerDependingOnClosedGeneric>();
        });

        resolver.RequiresScope(typeof(HandlerDependingOnClosedGeneric)).ShouldBeTrue();
    }

    [Fact]
    public void ValidateScopesIsOffByDefault_WhichIsWhyACaptureIsSilentRatherThanLoud()
    {
        // Pins the platform fact both the Singleton-prune comments and the uninspectable-branch comment
        // rely on. The container does NOT forbid a Singleton from capturing a Scoped, and does not report a
        // root-resolved capture: it validates only when asked, and it is not asked by default. The host
        // builder turns this on in Development only, so the silent case is the Production one.
        new ServiceProviderOptions().ValidateScopes.ShouldBeFalse();
    }

    [Fact]
    public void AnInterfaceTypeDeclaresNoConstructors_TheReasonTheWalkGoesBlind()
    {
        // Pins the platform fact the arm above depends on, so a reader does not have to take it on trust
        // and a future change to SelectActivatableConstructor cannot quietly invalidate the reasoning.
        typeof(IRepoLike).GetConstructors().ShouldBeEmpty();
    }

    [Fact]
    public void FactoryRegisteredHandlerCapturingAScopedDependency_RequiresScope()
    {
        // The concrete harm behind the arm above, with nothing hypothetical in it: the type is registered by
        // a factory that closes over a Scoped service. The walk cannot see inside the lambda, so the only
        // thing standing between this and a captive dependency is the uninspectable branch yielding Scope.
        var resolver = CreateScopeCapableResolver(services =>
        {
            _ = services.AddScoped<IScopedDep, ScopedDep>();
            _ = services.AddTransient(sp => NoPublicCtorHandler.Create(sp.GetRequiredService<IScopedDep>()));
        });

        resolver.RequiresScope(typeof(NoPublicCtorHandler)).ShouldBeTrue();
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

    /// <summary>
    /// A transient intermediate reached through its INTERFACE, which is how dependency injection is
    /// ordinarily written. The walk recurses on this interface type, not on <see cref="RepoLike" />.
    /// </summary>
    private interface IRepoLike;

    private sealed class RepoLike : IRepoLike
    {
        public RepoLike(IScopedDep dep) => _ = dep;
    }

    private sealed class HandlerDependingOnInterface
    {
        public HandlerDependingOnInterface(IRepoLike repo) => _ = repo;
    }

    private interface IGenericDep<T>;

    private sealed class GenericDep<T> : IGenericDep<T>
    {
        public GenericDep(IScopedDep dep) => _ = dep;
    }

    private sealed class HandlerDependingOnClosedGeneric
    {
        public HandlerDependingOnClosedGeneric(IGenericDep<string> dep) => _ = dep;
    }

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

        /// <summary>
        /// The only way to build one: a factory. That is precisely why the walk cannot prove anything about
        /// this type's dependencies -- whatever the factory closes over is invisible to a constructor walk.
        /// </summary>
        public static NoPublicCtorHandler Create(IScopedDep dep)
        {
            _ = dep;
            return new NoPublicCtorHandler();
        }
    }
}
