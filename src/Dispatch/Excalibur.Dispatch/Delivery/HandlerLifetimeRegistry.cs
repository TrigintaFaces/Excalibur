// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;

using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Delivery;

/// <summary>
/// Captures the registered <see cref="ServiceLifetime"/> of each service type at configuration time so
/// the singleton <see cref="LocalMessageBus"/> can decide, deterministically and without per-dispatch
/// reflection or exceptions, whether a handler must be resolved from a dependency-injection scope rather
/// than the root container (eliminating the captive-dependency failure).
/// </summary>
/// <remarks>
/// The map is built lazily from the <see cref="IServiceCollection"/> snapshot on first query — after all
/// registrations (including <c>AddHandlersFromAssembly</c> / manual handler registrations that may run
/// after <c>AddDispatchPipeline</c>) are complete and the provider has been built.
/// </remarks>
internal sealed class HandlerLifetimeRegistry
{
    private readonly Lazy<FrozenDictionary<Type, ServiceLifetime>> _map;

    /// <summary>
    /// Initializes a new instance of the <see cref="HandlerLifetimeRegistry"/> class.
    /// </summary>
    /// <param name="services">The service collection whose descriptors describe handler lifetimes.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="services"/> is <see langword="null"/>.</exception>
    public HandlerLifetimeRegistry(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _map = new Lazy<FrozenDictionary<Type, ServiceLifetime>>(
            () => Build(services),
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    /// <summary>
    /// Attempts to get the registered lifetime for a service type.
    /// </summary>
    /// <param name="serviceType">The service (handler) type to look up.</param>
    /// <param name="lifetime">When this method returns, the registered lifetime if found.</param>
    /// <returns><see langword="true"/> if a registration was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetLifetime(Type serviceType, out ServiceLifetime lifetime)
        => _map.Value.TryGetValue(serviceType, out lifetime);

    private static FrozenDictionary<Type, ServiceLifetime> Build(IServiceCollection services)
    {
        // Model what the container ACTUALLY does, because that is the only thing the scope verdict may
        // rest on. Microsoft DI resolves a service type using the LAST registered descriptor for it, so
        // a later AddTransient<Handler>() supersedes an earlier AddScoped<Handler>() and the earlier
        // descriptor becomes unreachable. Recording the earlier Scoped instead would make the registry
        // disagree with the provider it describes: the resolver would create a scope to contain a captive
        // dependency the container can never produce, and the consumer's explicit registration would be
        // silently ignored.
        //
        // Service-type registrations are therefore authoritative and last-wins. Implementation types are
        // kept only as a FALLBACK for types that are never registered as a service themselves (the
        // AddScoped<IActionHandler<T>, Handler>() shape, where GetService(Handler) returns null and the
        // activator constructs the handler directly). A fallback must never override a real service
        // registration, or last-wins breaks for any handler registered both ways.
        var serviceTypes = new Dictionary<Type, ServiceLifetime>();
        var implementationOnly = new Dictionary<Type, ServiceLifetime>();

        foreach (var descriptor in services)
        {
            // A KEYED descriptor is resolvable only through its key: GetService(IFoo) never returns it.
            // Recording it under the bare service type would make this registry disagree with the
            // container it models — and because the map is last-wins, a later keyed Singleton would MASK
            // an earlier non-keyed Scoped registration, so HandlerScopeResolver.Walk would prune that
            // dependency as provably root-safe and hand back Root for a handler that does capture a
            // scoped service. The keyed descriptor still feeds the implementation-type fallback below,
            // where it describes a type that has no bare service registration of its own.
            if (!descriptor.IsKeyedService)
            {
                serviceTypes[descriptor.ServiceType] = descriptor.Lifetime;
            }

            // keyed-safe accessor handles the keyed/non-keyed distinction.
            var implementationType = descriptor.GetImplementationType();
            if (implementationType is not null && implementationType != descriptor.ServiceType)
            {
                implementationOnly[implementationType] = descriptor.Lifetime;
            }
        }

        foreach (var (implementationType, lifetime) in implementationOnly)
        {
            // Only where the type is not resolvable as a service in its own right.
            _ = serviceTypes.TryAdd(implementationType, lifetime);
        }

        return serviceTypes.ToFrozenDictionary();
    }
}
