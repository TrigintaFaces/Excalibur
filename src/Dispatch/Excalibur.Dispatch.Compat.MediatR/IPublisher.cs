namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Publishes notifications to zero or more handlers. Provides the <c>IPublisher</c> shape used by
/// MediatR-based code; maps to the canonical <c>IDispatcher</c>.
/// </summary>
public interface IPublisher
{
    /// <summary>Publishes a notification whose type is not known at compile time.</summary>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when all handlers finish.</returns>
    Task Publish(object notification, CancellationToken cancellationToken = default);

    /// <summary>
 /// Publishes a strongly-typed notification to its handlers. Provides the generic
    /// <c>Publish&lt;TNotification&gt;</c> overload used by MediatR-based code.
    /// </summary>
    /// <typeparam name="TNotification">The notification type.</typeparam>
    /// <param name="notification">The notification instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task that completes when all handlers finish.</returns>
    Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification;
}
