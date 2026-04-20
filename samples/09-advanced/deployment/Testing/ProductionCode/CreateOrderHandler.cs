using Excalibur.Dispatch.Abstractions.Delivery;

namespace Testing.ProductionCode;

/// <summary>
/// Handler for <see cref="CreateOrderCommand"/>.
/// Validates input, delegates to <see cref="IOrderRepository"/>, and returns the new order ID.
/// </summary>
public sealed class CreateOrderHandler : IActionHandler<CreateOrderCommand, string>
{
    private readonly IOrderRepository _repository;

    public CreateOrderHandler(IOrderRepository repository)
    {
        _repository = repository;
    }

    public async Task<string> HandleAsync(CreateOrderCommand action, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (action.Quantity <= 0)
        {
            throw new ArgumentException("Quantity must be positive.", nameof(action));
        }

        var orderId = await _repository.SaveAsync(action.ProductName, action.Quantity, cancellationToken)
            .ConfigureAwait(false);

        return orderId;
    }
}
