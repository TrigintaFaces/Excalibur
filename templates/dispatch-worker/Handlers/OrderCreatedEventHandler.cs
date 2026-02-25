using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchWorker.Handlers;

/// <summary>
/// Handles order created events from the message bus.
/// </summary>
public sealed class OrderCreatedEventHandler : IMessageHandler<OrderCreatedEvent>
{
    private readonly ILogger<OrderCreatedEventHandler> _logger;

    public OrderCreatedEventHandler(ILogger<OrderCreatedEventHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderCreatedEvent message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing order created event for order {OrderId}, product {ProductId}, quantity {Quantity}",
            message.OrderId, message.ProductId, message.Quantity);

        // Simulate async processing (e.g., sending confirmation email, updating inventory)
        await Task.Delay(100, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} processing completed successfully", message.OrderId);
    }
}

/// <summary>
/// Represents an order created event.
/// </summary>
public sealed record OrderCreatedEvent(Guid OrderId, string ProductId, int Quantity);
