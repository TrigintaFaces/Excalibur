namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Handles a notification of type <typeparamref name="TNotification"/>. Provides the
/// <c>INotificationHandler&lt;TNotification&gt;</c> shape used by MediatR-based code; maps to the
/// canonical <c>IEventHandler</c>.
/// </summary>
/// <typeparam name="TNotification">The notification type handled.</typeparam>
public interface INotificationHandler<in TNotification>
    where TNotification : INotification
{
    /// <summary>Handles the notification.</summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when handling finishes.</returns>
    Task Handle(TNotification notification, CancellationToken cancellationToken);
}
