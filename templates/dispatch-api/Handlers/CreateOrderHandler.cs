using Company.DispatchApi.Actions;
using Company.DispatchApi.Infrastructure;
using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchApi.Handlers;

/// <summary>
/// Handles <see cref="CreateOrderAction"/> requests.
/// </summary>
public sealed class CreateOrderHandler : IMessageHandler<CreateOrderAction>
{
    private readonly InMemoryOrderStore _orderStore;
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(InMemoryOrderStore orderStore, ILogger<CreateOrderHandler> logger)
    {
        _orderStore = orderStore;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task HandleAsync(CreateOrderAction message, IMessageContext context, CancellationToken cancellationToken)
    {
        var orderId = Guid.NewGuid();

        _orderStore.Save(orderId, message.ProductId, message.Quantity, "Created");

        _logger.LogInformation("Order {OrderId} created for product {ProductId}, quantity {Quantity}",
            orderId, message.ProductId, message.Quantity);

        context.SetResult(orderId);
        return Task.CompletedTask;
    }
}
