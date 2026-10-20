using Company.ExcaliburCqrs.Domain.Aggregates;
using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating a new Order aggregate,
/// persisting it to the event store, and returning its identifier after inline projection processing.
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

        // The repository notifies framework-managed inline projections after the event commit.
        await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Order {OrderId} persisted and inline projection processing completed", orderId);
        return orderId;
    }
}
