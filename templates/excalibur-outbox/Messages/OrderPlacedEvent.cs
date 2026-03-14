using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburOutbox.Messages;

/// <summary>
/// Integration event raised when an order is placed.
/// Published reliably through the outbox to ensure at-least-once delivery.
/// </summary>
public sealed record OrderPlacedEvent(Guid OrderId, string ProductId, int Quantity, decimal TotalAmount, DateTimeOffset PlacedAt) : IDispatchEvent;
