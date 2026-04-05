using Company.ExcaliburCqrs.ReadModel;
using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by reading from the order projection store (CQRS read side).
/// </summary>
public sealed class GetOrderQueryHandler : IActionHandler<GetOrderQuery>
{
    private readonly IProjectionStore<OrderReadModel> _projectionStore;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(
        IProjectionStore<OrderReadModel> projectionStore,
        ILogger<GetOrderQueryHandler> logger)
    {
        _projectionStore = projectionStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying order read model for {OrderId}", action.OrderId);

        var readModel = await _projectionStore.GetByIdAsync(
            action.OrderId.ToString(), cancellationToken).ConfigureAwait(false);

        if (readModel is null)
        {
            _logger.LogWarning("Order {OrderId} not found in projection store", action.OrderId);
            return;
        }
    }
}
