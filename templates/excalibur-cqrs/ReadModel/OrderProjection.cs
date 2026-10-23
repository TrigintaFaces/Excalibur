using Company.ExcaliburCqrs.Domain.Events;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Projects order events using framework-managed loading and persistence.
/// </summary>
/// <remarks>
/// Registered through AddProjection with Async and WhenHandledBy, and applied by the projection host
/// in global-stream order -- so this type is the only writer of these rows. A read that must observe a
/// particular write passes that write's session token to IProjectionSessionReader.ReadAsync.
/// A failure here does not roll back the committed events; the host halts at the failure rather than
/// advancing past it, so the projection resumes from the same event once the fault is fixed.
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
