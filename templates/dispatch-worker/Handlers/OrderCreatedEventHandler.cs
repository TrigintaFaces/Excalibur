using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Company.DispatchWorker.Handlers;

/// <summary>
/// Handles order created events from the message bus.
/// </summary>
public sealed class OrderCreatedEventHandler : IEventHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderCreatedEvent eventMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order created event for order {OrderId}, product {ProductId}, quantity {Quantity}",
            eventMessage.OrderId, eventMessage.ProductId, eventMessage.Quantity);

        // Simulate async processing (e.g., sending confirmation email, updating inventory)
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} processing completed successfully", eventMessage.OrderId);
    }
}

/// <summary>
/// Represents an order created event.
/// </summary>
public sealed record OrderCreatedEvent(Guid OrderId, string ProductId, int Quantity) : IDispatchEvent;
