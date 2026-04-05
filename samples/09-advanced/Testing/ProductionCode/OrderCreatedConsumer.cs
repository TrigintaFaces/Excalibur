using Excalibur.Dispatch.Abstractions.Delivery;

namespace Testing.ProductionCode;

/// <summary>
/// Event handler that reacts to <see cref="OrderCreatedEvent"/>.
/// In production this might update a read model or send a notification.
/// </summary>
public sealed class OrderCreatedConsumer : IEventHandler<OrderCreatedEvent>
{
    /// <summary>
    /// Tracks handled events so tests can verify invocation.
    /// </summary>
    public List<OrderCreatedEvent> HandledEvents { get; } = [];

    public Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(eventMessage);
        HandledEvents.Add(eventMessage);
        return Task.CompletedTask;
    }
}
