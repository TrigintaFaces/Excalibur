using Company.ExcaliburCqrs.Domain.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Projects order domain events into the <see cref="OrderReadModel"/>.
/// </summary>
public sealed class OrderProjection :
    IEventHandler<OrderCreated>,
    IEventHandler<OrderShipped>
{
    private readonly IProjectionStore<OrderReadModel> _projectionStore;
    private readonly ILogger<OrderProjection> _logger;

    public OrderProjection(
        IProjectionStore<OrderReadModel> projectionStore,
        ILogger<OrderProjection> logger)
    {
        _projectionStore = projectionStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderCreated eventMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Projecting OrderCreated for {OrderId}", eventMessage.OrderId);

        var readModel = new OrderReadModel
        {
            OrderId = eventMessage.OrderId,
            Status = "Created",
            TotalItems = eventMessage.Quantity,
            LastUpdated = eventMessage.OccurredAt
        };

        await _projectionStore.UpsertAsync(
            eventMessage.OrderId.ToString(), readModel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderShipped eventMessage, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Projecting OrderShipped for {OrderId}", eventMessage.OrderId);

        var existing = await _projectionStore.GetByIdAsync(
            eventMessage.OrderId.ToString(), cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            _logger.LogWarning("OrderReadModel for {OrderId} not found — event may have arrived out of order", eventMessage.OrderId);
            return;
        }

        existing.Status = "Shipped";
        existing.LastUpdated = eventMessage.OccurredAt;

        await _projectionStore.UpsertAsync(
            eventMessage.OrderId.ToString(), existing, cancellationToken).ConfigureAwait(false);
    }
}
