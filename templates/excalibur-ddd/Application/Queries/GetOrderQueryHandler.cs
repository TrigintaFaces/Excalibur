using Company.ExcaliburDdd.Domain.Aggregates;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburDdd.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by loading an Order aggregate from the event store.
/// </summary>
public sealed class GetOrderQueryHandler : IActionHandler<GetOrderQuery>
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
    public async Task HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Loading order {OrderId} from event store", action.OrderId);

        var order = await _orderRepository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found in event store", action.OrderId);
            return;
        }

        _logger.LogInformation("Order {OrderId} loaded at version {Version}", order.Id, order.Version);
    }
}
