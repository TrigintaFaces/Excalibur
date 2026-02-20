using Company.DispatchApi.Actions;
using Company.DispatchApi.Infrastructure;
using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchApi.Handlers;

/// <summary>
/// Handles <see cref="GetOrderAction"/> requests.
/// </summary>
public sealed class GetOrderHandler : IMessageHandler<GetOrderAction>
{
    private readonly InMemoryOrderStore _orderStore;
    private readonly ILogger<GetOrderHandler> _logger;

    public GetOrderHandler(InMemoryOrderStore orderStore, ILogger<GetOrderHandler> logger)
    {
        _orderStore = orderStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(GetOrderAction message, IMessageContext context, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving order {OrderId}", message.OrderId);

        var order = _orderStore.GetById(message.OrderId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found", message.OrderId);
            context.SetResult<object?>(null);
            return Task.CompletedTask;
        }

        context.SetResult(new { order.Id, order.ProductId, order.Quantity, order.Status });
        return Task.CompletedTask;
    }
}
