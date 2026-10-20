using Excalibur.Dispatch;

namespace Company.ExcaliburOutbox.Messages;

/// <summary>
/// Integration event raised when an order is placed.
/// Staged through the framework outbox for at-least-once delivery.
/// Crash durability depends on the selected storage provider.
/// </summary>
[MessageName("Company.ExcaliburOutbox.OrderPlaced")]
public sealed record OrderPlacedEvent(Guid OrderId, string ProductId, int Quantity, decimal TotalAmount, DateTimeOffset PlacedAt) : IIntegrationEvent;
