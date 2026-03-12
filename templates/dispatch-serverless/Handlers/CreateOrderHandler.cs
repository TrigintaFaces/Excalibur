using Company.DispatchServerless.Messages;

using Excalibur.Dispatch.Abstractions;

using Microsoft.Extensions.Logging;

namespace Company.DispatchServerless.Handlers;

/// <summary>
/// Handler for <see cref="CreateOrderAction"/>.
/// </summary>
public sealed class CreateOrderHandler : IDispatchHandler<CreateOrderAction>
{
    private readonly ILogger<CreateOrderHandler> _logger;

    public CreateOrderHandler(ILogger<CreateOrderHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<IMessageResult> HandleAsync(
        CreateOrderAction message,
        IMessageContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Creating order {OrderId} for customer {CustomerId}, amount {Amount:C}",
            message.OrderId,
            message.CustomerId,
            message.TotalAmount);

        // TODO: Add your order creation logic here.
        // For example: save to database, publish integration events, etc.

        return Task.FromResult(MessageResult.Success());
    }
}
