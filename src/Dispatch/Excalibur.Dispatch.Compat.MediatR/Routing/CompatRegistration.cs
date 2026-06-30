using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Public registration entry points called by the source-generated <c>[ModuleInitializer]</c>.
/// The generated code lives in the consumer's assembly, so these methods are public; the bridge
/// implementations they construct remain internal. Not intended for direct consumer use.
/// </summary>
public static class CompatRegistration
{
    /// <summary>Registers the generated request handler + bridge for <typeparamref name="TRequest"/> (called by generated code).</summary>
    /// <typeparam name="TRequest">The compat request type.</typeparam>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="handlerImplementationType">The concrete handler implementation type.</param>
    public static void RegisterRequest<TRequest, TResponse>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerImplementationType)
        where TRequest : notnull, IRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handlerImplementationType);
        CompatGeneratedRegistrations.AddRequest(new CompatGeneratedRegistrations.RequestEntry(
            typeof(TRequest),
            handlerImplementationType.Assembly,
            static () => new CompatActionBridge<TRequest, TResponse>(),
            typeof(IRequestHandler<TRequest, TResponse>),
            handlerImplementationType));
    }

    /// <summary>Registers the generated notification handler + bridge for <typeparamref name="TNotification"/> (called by generated code).</summary>
    /// <typeparam name="TNotification">The compat notification type.</typeparam>
    /// <param name="handlerImplementationType">The concrete handler implementation type.</param>
    public static void RegisterNotification<TNotification>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerImplementationType)
        where TNotification : notnull, INotification
    {
        ArgumentNullException.ThrowIfNull(handlerImplementationType);
        CompatGeneratedRegistrations.AddNotification(new CompatGeneratedRegistrations.NotificationEntry(
            typeof(TNotification),
            handlerImplementationType.Assembly,
            static () => new CompatNotificationBridge<TNotification>(),
            typeof(INotificationHandler<TNotification>),
            handlerImplementationType));
    }

    /// <summary>Registers the generated stream handler + bridge for <typeparamref name="TRequest"/> (called by generated code).</summary>
    /// <typeparam name="TRequest">The compat stream-request type.</typeparam>
    /// <typeparam name="TResponse">The stream element type.</typeparam>
    /// <param name="handlerImplementationType">The concrete handler implementation type.</param>
    public static void RegisterStream<TRequest, TResponse>(
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type handlerImplementationType)
        where TRequest : IStreamRequest<TResponse>
    {
        ArgumentNullException.ThrowIfNull(handlerImplementationType);
        CompatGeneratedRegistrations.AddStream(new CompatGeneratedRegistrations.StreamEntry(
            typeof(TRequest),
            handlerImplementationType.Assembly,
            static () => new CompatStreamBridge<TRequest, TResponse>(),
            typeof(IStreamRequestHandler<TRequest, TResponse>),
            handlerImplementationType));
    }
}

/// <summary>
/// Process-wide collector of generated bridge registrations (populated by module initializers at assembly
/// load). <c>AddMediatRCompat</c> reads it and filters by the consumer's registered assemblies to build a
/// per-container <see cref="CompatBridgeRegistry"/> — so scoping and isolation are per-container, not global.
/// </summary>
internal static class CompatGeneratedRegistrations
{
    private static readonly ConcurrentBag<RequestEntry> RequestEntries = [];
    private static readonly ConcurrentBag<NotificationEntry> NotificationEntries = [];
    private static readonly ConcurrentBag<StreamEntry> StreamEntries = [];

    // C2: descriptor registration is idempotent — guard duplicate adds (keyed by message+handler type) so a
    // descriptor is held at most once even if a module initializer is somehow invoked more than once. The
    // static hub holds ONLY these idempotent descriptors; dup-detection/filtering is per-container (C1).
    private static readonly ConcurrentDictionary<string, byte> Seen = new(StringComparer.Ordinal);

    internal static void AddRequest(RequestEntry entry)
    {
        if (Seen.TryAdd($"R|{entry.RequestType.FullName}|{entry.HandlerImplementationType.FullName}", 0))
        {
            RequestEntries.Add(entry);
        }
    }

    internal static void AddNotification(NotificationEntry entry)
    {
        if (Seen.TryAdd($"N|{entry.NotificationType.FullName}|{entry.HandlerImplementationType.FullName}", 0))
        {
            NotificationEntries.Add(entry);
        }
    }

    internal static void AddStream(StreamEntry entry)
    {
        if (Seen.TryAdd($"S|{entry.RequestType.FullName}|{entry.HandlerImplementationType.FullName}", 0))
        {
            StreamEntries.Add(entry);
        }
    }

    internal static IReadOnlyCollection<RequestEntry> Requests => RequestEntries;

    internal static IReadOnlyCollection<NotificationEntry> Notifications => NotificationEntries;

    internal static IReadOnlyCollection<StreamEntry> Streams => StreamEntries;

    internal readonly record struct RequestEntry(
        Type RequestType,
        Assembly HandlerAssembly,
        Func<ICompatRequestBridge> BridgeFactory,
        Type HandlerServiceType,
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        [field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerImplementationType);

    internal readonly record struct NotificationEntry(
        Type NotificationType,
        Assembly HandlerAssembly,
        Func<ICompatNotificationBridge> BridgeFactory,
        Type HandlerServiceType,
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        [field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerImplementationType);

    internal readonly record struct StreamEntry(
        Type RequestType,
        Assembly HandlerAssembly,
        Func<ICompatStreamBridge> BridgeFactory,
        Type HandlerServiceType,
        [property: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
        [field: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] Type HandlerImplementationType);
}
