using Company.ExcaliburCqrs.ReadModel;
using Excalibur.Dispatch.Delivery;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.Application.Queries;

/// <summary>
/// Handles <see cref="GetOrderQuery"/> by reading from the order projection store (CQRS read side).
/// </summary>
public sealed class GetOrderQueryHandler : IActionHandler<GetOrderQuery, ProjectionRead<OrderReadModel>>
{
    private readonly IProjectionSessionReader _reader;
    private readonly ILogger<GetOrderQueryHandler> _logger;

    public GetOrderQueryHandler(
        IProjectionSessionReader reader,
        ILogger<GetOrderQueryHandler> logger)
    {
        _reader = reader;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ProjectionRead<OrderReadModel>> HandleAsync(GetOrderQuery action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Querying order read model for {OrderId}", action.OrderId);

        var readModel = await _reader.ReadAsync<OrderReadModel>(
            action.OrderId.ToString(), action.Session, cancellationToken).ConfigureAwait(false);

        if (readModel.Projection is null)
        {
            _logger.LogWarning("Order {OrderId} not found in projection store", action.OrderId);
        }
        return readModel;
    }
}
