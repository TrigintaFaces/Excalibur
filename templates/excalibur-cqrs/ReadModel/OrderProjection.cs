using Company.ExcaliburCqrs.Domain.Events;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Projects order domain events into the <see cref="OrderReadModel"/>.
/// </summary>
public sealed class OrderProjection :
    IMessageHandler<OrderCreated>,
    IMessageHandler<OrderShipped>
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
    public async Task HandleAsync(OrderCreated message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Projecting OrderCreated for {OrderId}", message.OrderId);

        var readModel = new OrderReadModel
        {
            OrderId = message.OrderId,
            Status = "Created",
            TotalItems = message.Quantity,
            LastUpdated = message.OccurredAt
        };

        await _projectionStore.UpsertAsync(
            message.OrderId.ToString(), readModel, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task HandleAsync(OrderShipped message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Projecting OrderShipped for {OrderId}", message.OrderId);

        var existing = await _projectionStore.GetByIdAsync(
            message.OrderId.ToString(), cancellationToken).ConfigureAwait(false);

        if (existing is null)
        {
            _logger.LogWarning("OrderReadModel for {OrderId} not found — event may have arrived out of order", message.OrderId);
            return;
        }

        existing.Status = "Shipped";
        existing.LastUpdated = message.OccurredAt;

        await _projectionStore.UpsertAsync(
            message.OrderId.ToString(), existing, cancellationToken).ConfigureAwait(false);
    }
}
