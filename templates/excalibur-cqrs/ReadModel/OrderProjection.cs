using Company.ExcaliburCqrs.Domain.Events;
using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.ReadModel;

/// <summary>
/// Projects order domain events into the <see cref="OrderReadModel"/>.
/// </summary>
/// <remarks>
/// <para>
/// These handlers write with <c>UpsertAsync</c>, and that is the only write available to them: an
/// <c>IEventHandler&lt;T&gt;</c> receives the event and nothing else, so it has no global stream position
/// to write against. The code below is correct for this shape.
/// </para>
/// <para>
/// <b>What to know when you replace the in-memory store.</b> Every database-backed projection store
/// records a position, and a write that supplies none leaves the row recording that its state cannot be
/// related to any prefix of the event stream. Nothing goes wrong while these handlers are the only writer,
/// which is the case in this template.
/// </para>
/// <para>
/// It matters if you later ALSO register this projection through
/// <c>AddProjection&lt;OrderReadModel&gt;().Async()</c>, because the async host writes POSITIONED: the
/// first positioned write against such a row is refused terminally and the projection must be rebuilt from
/// the event stream before it can advance. An <c>.Inline()</c> registration is unaffected -- it writes
/// without a position too. So choose per projection, and rebuild as part of any migration to
/// <c>.Async()</c> rather than meeting the refusal at runtime.
/// </para>
/// </remarks>
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
