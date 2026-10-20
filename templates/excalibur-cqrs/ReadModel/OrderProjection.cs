using Company.ExcaliburCqrs.Domain.Events;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Projects order events using framework-managed loading and persistence.
/// </summary>
/// <remarks>
/// Registered through AddProjection with Inline and WhenHandledBy. Inline writes are unpositioned;
/// migrating an existing database projection to Async requires rebuilding it from the event stream.
/// Projection failures propagate after event persistence and do not roll back committed events.
/// </remarks>
public sealed class OrderProjection :
    IProjectionEventHandler<OrderReadModel, OrderCreated>,
    IProjectionEventHandler<OrderReadModel, OrderShipped>
{
    private readonly ILogger<OrderProjection> _logger;

    public OrderProjection(ILogger<OrderProjection> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(OrderReadModel projection, OrderCreated eventMessage,
        ProjectionHandlerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projection.OrderId = eventMessage.OrderId;
        projection.Status = "Created";
        projection.TotalItems = eventMessage.Quantity;
        projection.LastUpdated = eventMessage.OccurredAt;
        _logger.LogInformation("Projecting OrderCreated for {OrderId}", eventMessage.OrderId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task HandleAsync(OrderReadModel projection, OrderShipped eventMessage,
        ProjectionHandlerContext context, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        projection.OrderId = eventMessage.OrderId;
        projection.Status = "Shipped";
        projection.LastUpdated = eventMessage.OccurredAt;
        _logger.LogInformation("Projecting OrderShipped for {OrderId}", eventMessage.OrderId);
        return Task.CompletedTask;
    }
}
