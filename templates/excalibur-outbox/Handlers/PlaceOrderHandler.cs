using Company.ExcaliburOutbox.Messages;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Exceptions;

namespace Company.ExcaliburOutbox.Handlers;

/// <summary>
/// Handles the <see cref="PlaceOrderCommand"/> by creating an order
/// and publishing an <see cref="OrderPlacedEvent"/> through the outbox
/// for reliable at-least-once delivery.
/// </summary>
public sealed class PlaceOrderHandler : IActionHandler<PlaceOrderCommand, Guid>
{
    private readonly ILogger<PlaceOrderHandler> _logger;
    private readonly IOutboxWriter _outbox;

    public PlaceOrderHandler(IOutboxWriter outbox, ILogger<PlaceOrderHandler> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(PlaceOrderCommand action, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(action.ProductId))
        {
            errors[nameof(action.ProductId)] = ["ProductId is required."];
        }
        if (action.Quantity <= 0)
        {
            errors[nameof(action.Quantity)] = ["Quantity must be positive."];
        }
        if (action.UnitPrice < 0)
        {
            errors[nameof(action.UnitPrice)] = ["UnitPrice must be non-negative."];
        }
        if (errors.Count > 0)
        {
            throw new ValidationException(errors);
        }
        decimal totalAmount;
        try
        {
            totalAmount = action.Quantity * action.UnitPrice;
        }
        catch (OverflowException)
        {
            throw new ValidationException(new Dictionary<string, string[]>
            {
                [nameof(action.UnitPrice)] = ["The order total exceeds the supported amount."],
            });
        }
        var orderId = Guid.NewGuid();

        _logger.LogInformation(
            "Placing order {OrderId}: {Quantity}x {ProductId} = {Total:C}",
            orderId, action.Quantity, action.ProductId, totalAmount);

        // The middleware stages this buffered event after the handler succeeds.
        // This demonstration has no business database transaction; it shows eventual delivery.
        await _outbox.WriteAsync(
            new OrderPlacedEvent(orderId, action.ProductId, action.Quantity, totalAmount, DateTimeOffset.UtcNow),
            null, cancellationToken);

        _logger.LogInformation("Order {OrderId} event buffered for outbox staging", orderId);
        return orderId;
    }
}
