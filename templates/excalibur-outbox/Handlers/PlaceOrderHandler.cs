using Company.ExcaliburOutbox.Messages;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Company.ExcaliburOutbox.Handlers;

/// <summary>
/// Handles the <see cref="PlaceOrderCommand"/> by creating an order
/// and publishing an <see cref="OrderPlacedEvent"/> through the outbox
/// for reliable at-least-once delivery.
/// </summary>
public sealed class PlaceOrderHandler : IActionHandler<PlaceOrderCommand>
{
    private readonly ILogger<PlaceOrderHandler> _logger;

    public PlaceOrderHandler(ILogger<PlaceOrderHandler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(PlaceOrderCommand action, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var totalAmount = action.Quantity * action.UnitPrice;

        _logger.LogInformation(
            "Placing order {OrderId}: {Quantity}x {ProductId} = {Total:C}",
            orderId, action.Quantity, action.ProductId, totalAmount);

        // In a real application, you would:
        // 1. Save the order to your database
        // 2. Add the OrderPlacedEvent to the outbox in the same transaction
        // The outbox background processor will then reliably publish the event
        // to the configured transport, guaranteeing at-least-once delivery.

        _logger.LogInformation("Order {OrderId} placed and event queued in outbox", orderId);

        return Task.CompletedTask;
    }
}
