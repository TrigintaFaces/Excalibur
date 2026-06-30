namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Maps a request's runtime <see cref="Type"/> to its closed <see cref="ICompatRequestBridge"/>. Populated
/// by the source-generated <c>AddMediatRCompatGenerated</c> registration (one entry per discovered request
/// type), and read by the <see cref="IMediator"/> facade to route a <c>Send</c> without reflection.
/// </summary>
internal sealed class CompatBridgeRegistry
{
    private readonly Dictionary<Type, ICompatRequestBridge> _requestBridges = [];
    private readonly Dictionary<Type, ICompatNotificationBridge> _notificationBridges = [];
    private readonly Dictionary<Type, ICompatStreamBridge> _streamBridges = [];

    /// <summary>Registers the bridge for a request type (last registration wins; dup-fail-fast is enforced upstream).</summary>
    /// <param name="requestType">The compat request type.</param>
    /// <param name="bridge">The closed bridge that routes it.</param>
    public void AddRequestBridge(Type requestType, ICompatRequestBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(bridge);
        _requestBridges[requestType] = bridge;
    }

    /// <summary>Gets the bridge for a request type, or <see langword="null"/> if none is registered.</summary>
    /// <param name="requestType">The compat request type.</param>
    /// <returns>The bridge, or <see langword="null"/>.</returns>
    public ICompatRequestBridge? GetRequestBridge(Type requestType) =>
        _requestBridges.TryGetValue(requestType, out var bridge) ? bridge : null;

    /// <summary>Registers the fan-out bridge for a notification type.</summary>
    /// <param name="notificationType">The compat notification type.</param>
    /// <param name="bridge">The closed bridge that fans it out.</param>
    public void AddNotificationBridge(Type notificationType, ICompatNotificationBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(notificationType);
        ArgumentNullException.ThrowIfNull(bridge);
        _notificationBridges[notificationType] = bridge;
    }

    /// <summary>Gets the fan-out bridge for a notification type, or <see langword="null"/> if none is registered.</summary>
    /// <param name="notificationType">The compat notification type.</param>
    /// <returns>The bridge, or <see langword="null"/> (no handlers → publish is a no-op).</returns>
    public ICompatNotificationBridge? GetNotificationBridge(Type notificationType) =>
        _notificationBridges.TryGetValue(notificationType, out var bridge) ? bridge : null;

    /// <summary>Registers the stream bridge for a stream-request type.</summary>
    /// <param name="requestType">The compat stream-request type.</param>
    /// <param name="bridge">The closed bridge that streams it.</param>
    public void AddStreamBridge(Type requestType, ICompatStreamBridge bridge)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        ArgumentNullException.ThrowIfNull(bridge);
        _streamBridges[requestType] = bridge;
    }

    /// <summary>Gets the stream bridge for a stream-request type, or <see langword="null"/> if none is registered.</summary>
    /// <param name="requestType">The compat stream-request type.</param>
    /// <returns>The bridge, or <see langword="null"/>.</returns>
    public ICompatStreamBridge? GetStreamBridge(Type requestType) =>
        _streamBridges.TryGetValue(requestType, out var bridge) ? bridge : null;
}
