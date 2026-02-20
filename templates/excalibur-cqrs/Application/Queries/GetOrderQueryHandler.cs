using Company.ExcaliburCqrs.ReadModel;
using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;

namespace Company.ExcaliburCqrs.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by reading from the order projection store (CQRS read side).
/// </summary>
public sealed class GetOrderQueryHandler : IMessageHandler<GetOrderQuery>
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
    public async Task HandleAsync(GetOrderQuery message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying order read model for {OrderId}", message.OrderId);

        var readModel = await _projectionStore.GetByIdAsync(
            message.OrderId.ToString(), cancellationToken).ConfigureAwait(false);

        if (readModel is null)
        {
            _logger.LogWarning("Order {OrderId} not found in projection store", message.OrderId);
            context.SetResult<OrderReadModel?>(null);
            return;
        }

        context.SetResult(readModel);
    }
}
