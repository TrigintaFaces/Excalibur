using Company.ExcaliburCqrs.Domain.Aggregates;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>
/// Handles <see cref="CreateOrderCommand"/> by creating a new Order aggregate,
/// persisting it to the event store, and dispatching domain events for projections.
/// </summary>
public sealed class CreateOrderCommandHandler : IMessageHandler<CreateOrderCommand>
{
    private readonly IEventSourcedRepository<Order, Guid> _orderRepository;
    private readonly IDispatcher _dispatcher;
    private readonly ILogger<CreateOrderCommandHandler> _logger;

    public CreateOrderCommandHandler(
        IEventSourcedRepository<Order, Guid> orderRepository,
        IDispatcher dispatcher,
        ILogger<CreateOrderCommandHandler> logger)
    {
        _orderRepository = orderRepository;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(CreateOrderCommand message, IMessageContext context, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();
        var order = Order.Create(orderId, message.ProductId, message.Quantity);

        _logger.LogInformation("Persisting order {OrderId} with {ItemCount} items", orderId, order.Items.Count);

        // Capture uncommitted events before save (save clears them)
        var uncommittedEvents = order.GetUncommittedEvents().ToList();

        await _orderRepository.SaveAsync(order, cancellationToken).ConfigureAwait(false);

        // Dispatch domain events to update projections (CQRS read side)
        foreach (var domainEvent in uncommittedEvents)
        {
            await _dispatcher.DispatchAsync(domainEvent, context, cancellationToken).ConfigureAwait(false);
        }

        _logger.LogInformation("Order {OrderId} persisted and {EventCount} events dispatched for projections",
            orderId, uncommittedEvents.Count);

        context.SetResult(orderId);
    }
}
