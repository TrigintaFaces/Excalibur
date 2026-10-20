using Company.ExcaliburOutbox.Messages;
using Excalibur.Dispatch.Delivery;

namespace Company.ExcaliburOutbox.Handlers;

/// <summary>Demonstrates the local event consumer used by the in-memory transport scaffold.</summary>
public sealed class OrderPlacedHandler(ILogger<OrderPlacedHandler> logger) : IEventHandler<OrderPlacedEvent>
{
    public Task HandleAsync(OrderPlacedEvent message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Outbox event received for order {OrderId}: {Quantity}x {ProductId}, total {TotalAmount}",
            message.OrderId, message.Quantity, message.ProductId, message.TotalAmount);
        return Task.CompletedTask;
    }
}
