using Company.DispatchMinimalApi.Actions;
using Company.DispatchMinimalApi.Infrastructure;
using Excalibur.Dispatch.Abstractions.Delivery;

namespace Company.DispatchMinimalApi.Handlers;

/// <summary>
/// Handles <see cref="CreateOrderAction"/> requests.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderAction, Guid>
{
    private readonly InMemoryOrderStore _orderStore;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(InMemoryOrderStore orderStore, ILogger<CreateOrderHandler> logger)
    {
        _orderStore = orderStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<Guid> HandleAsync(CreateOrderAction action, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();

        _orderStore.Save(orderId, action.ProductId, action.Quantity, "Created");

        _logger.LogInformation("Order {OrderId} created for product {ProductId}, quantity {Quantity}",
            orderId, action.ProductId, action.Quantity);

        return Task.FromResult(orderId);
    }
}
