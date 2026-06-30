using Excalibur.Dispatch;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Canonical <see cref="IDispatchEvent"/> envelope carrying a compat <see cref="INotification"/> through
/// the canonical dispatch middleware pipeline, so canonical middleware wraps the
/// fan-out to the compat <see cref="INotificationHandler{TNotification}"/> handlers.
/// </summary>
/// <typeparam name="TNotification">The compat notification type.</typeparam>
internal sealed class CompatNotificationWrapper<TNotification>(TNotification notification) : IDispatchEvent
    where TNotification : notnull
{
    /// <summary>Gets the wrapped compat notification.</summary>
    public TNotification Notification { get; } = notification;
}
