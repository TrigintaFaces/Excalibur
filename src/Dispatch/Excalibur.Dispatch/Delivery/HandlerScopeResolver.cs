// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;
using Excalibur.Dispatch.Options.Configuration;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Decides whether a message handler must be resolved from a dependency-injection scope (rather than the
/// root container captured by the singleton <see cref="LocalMessageBus"/>) and runs the handler invocation
/// inside the correct scope. This eliminates the captive-dependency failure
/// (<c>"Cannot resolve scoped service '…' from root provider"</c>) for scoped handlers — and handlers whose
/// constructor dependency graph reaches a scoped service directly or transitively — dispatched through the
/// ultra-local fast paths.
/// </summary>
/// <remarks>
/// <para>
/// The verdict for each handler type is computed once and cached, purely from registration metadata (no
/// construction, no exceptions): a handler registered <see cref="ServiceLifetime.Scoped"/> needs a scope;
/// a <see cref="ServiceLifetime.Singleton"/> is always root-safe; any handler whose constructor dependency
/// graph reaches — directly or transitively — a service registered <see cref="ServiceLifetime.Scoped"/>
/// needs a scope. No reflection or allocation occurs on the warm dispatch path — only on the first dispatch
/// of each handler type.
/// </para>
/// <para>
/// When a scope is required, the handler is resolved from the <em>ambient</em> scope supplied by an
/// <see cref="IDispatchAmbientScopeAccessor"/> (for example the active ASP.NET Core request scope) when one
/// is available — so request-scoped state is shared — otherwise from a freshly created scope that is
/// disposed once the handler completes (the canonical pattern for a singleton consuming scoped services).
/// </para>
/// </remarks>
internal sealed class HandlerScopeResolver
{
    private enum Requirement : byte
    {
        Root = 0,
        Scope = 1,
    }

    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly IDispatchAmbientScopeAccessor? _ambientScope;
    private readonly HandlerLifetimeRegistry? _lifetimes;
    private readonly bool _promotionEnabled;
    private readonly ConcurrentDictionary<Type, Requirement> _cache = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerScopeResolver"/> class.
    /// </summary>
    /// <param name="root">The root service provider captured by the singleton message bus.</param>
    public HandlerScopeResolver(IServiceProvider root)
    {
        ArgumentNullException.ThrowIfNull(root);
        _scopeFactory = root.GetService(typeof(IServiceScopeFactory)) as IServiceScopeFactory;
        _ambientScope = root.GetService(typeof(IDispatchAmbientScopeAccessor)) as IDispatchAmbientScopeAccessor;
        _lifetimes = root.GetService(typeof(HandlerLifetimeRegistry)) as HandlerLifetimeRegistry;
        _promotionEnabled =
            (root.GetService(typeof(IOptions<DispatchOptions>)) as IOptions<DispatchOptions>)
                ?.Value.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton ?? false;
    }

    /// <summary>
    /// Whether a single shared handler instance may be substituted for per-dispatch activation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Substituting a shared instance changes the lifetime the consumer registered, so it is permitted
    /// only for <see cref="ServiceLifetime.Transient"/>. Transient is the discovery default, so a consumer
    /// who writes <c>AddTransient</c> has asked for nothing different from what they would have received
    /// anyway — there is no preference to override. <c>AddScoped</c> and <c>AddSingleton</c> ARE departures
    /// from the default and are honoured as written; Scoped in particular is chosen precisely when
    /// per-request isolation matters, and silently sharing one instance would take that away.
    /// </para>
    /// <para>
    /// This is the same rule <c>HandlerLifetimeAnalyzer.PromoteEligibleHandlers</c> applies to the
    /// DI-descriptor rewrite, which skips any descriptor whose lifetime is not Transient. The two
    /// promotion paths must not disagree about which registrations they are entitled to change.
    /// </para>
    /// <para>
    /// An unknown lifetime returns <see langword="false"/>. That is the safe direction: the cost is a
    /// handler activation we could have avoided, rather than a lifetime the consumer asked for and did
    /// not get.
    /// </para>
    /// </remarks>
    internal bool MayPromoteToSharedInstance(Type handlerType) =>
        _promotionEnabled &&
        TryGetRegisteredLifetime(handlerType, out var lifetime) &&
        lifetime == ServiceLifetime.Transient;

    /// <summary>
    /// Reports the lifetime the consumer registered for <paramref name="handlerType"/>, when it is known.
    /// </summary>
    /// <remarks>
    /// Exposed so a caller can distinguish "not promotable because of its shape" from "not promotable
    /// because the consumer asked for a different lifetime" — only the second is worth telling them about,
    /// and only the second names a lifetime in the message.
    /// </remarks>
    internal bool TryGetRegisteredLifetime(Type handlerType, out ServiceLifetime lifetime)
    {
        if (_lifetimes is not null && _lifetimes.TryGetLifetime(handlerType, out var registered))
        {
            lifetime = registered;
            return true;
        }

        lifetime = default;
        return false;
    }

    /// <summary>
    /// Gets a value indicating whether a scope can be obtained at all. When neither an ambient scope
    /// accessor nor a scope factory is available the bus must behave exactly as before (resolve from root).
    /// </summary>
    public bool CanCreateScope => _scopeFactory is not null || _ambientScope is not null;

    /// <summary>
    /// Returns whether the specified handler type must be resolved from a scope. Cached per handler type.
    /// </summary>
    public bool RequiresScope([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        if (!CanCreateScope || _lifetimes is null)
        {
            return false;
        }

        if (_cache.TryGetValue(handlerType, out var cached))
        {
            return cached == Requirement.Scope;
        }

        // Compute directly (not via a GetOrAdd factory lambda) so the trimmer-tracked annotation on
        // handlerType flows into Compute. Idempotent under races.
        var requirement = Compute(handlerType);
        _ = _cache.TryAdd(handlerType, requirement);
        return requirement == Requirement.Scope;
    }

    /// <summary>
    /// Runs <paramref name="invoke"/> using a service provider that can satisfy a scoped handler, in
    /// precedence order: the caller-supplied <paramref name="preferredScope"/> (the request scope a
    /// context-bound dispatch already carries) when present; otherwise the ambient scope; otherwise a
    /// freshly created scope. The first two are borrowed (never disposed here); a created scope is disposed
    /// after the invocation completes.
    /// </summary>
    /// <param name="handlerType">The handler type being resolved (used only for diagnostics).</param>
    /// <param name="preferredScope">
    /// An explicit scope supplied by the caller (typically <c>IMessageContext.RequestServices</c>). When
    /// non-<see langword="null"/> it is used directly so the handler shares the caller's request scope; the
    /// caller is responsible for filtering out the root provider before passing it.
    /// </param>
    /// <param name="state">
    /// Caller state handed back to <paramref name="invoke"/>. It exists so callers can pass a
    /// <see langword="static"/> (non-capturing) lambda: a capturing lambda allocates a closure object and a
    /// fresh delegate on every dispatch, and this method is the seam every scoped dispatch funnels through.
    /// </param>
    /// <param name="invoke">The handler invocation, given the resolved scope's service provider and <paramref name="state"/>.</param>
    public async ValueTask<T> RunAsync<TState, T>(
        Type handlerType,
        IServiceProvider? preferredScope,
        TState state,
        Func<IServiceProvider, TState, ValueTask<T>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        // 1. An explicit caller-supplied scope (the dispatch context's request scope) — borrowed, never
        //    disposed here. Honors the request scope a context-bound dispatch already carries so the
        //    handler's IMessageContext.RequestServices is the same instance the caller is using.
        if (preferredScope is not null)
        {
            return await invoke(preferredScope, state).ConfigureAwait(false);
        }

        var ambient = _ambientScope?.CurrentServiceProvider;
        if (ambient is not null)
        {
            return await invoke(ambient, state).ConfigureAwait(false);
        }

        if (_scopeFactory is not null)
        {
            var scope = _scopeFactory.CreateAsyncScope();
            try
            {
                return await invoke(scope.ServiceProvider, state).ConfigureAwait(false);
            }
            finally
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
        }

        throw CreateNoScopeDiagnostic(handlerType);
    }

    private Requirement Compute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // 1. A handler registered Scoped is resolved (when self-registered) honoring that lifetime; from
        //    the root container that throws the captive-dependency error.
        //
        //    A Singleton yields Root, and the reason is NOT that the container forbids a Singleton from
        //    capturing a Scoped. It does not: ServiceProviderOptions.ValidateScopes defaults to false and is
        //    turned on only by the host builder in Development, so in Production that capture is permitted
        //    and silent. The real reason is simpler and does not depend on a container setting: a Singleton
        //    resolves to ONE instance owned by the root, whichever provider you ask. No verdict this class
        //    produces can change its closure, so asking for a scope on its behalf would buy nothing. If it
        //    does capture a Scoped, that is the Singleton's own defect and a scope here cannot repair it.
        if (_lifetimes!.TryGetLifetime(handlerType, out var lifetime))
        {
            switch (lifetime)
            {
                case ServiceLifetime.Scoped:
                    return Requirement.Scope;

                case ServiceLifetime.Singleton:
                    return Requirement.Root;
            }
        }

        // 2. A transient or factory-activated handler needs a scope when ANY transitively reachable
        //    constructor dependency is Scoped (resolving it from root is the captive-dependency bug). The
        //    walk yields Root only on a provably root-safe closure; every uncertain branch yields Scope, so
        //    the captive-dependency violation is inexpressible (enforce-invariants-structurally).
        return Walk(handlerType, [handlerType]);
    }

    /// <summary>
    /// Recursively walks the constructor-dependency graph from the actually-activatable constructor, marking
    /// <see cref="Requirement.Scope"/> if any reachable dependency is Scoped or cannot be proven root-safe.
    /// Hybrid: a recursive walk (precise) with a conservative Scope-on-doubt fallback for unprovable branches
    /// (factory registrations, unregistered types). The <paramref name="visited"/> set guards cycles and
    /// diamonds so the walk always terminates with a defined verdict.
    /// </summary>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "The value is a constructor parameter's type, reached by reflection, so DynamicallyAccessedMembers cannot " +
            "flow to it. Its constructors are preserved by the container registration that makes it injectable in the first place. " +
            "This is safe without relying on that: a type whose constructors this walk cannot read takes the uninspectable branch " +
            "and is classified scope-requiring, so a trimmed type degrades to an unnecessary scope, never to a captive dependency.")]
    private Requirement Walk(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        HashSet<Type> visited)
    {
        var ctor = SelectActivatableConstructor(type);
        if (ctor is null)
        {
            // No constructor this walk can inspect. That is an UNCERTAIN branch, not a proven-safe one, so it
            // yields Scope like every other uncertain branch above.
            //
            // It used to yield Root, reasoning "no public constructor means nothing scoped to capture". That
            // reasoning is false in both ways this branch is reached. A type with genuinely no public
            // constructor is activated by a factory registration, and a factory closes over whatever it likes,
            // including a Scoped service the walk cannot see. And under trimming GetConstructors() returns
            // empty for a type whose constructors were removed, which is indistinguishable here from the first
            // case -- so the walk would hand back Root for a type it simply could not read.
            //
            // The dominant case is NOT an exotic type. Walk recurses on the PARAMETER type, and in an
            // interface-registered graph -- the ordinary way dependency injection is written -- that
            // parameter IS an interface, and an interface declares no instance constructors. So a plain
            // AddScoped<DbSession>() + AddTransient<IRepo, Repo>() + AddTransient<MyHandler>() reached this
            // branch and was classified root-safe. With ServiceProviderOptions.ValidateScopes left at its
            // default of false, that resolves silently and the Scoped service becomes process-lifetime.
            //
            // Root is the direction that resolves once from the root container and reuses the instance for
            // every dispatch, which is precisely the captive-dependency bug this walk exists to prevent. The
            // cost of being wrong the other way is a scope per dispatch for a type that did not need one.
            //
            // Two consequences of choosing Scope here, named because they are behaviour changes and not
            // merely cost: a transient IDisposable in this closure is now disposed with the per-dispatch
            // scope instead of living to shutdown, and under a request-scoped host the instance joins the
            // ambient request scope rather than the root. Both are the correct lifetimes; neither is a
            // no-op.
            return Requirement.Scope;
        }

        foreach (var parameter in ctor.GetParameters())
        {
            var parameterType = parameter.ParameterType;
            var lifetime = ResolveLifetime(parameterType);

            if (lifetime == ServiceLifetime.Scoped)
            {
                return Requirement.Scope; // Direct Scoped dependency — short-circuit.
            }

            if (lifetime == ServiceLifetime.Singleton)
            {
                // Prune. Again NOT because the container forbids a Singleton capturing a Scoped -- it
                // permits that silently whenever ValidateScopes is false, which is its default. The prune
                // is sound because a Singleton is one root-owned instance regardless of which provider
                // resolves it, so nothing downstream of this walk can alter its closure.
                continue;
            }

            if (lifetime == ServiceLifetime.Transient)
            {
                // Recurse through the transient intermediary — this is the depth-1 blind spot fixes.
                // visited.Add returns false on a cycle/diamond, terminating the walk for that branch.
                //
                // INVARIANT, load-bearing and easy to destroy: pruning on an already-visited type is sound
                // ONLY because Scope short-circuits. visited.Add(T) == false implies either T is the seed or
                // a previous Walk(T) returned Root -- never Scope, because a Scope verdict returns
                // immediately at every site below rather than being accumulated. Anyone who rewrites this
                // walk to collect verdicts and decide at the end silently breaks the prune: a pruned branch
                // could then have been the Scope one.
                if (visited.Add(parameterType) && Walk(parameterType, visited) == Requirement.Scope)
                {
                    return Requirement.Scope;
                }

                continue;
            }

            // Unknown/unresolved (factory registration, unregistered type, open-generic miss): bias to Scope
            // so a captive-dependency cannot slip through an unprovable branch.
            return Requirement.Scope;
        }

        return Requirement.Root;
    }

    /// <summary>
    /// Selects the constructor the DI container would actually activate: the one marked
    /// <see cref="ActivatorUtilitiesConstructorAttribute"/> if present; otherwise the longest constructor
    /// whose parameters are all resolvable from the registry; otherwise the longest constructor (its
    /// unresolved parameters are treated as Unknown and bias the verdict to Scope). Mirrors
    /// <see cref="ActivatorUtilities.CreateInstance(IServiceProvider, Type, object[])"/> selection.
    /// </summary>
    private ConstructorInfo? SelectActivatableConstructor(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type)
    {
        var constructors = type.GetConstructors();
        if (constructors.Length == 0)
        {
            return null;
        }

        ConstructorInfo? longest = null;
        var longestLength = -1;
        ConstructorInfo? longestResolvable = null;
        var longestResolvableLength = -1;

        foreach (var constructor in constructors)
        {
            if (constructor.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), inherit: false))
            {
                return constructor; // Explicit activation constructor wins outright.
            }

            var parameters = constructor.GetParameters();
            var length = parameters.Length;

            if (length > longestLength)
            {
                longest = constructor;
                longestLength = length;
            }

            if (length > longestResolvableLength && AllParametersResolvable(parameters))
            {
                longestResolvable = constructor;
                longestResolvableLength = length;
            }
        }

        return longestResolvable ?? longest;
    }

    private bool AllParametersResolvable(ParameterInfo[] parameters)
    {
        foreach (var parameter in parameters)
        {
            if (ResolveLifetime(parameter.ParameterType) is null)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the registered lifetime of a dependency type: a direct registry lookup, then an open-generic
    /// fallback on the generic type definition (e.g. <c>ILogger&lt;T&gt;</c>, <c>IOptions&lt;T&gt;</c>).
    /// Returns <see langword="null"/> when the type is not registered (Unknown → the caller biases to Scope).
    /// </summary>
    private ServiceLifetime? ResolveLifetime(Type type)
    {
        if (_lifetimes!.TryGetLifetime(type, out var lifetime))
        {
            return lifetime;
        }

        if (type.IsGenericType && _lifetimes.TryGetLifetime(type.GetGenericTypeDefinition(), out var genericLifetime))
        {
            return genericLifetime;
        }

        return null;
    }

    private static InvalidOperationException CreateNoScopeDiagnostic(Type handlerType) =>
        new($"Handler '{handlerType.FullName}' must be resolved from a dependency-injection scope, but no " +
            "scope is available. Microsoft DI registers IServiceScopeFactory by default; if it is absent, " +
            "register an IDispatchAmbientScopeAccessor (for example via the Excalibur.Dispatch.Hosting.AspNetCore " +
            "integration) so scoped handlers resolve from the active request scope.");
}
