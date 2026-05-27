using Excalibur.Dispatch.Abstractions;

namespace SagaOrchestration.Sagas;

/// <summary>
/// Initiates the order fulfillment saga. Registered as a "start" event
/// so the framework creates a new saga instance on first receipt.
/// </summary>
public sealed record StartOrderProcessing(
    string SagaId,
    string OrderId,
    string CustomerId,
    decimal TotalAmount) : ISagaEvent
{
    public string? StepId => null;
}

/// <summary>
/// Indicates inventory has been successfully reserved for the order.
/// </summary>
public sealed record InventoryReserved(
    string SagaId,
    string ReservationId) : ISagaEvent
{
    public string? StepId => "ReserveInventory";
}

/// <summary>
/// Indicates payment has been successfully processed.
/// </summary>
public sealed record PaymentProcessed(
    string SagaId,
    string TransactionId) : ISagaEvent
{
    public string? StepId => "ProcessPayment";
}

/// <summary>
/// Indicates the order has been shipped.
/// </summary>
public sealed record OrderShipped(
    string SagaId,
    string TrackingNumber) : ISagaEvent
{
    public string? StepId => "ShipOrder";
}

/// <summary>
/// Indicates payment processing failed.
/// </summary>
public sealed record PaymentFailed(
    string SagaId,
    string Reason) : ISagaEvent
{
    public string? StepId => "ProcessPayment";
}
