using System.Reflection;

using Excalibur.Dispatch.Compat.MediatR;
using Excalibur.Dispatch.Compat.MediatR.Routing;

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Registration extensions for the MediatR compatibility surface. The entry point
/// <see cref="AddMediatRCompat"/> mirrors the shape of the incumbent <c>AddMediatR</c> registration call
/// so it compiles after a namespace swap, while wiring the source-compatible adapters onto
/// Excalibur.Dispatch.
/// </summary>
public static class MediatRCompatServiceCollectionExtensions
{
    /// <summary>
    /// Registers the MediatR compatibility surface (<see cref="Excalibur.Dispatch.Compat.MediatR.IMediator"/>,
    /// <see cref="Excalibur.Dispatch.Compat.MediatR.ISender"/>,
    /// <see cref="Excalibur.Dispatch.Compat.MediatR.IPublisher"/>) and the handler adapters, validating
    /// <see cref="Excalibur.Dispatch.Compat.MediatR.MediatRCompatOptions"/> at startup.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Configures assembly registration, handler lifetime, and behavior order.</param>
    /// <returns>The same <see cref="IServiceCollection"/>, for chaining.</returns>
    public static IServiceCollection AddMediatRCompat(
        this IServiceCollection services,
        Action<MediatRCompatOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Swap-only self-containment (nd76b5): MediatR's AddMediatR is self-contained, so AddMediatRCompat
        // must bootstrap the Dispatch core the facade routes through. AddDispatch is TryAdd-idempotent, so
        // a consumer that also calls AddDispatch() explicitly is unaffected.
        services.AddDispatch();

        services.AddOptions<MediatRCompatOptions>()
            .Configure(configure)
            .ValidateOnStart();

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IValidateOptions<MediatRCompatOptions>, MediatRCompatOptionsValidator>());

        // Register the generated handlers/behaviors into DI and build the per-container request→bridge
        // registry from the source-generated registrations (populated by module initializers at load),
        // filtered to the consumer's registered assemblies.
        var probe = new MediatRCompatOptions();
        configure(probe);
        services.TryAddSingleton(RegisterGeneratedComponents(services, probe));

        // Facade: ISender/IPublisher resolve the single IMediator implementation.
        services.TryAddTransient<Excalibur.Dispatch.Compat.MediatR.IMediator, Mediator>();
        services.TryAddTransient<Excalibur.Dispatch.Compat.MediatR.ISender>(
            static sp => sp.GetRequiredService<Excalibur.Dispatch.Compat.MediatR.IMediator>());
        services.TryAddTransient<Excalibur.Dispatch.Compat.MediatR.IPublisher>(
            static sp => sp.GetRequiredService<Excalibur.Dispatch.Compat.MediatR.IMediator>());

        return services;
    }

    private static CompatBridgeRegistry RegisterGeneratedComponents(IServiceCollection services, MediatRCompatOptions options)
    {
        var registeredAssemblies = new HashSet<Assembly>(options.Assemblies);
        var lifetime = options.HandlerLifetime;
        var registry = new CompatBridgeRegistry();

        // Requests: exactly one handler per request type within the registered scope → dup-fail-fast.
        var seenRequestTypes = new HashSet<Type>();
        foreach (var entry in CompatGeneratedRegistrations.Requests)
        {
            if (!registeredAssemblies.Contains(entry.HandlerAssembly))
            {
                continue;
            }

            if (!seenRequestTypes.Add(entry.RequestType))
            {
                throw DuplicateRequestHandlerException.ForRequest(entry.RequestType);
            }

            services.Add(ServiceDescriptor.Describe(entry.HandlerServiceType, entry.HandlerImplementationType, lifetime));
            registry.AddRequestBridge(entry.RequestType, entry.BridgeFactory());
        }

        // Notifications: many handlers per type are valid → register each, one bridge per type.
        foreach (var entry in CompatGeneratedRegistrations.Notifications)
        {
            if (!registeredAssemblies.Contains(entry.HandlerAssembly))
            {
                continue;
            }

            services.TryAddEnumerable(ServiceDescriptor.Describe(entry.HandlerServiceType, entry.HandlerImplementationType, lifetime));
            registry.AddNotificationBridge(entry.NotificationType, entry.BridgeFactory());
        }

        // Streams: one handler + bridge per stream-request type.
        foreach (var entry in CompatGeneratedRegistrations.Streams)
        {
            if (!registeredAssemblies.Contains(entry.HandlerAssembly))
            {
                continue;
            }

            services.Add(ServiceDescriptor.Describe(entry.HandlerServiceType, entry.HandlerImplementationType, lifetime));
            registry.AddStreamBridge(entry.RequestType, entry.BridgeFactory());
        }

        RegisterBehaviors(services, options);
        return registry;
    }

    private static void RegisterBehaviors(IServiceCollection services, MediatRCompatOptions options)
    {
        // Open-generic behaviors: registered against the open IPipelineBehavior<,>, in registration order.
        foreach (var open in options.OpenBehaviors)
        {
            services.Add(ServiceDescriptor.Describe(typeof(IPipelineBehavior<,>), open.BehaviorType, open.Lifetime));
        }

        // Closed behaviors: an explicit service type, or every closed IPipelineBehavior<,> the impl declares.
        foreach (var closed in options.ClosedBehaviors)
        {
            if (closed.ServiceType is not null)
            {
                services.Add(ServiceDescriptor.Describe(closed.ServiceType, closed.BehaviorImplementationType, closed.Lifetime));
                continue;
            }

            foreach (var iface in closed.BehaviorImplementationType.GetInterfaces())
            {
                if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>))
                {
                    services.Add(ServiceDescriptor.Describe(iface, closed.BehaviorImplementationType, closed.Lifetime));
                }
            }
        }
    }
}
