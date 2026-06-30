using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Configures the MediatR compatibility surface registered by
/// <c>AddMediatRCompat</c>. Exposes the configuration entry points used by code written against the
/// MediatR API (assembly registration, handler lifetime, open-behavior ordering) so that incumbent
/// registration calls compile after a namespace swap.
/// </summary>
public sealed class MediatRCompatOptions
{
    private readonly List<Assembly> _assemblies = [];
    private readonly List<OpenBehaviorRegistration> _openBehaviors = [];
    private readonly List<ClosedBehaviorRegistration> _closedBehaviors = [];

    /// <summary>
    /// Gets or sets the service lifetime used when registering generated handler adapters.
    /// </summary>
    /// <value>The handler <see cref="ServiceLifetime"/>; defaults to <see cref="ServiceLifetime.Transient"/>.</value>
    public ServiceLifetime HandlerLifetime { get; set; } = ServiceLifetime.Transient;

    /// <summary>Gets the assemblies registered for handler discovery (compile-time, source-generated).</summary>
    internal IReadOnlyList<Assembly> Assemblies => _assemblies;

    /// <summary>Gets the open-generic pipeline behaviors registered, in registration order.</summary>
    internal IReadOnlyList<OpenBehaviorRegistration> OpenBehaviors => _openBehaviors;

    /// <summary>Gets the closed pipeline behaviors registered, in registration order.</summary>
    internal IReadOnlyList<ClosedBehaviorRegistration> ClosedBehaviors => _closedBehaviors;

    /// <summary>Registers handlers discovered in the specified assembly.</summary>
    /// <param name="assembly">The assembly to scan for handler implementations.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions RegisterServicesFromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        if (!_assemblies.Contains(assembly))
        {
            _assemblies.Add(assembly);
        }

        return this;
    }

    /// <summary>Registers handlers discovered in the specified assemblies.</summary>
    /// <param name="assemblies">The assemblies to scan for handler implementations.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions RegisterServicesFromAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);
        foreach (var assembly in assemblies)
        {
            RegisterServicesFromAssembly(assembly);
        }

        return this;
    }

    /// <summary>Registers handlers discovered in the assembly containing <typeparamref name="T"/>.</summary>
    /// <typeparam name="T">A type whose assembly is scanned for handler implementations.</typeparam>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions RegisterServicesFromAssemblyContaining<T>() =>
        RegisterServicesFromAssembly(typeof(T).Assembly);

    /// <summary>Registers handlers discovered in the assembly containing <paramref name="type"/>.</summary>
    /// <param name="type">A type whose assembly is scanned for handler implementations.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions RegisterServicesFromAssemblyContaining(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return RegisterServicesFromAssembly(type.Assembly);
    }

    /// <summary>
    /// Registers an open-generic pipeline behavior (e.g. <c>typeof(LoggingBehavior&lt;,&gt;)</c>). Behaviors
    /// run in registration order, nested around the handler.
    /// </summary>
    /// <param name="openBehaviorType">The open-generic <see cref="IPipelineBehavior{TRequest,TResponse}"/> type.</param>
    /// <param name="lifetime">The service lifetime for the behavior; defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions AddOpenBehavior(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type openBehaviorType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(openBehaviorType);
        _openBehaviors.Add(new OpenBehaviorRegistration(openBehaviorType, lifetime));
        return this;
    }

    /// <summary>
    /// Registers a closed pipeline behavior by service and implementation type (e.g.
    /// <c>AddBehavior(typeof(IPipelineBehavior&lt;Ping,Pong&gt;), typeof(PingPongBehavior))</c>). Behaviors
    /// run in registration order, nested around the handler.
    /// </summary>
    /// <param name="serviceType">The closed <see cref="IPipelineBehavior{TRequest,TResponse}"/> service type.</param>
    /// <param name="implementationType">The behavior implementation type.</param>
    /// <param name="lifetime">The service lifetime; defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions AddBehavior(Type serviceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type implementationType, ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        ArgumentNullException.ThrowIfNull(implementationType);
        _closedBehaviors.Add(new ClosedBehaviorRegistration(serviceType, implementationType, lifetime));
        return this;
    }

    /// <summary>
    /// Registers a closed pipeline behavior by service and implementation type (e.g.
    /// <c>AddBehavior&lt;IPipelineBehavior&lt;Ping,Pong&gt;, PingPongBehavior&gt;()</c>).
    /// </summary>
    /// <typeparam name="TServiceType">The closed <see cref="IPipelineBehavior{TRequest,TResponse}"/> service type.</typeparam>
    /// <typeparam name="TImplementationType">The behavior implementation type.</typeparam>
    /// <param name="lifetime">The service lifetime; defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions AddBehavior<TServiceType, [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] TImplementationType>(ServiceLifetime lifetime = ServiceLifetime.Transient) =>
        AddBehavior(typeof(TServiceType), typeof(TImplementationType), lifetime);

    /// <summary>
    /// Registers a closed pipeline behavior by implementation type; it is registered against each closed
    /// <see cref="IPipelineBehavior{TRequest,TResponse}"/> interface it implements.
    /// </summary>
    /// <typeparam name="TImplementationType">The behavior implementation type.</typeparam>
    /// <param name="lifetime">The service lifetime; defaults to <see cref="ServiceLifetime.Transient"/>.</param>
    /// <returns>This instance, for chaining.</returns>
    public MediatRCompatOptions AddBehavior<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] TImplementationType>(ServiceLifetime lifetime = ServiceLifetime.Transient)
    {
        _closedBehaviors.Add(new ClosedBehaviorRegistration(ServiceType: null, typeof(TImplementationType), lifetime));
        return this;
    }
}

/// <summary>Records an open-generic pipeline behavior registration and its lifetime.</summary>
/// <param name="BehaviorType">The open-generic behavior type.</param>
/// <param name="Lifetime">The service lifetime.</param>
internal sealed record OpenBehaviorRegistration([property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)][field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type BehaviorType, ServiceLifetime Lifetime);

/// <summary>Records a closed pipeline behavior registration and its lifetime.</summary>
/// <param name="ServiceType">The closed behavior service type, or <see langword="null"/> to register against all implemented behavior interfaces.</param>
/// <param name="BehaviorImplementationType">The behavior implementation type.</param>
/// <param name="Lifetime">The service lifetime.</param>
internal sealed record ClosedBehaviorRegistration(Type? ServiceType, [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)][field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.Interfaces)] Type BehaviorImplementationType, ServiceLifetime Lifetime);
