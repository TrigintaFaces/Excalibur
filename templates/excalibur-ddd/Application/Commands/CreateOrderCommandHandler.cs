using Company.ExcaliburDdd.Domain.Aggregates;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburDdd.Application.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating a new Order aggregate
/// and persisting it to the event store.
/// </summary>
public sealed class CreateOrderCommandHandler : IMessageHandler<CreateOrderCommand>
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
    public async Task HandleAsync(CreateOrderCommand message, IMessageContext context, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, message.ProductId, message.Quantity);

        _logger.LogInformation("Persisting order {OrderId} with {ItemCount} items", orderId, order.Items.Count);

        await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Order {OrderId} persisted at version {Version}", orderId, order.Version);

        context.SetResult(orderId);
    }
}
