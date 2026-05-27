using Excalibur.Dispatch.Abstractions.Messaging;

namespace SagaOrchestration.Sagas;

/// <summary>
/// State for the order fulfillment saga, tracking progress through each step.
/// Extends the framework's <see cref="SagaState"/> base class for built-in
/// persistence, versioning, and idempotent replay protection.
/// </summary>
public sealed class OrderSagaState : SagaState
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string? ReservationId { get; set; }
    public string? PaymentTransactionId { get; set; }
    public string? ShipmentTrackingNumber { get; set; }
    public string? TimeoutId { get; set; }
    public List<string> CompletedSteps { get; init; } = [];
    public string? FailureReason { get; set; }
}
