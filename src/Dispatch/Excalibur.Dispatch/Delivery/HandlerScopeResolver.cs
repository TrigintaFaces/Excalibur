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
/// constructor dependencies are scoped — dispatched through the ultra-local fast paths.
/// </summary>
/// <remarks>
/// <para>
/// The verdict for each handler type is computed once and cached, purely from registration metadata (no
/// construction, no exceptions): a handler registered <see cref="ServiceLifetime.Scoped"/> needs a scope;
/// a <see cref="ServiceLifetime.Singleton"/> is always root-safe; any handler with a constructor dependency
/// registered <see cref="ServiceLifetime.Scoped"/> needs a scope. No reflection or allocation occurs on the
/// warm dispatch path — only on the first dispatch of each handler type.
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
    /// Runs <paramref name="invoke"/> using a service provider that can satisfy a scoped handler: the
    /// ambient scope when available (borrowed — not disposed here), otherwise a freshly created scope that
    /// is disposed after the invocation completes.
    /// </summary>
    public async ValueTask<T> RunAsync<T>(Type handlerType, Func<IServiceProvider, ValueTask<T>> invoke)
    {
        ArgumentNullException.ThrowIfNull(invoke);

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

    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2070:'this' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method.",
        Justification = "Handler types are preserved via DI registration; constructor metadata is inspected only to decide scope requirement. AOT consumers use the source-generated dispatcher.")]
    private Requirement Compute([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        // 1. A handler registered Scoped is resolved (when self-registered) honoring that lifetime; from
        //    the root container that throws the captive-dependency error. Singletons are always root-safe.
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

        // 2. A transient or factory-activated handler still needs a scope when any of its constructor
        //    dependencies is registered Scoped (resolving that dependency from root throws captive-dependency).
        return HasScopedConstructorDependency(handlerType) ? Requirement.Scope : Requirement.Root;
    }

    private bool HasScopedConstructorDependency(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerType)
    {
        ConstructorInfo? selected = null;
        var selectedLength = -1;
        foreach (var ctor in handlerType.GetConstructors())
        {
            var length = ctor.GetParameters().Length;
            if (length > selectedLength)
            {
                selected = ctor;
                selectedLength = length;
            }
        }

        if (selected is null)
        {
            return false;
        }

        foreach (var parameter in selected.GetParameters())
        {
            if (_lifetimes!.TryGetLifetime(parameter.ParameterType, out var lifetime) && lifetime == ServiceLifetime.Scoped)
            {
                return true;
            }
        }

        return false;
    }

    private static InvalidOperationException CreateNoScopeDiagnostic(Type handlerType) =>
        new($"Handler '{handlerType.FullName}' must be resolved from a dependency-injection scope, but no " +
            "scope is available. Microsoft DI registers IServiceScopeFactory by default; if it is absent, " +
            "register an IDispatchAmbientScopeAccessor (for example via the Excalibur.Dispatch.Hosting.AspNetCore " +
            "integration) so scoped handlers resolve from the active request scope.");
}
