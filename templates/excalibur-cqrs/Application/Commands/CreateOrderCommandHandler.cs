using Company.ExcaliburCqrs.Domain.Aggregates;
using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating a new Order aggregate,
/// persisting it to the event store, and returning its identifier.
/// </summary>
public sealed class CreateOrderCommandHandler : IActionHandler<CreateOrderCommand, Guid>
{
    private readonly IEventSourcedRepository<Order, Guid> _orderRepository;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IEventSourcedRepository<Order, Guid> orderRepository,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Guid> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, action.ProductId, action.Quantity);

        _logger.LogInformation("Persisting order {OrderId} with {ItemCount} items", orderId, order.Items.Count);

        // The projection host applies the events; nothing has been projected when this returns.
        // SaveAsync returns a session token (SaveResult.SessionToken) -- hand it to
        // IProjectionSessionReader.ReadAsync when a read must observe this write.
        var saved = await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Order {OrderId} persisted at version {Version}; the read model follows asynchronously",
            orderId,
            saved.CommittedVersion);
        return orderId;
    }
}
