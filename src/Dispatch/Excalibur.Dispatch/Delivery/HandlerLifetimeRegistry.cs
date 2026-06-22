// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;

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
        // Index BOTH the service type and the implementation type, because the scope verdict is queried
        // by the concrete handler type (entry.HandlerType = ImplementationType) while handlers are often
        // registered only by interface (e.g. AddScoped<IActionHandler<T>, Handler>()). Scoped is
        // "stickiest": if any registration that can resolve a type is Scoped, the type needs a scope —
        // so Scoped never gets overwritten by a Transient/Singleton self-registration of the same type.
        var map = new Dictionary<Type, ServiceLifetime>();

        foreach (var descriptor in services)
        {
            Record(map, descriptor.ServiceType, descriptor.Lifetime);

            var implementationType = descriptor.ImplementationType;
            if (implementationType is not null && implementationType != descriptor.ServiceType)
            {
                Record(map, implementationType, descriptor.Lifetime);
            }
        }

        return map.ToFrozenDictionary();
    }

    private static void Record(Dictionary<Type, ServiceLifetime> map, Type type, ServiceLifetime lifetime)
    {
        if (map.TryGetValue(type, out var existing))
        {
            // Never downgrade away from Scoped (the lifetime that requires a DI scope).
            if (existing == ServiceLifetime.Scoped)
            {
                return;
            }

            map[type] = lifetime == ServiceLifetime.Scoped ? ServiceLifetime.Scoped : lifetime;
            return;
        }

        map[type] = lifetime;
    }
}
