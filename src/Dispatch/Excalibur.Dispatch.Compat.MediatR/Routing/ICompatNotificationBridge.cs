namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// A closed-typed bridge that fans a compat notification out to all registered
/// <see cref="INotificationHandler{TNotification}"/> through the canonical dispatch pipeline. The source
/// generator emits one per discovered notification type and maps it by runtime <see cref="Type"/> in the
/// <see cref="CompatBridgeRegistry"/>, so the facade fans out without reflection.
/// </summary>
internal interface ICompatNotificationBridge
{
    /// <summary>Publishes the notification to all registered handlers.</summary>
    /// <param name="notification">The compat notification instance.</param>
    /// <param name="provider">The service provider used to resolve handlers and the canonical pipeline.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when all handlers have run.</returns>
    Task PublishAsync(object notification, IServiceProvider provider, CancellationToken cancellationToken);
}
