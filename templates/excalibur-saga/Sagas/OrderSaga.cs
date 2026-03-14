using Excalibur.Saga.Abstractions;

namespace Company.ExcaliburSaga.Sagas;

/// <summary>
/// Order processing saga that coordinates payment, inventory, and shipping.
/// Demonstrates a multi-step process manager with compensation logic.
/// </summary>
public sealed class OrderSaga : ISagaDefinition<OrderSagaData>
{
    /// <inheritdoc />
    public string Name => "OrderProcessing";

    /// <inheritdoc />
    public TimeSpan Timeout => TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public IReadOnlyList<ISagaStep<OrderSagaData>> Steps { get; } = new ISagaStep<OrderSagaData>[]
    {
        new CollectPaymentStep(),
        new ReserveInventoryStep(),
        new ShipOrderStep(),
    };

    /// <inheritdoc />
    public ISagaRetryPolicy? RetryPolicy => null;

    /// <inheritdoc />
    public Task OnCompletedAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        Console.WriteLine($"Order {data.OrderId} completed. Tracking: {data.TrackingNumber}");
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task OnFailedAsync(ISagaContext<OrderSagaData> context, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Order {context.Data.OrderId} failed: {exception.Message}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Step 1: Collect payment for the order.
/// </summary>
internal sealed class CollectPaymentStep : ISagaStep<OrderSagaData>
{
    public string Name => "CollectPayment";
    public bool CanCompensate => true;
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public Task<StepResult> ExecuteAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        // TODO: Integrate with your payment service
        data.PaymentCollected = true;
        Console.WriteLine($"Payment of {data.TotalAmount:C} collected for order {data.OrderId}");
        return Task.FromResult(StepResult.Success);
    }

    public Task<StepResult> CompensateAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        // TODO: Refund payment
        data.PaymentCollected = false;
        Console.WriteLine($"Payment refunded for order {data.OrderId}");
        return Task.FromResult(StepResult.Success);
    }
}

/// <summary>
/// Step 2: Reserve inventory for the order.
/// </summary>
internal sealed class ReserveInventoryStep : ISagaStep<OrderSagaData>
{
    public string Name => "ReserveInventory";
    public bool CanCompensate => true;
    public TimeSpan Timeout => TimeSpan.FromMinutes(5);

    public Task<StepResult> ExecuteAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        // TODO: Integrate with your inventory service
        data.InventoryReserved = true;
        Console.WriteLine($"Reserved {data.Quantity}x {data.ProductId} for order {data.OrderId}");
        return Task.FromResult(StepResult.Success);
    }

    public Task<StepResult> CompensateAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        // TODO: Release inventory reservation
        data.InventoryReserved = false;
        Console.WriteLine($"Released inventory for order {data.OrderId}");
        return Task.FromResult(StepResult.Success);
    }
}

/// <summary>
/// Step 3: Ship the order.
/// </summary>
internal sealed class ShipOrderStep : ISagaStep<OrderSagaData>
{
    public string Name => "ShipOrder";
    public bool CanCompensate => false;
    public TimeSpan Timeout => TimeSpan.FromMinutes(10);

    public Task<StepResult> ExecuteAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        var data = context.Data;
        // TODO: Integrate with your shipping service
        data.TrackingNumber = $"TRACK-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
        Console.WriteLine($"Order {data.OrderId} shipped with tracking {data.TrackingNumber}");
        return Task.FromResult(StepResult.Success);
    }

    public Task<StepResult> CompensateAsync(ISagaContext<OrderSagaData> context, CancellationToken cancellationToken)
    {
        // Shipping cannot be compensated once dispatched
        return Task.FromResult(StepResult.Success);
    }
}
