using Company.ExcaliburCqrs.Domain.Aggregates;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>Ships an existing order and returns its write receipt, or null when absent.</summary>
public sealed record ShipOrderCommand(Guid OrderId) : IDispatchAction<SaveResult?>;

public sealed class ShipOrderCommandHandler(IEventSourcedRepository<Order, Guid> repository)
    : IActionHandler<ShipOrderCommand, SaveResult?>
{
    public async Task<SaveResult?> HandleAsync(ShipOrderCommand action, CancellationToken cancellationToken)
    {
        var order = await repository.GetByIdAsync(action.OrderId, cancellationToken).ConfigureAwait(false);
        if (order is null) { return null; }
        order.Ship();
        return await repository.SaveAsync(order, cancellationToken).ConfigureAwait(false);
    }
}
