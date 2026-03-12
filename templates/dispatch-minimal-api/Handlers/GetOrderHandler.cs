using Company.DispatchMinimalApi.Actions;
using Company.DispatchMinimalApi.Infrastructure;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Company.DispatchMinimalApi.Handlers;

/// <summary>
/// Handles <see cref="GetOrderAction"/> requests.
/// </summary>
public sealed class GetOrderHandler : IActionHandler<GetOrderAction, OrderResult?>
{
    private readonly InMemoryOrderStore _orderStore;
    private readonly ILogger<GetOrderHandler> _logger;

    public GetOrderHandler(InMemoryOrderStore orderStore, ILogger<GetOrderHandler> logger)
    {
        _orderStore = orderStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<OrderResult?> HandleAsync(GetOrderAction action, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving order {OrderId}", action.OrderId);

        var order = _orderStore.GetById(action.OrderId);

        if (order is null)
        {
            _logger.LogWarning("Order {OrderId} not found", action.OrderId);
            return Task.FromResult<OrderResult?>(null);
        }

        return Task.FromResult<OrderResult?>(new OrderResult(order.Id, order.ProductId, order.Quantity, order.Status));
    }
}
