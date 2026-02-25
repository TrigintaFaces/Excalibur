using Company.ExcaliburDdd.Domain.Aggregates;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburDdd.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by loading an Order aggregate from the event store.
/// </summary>
public sealed class GetOrderQueryHandler : IMessageHandler<GetOrderQuery>
{
    private readonly IEventSourcedRepository<Order, Guid> _orderRepository;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(
        IEventSourcedRepository<Order, Guid> orderRepository,
        ILogger<GetOrderQueryHandler> logger)
    {
        _orderRepository = orderRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(GetOrderQuery message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading order {OrderId} from event store", message.OrderId);

        var order = await _orderRepository.GetByIdAsync(message.OrderId, cancellationToken).ConfigureAwait(false);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found in event store", message.OrderId);
            context.SetResult<object?>(null);
            return;
        }

        var result = new
        {
            order.Id,
            Status = order.Status.ToString(),
            Items = order.Items.Select(i => new { i.ProductId, i.Quantity }),
            order.Version
        };

        context.SetResult(result);
    }
}
