using Excalibur.Dispatch;

namespace Company.ExcaliburSaga.Messages;

/// <summary>
/// Event raised when payment has been collected for an order.
/// </summary>
public sealed record PaymentCollected(Guid OrderId, decimal Amount) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "CollectPayment";
}

/// <summary>
/// Event raised when inventory has been reserved for an order.
/// </summary>
public sealed record InventoryReserved(Guid OrderId, string ProductId, int Quantity) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "ReserveInventory";
}

/// <summary>
/// Event raised when an order has been shipped.
/// </summary>
public sealed record OrderShipped(Guid OrderId, string TrackingNumber) : ISagaEvent
{
    public string SagaId => OrderId.ToString();
    public string? StepId => "ShipOrder";
}
