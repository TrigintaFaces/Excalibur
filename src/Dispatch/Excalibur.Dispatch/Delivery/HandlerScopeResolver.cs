// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

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
    /// <param name="invoke">The handler invocation, given the resolved scope's service provider.</param>
    public async ValueTask<T> RunAsync<T>(
        Type handlerType,
        IServiceProvider? preferredScope,
        Func<IServiceProvider, ValueTask<T>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);

        // 1. An explicit caller-supplied scope (the dispatch context's request scope) — borrowed, never
        //    disposed here. Honors the request scope a context-bound dispatch already carries so the
        //    handler's IMessageContext.RequestServices is the same instance the caller is using.
        if (preferredScope is not null)
        {
            return await invoke(preferredScope).ConfigureAwait(false);
        }

        var ambient = _ambientScope?.CurrentServiceProvider;
        if (ambient is not null)
        {
            return await invoke(ambient).ConfigureAwait(false);
        }

        if (_scopeFactory is not null)
        {
            var scope = _scopeFactory.CreateAsyncScope();
            try
            {
                return await invoke(scope.ServiceProvider).ConfigureAwait(false);
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
        //    the root container that throws the captive-dependency error. Singletons are always root-safe
        //    (a valid Singleton cannot transitively capture a Scoped — the container forbids that).
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
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method.",
        Justification = "Handler and dependency types are preserved via DI registration; constructor metadata is inspected only to decide scope requirement. AOT consumers use the source-generated dispatcher.")]
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2072:'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method. The return value of the source method does not have matching annotations.",
        Justification = "A constructor parameter type is resolved from DI; its constructors are preserved by registration. The scope verdict is advisory and AOT consumers use the source-generated dispatcher.")]
    private Requirement Walk(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type type,
        HashSet<Type> visited)
    {
        var ctor = SelectActivatableConstructor(type);
        if (ctor is null)
        {
            return Requirement.Root; // EC-A1: no public constructor — nothing scoped to capture.
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
                continue; // Provably root-safe subtree — prune (cannot transitively reach Scoped).
            }

            if (lifetime == ServiceLifetime.Transient)
            {
                // Recurse through the transient intermediary — this is the depth-1 blind spot pedo87 fixes.
                // visited.Add returns false on a cycle/diamond, terminating the walk for that branch.
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
    /// Selects the constructor the DI container would actually activate (FR-A3): the one marked
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
