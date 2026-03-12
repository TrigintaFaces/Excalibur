using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburSaga.Messages;

/// <summary>
/// Event raised when payment has been collected for an order.
/// </summary>
public sealed record PaymentCollected(Guid OrderId, decimal Amount) : IDispatchEvent;

/// <summary>
/// Event raised when inventory has been reserved for an order.
/// </summary>
public sealed record InventoryReserved(Guid OrderId, string ProductId, int Quantity) : IDispatchEvent;

/// <summary>
/// Event raised when an order has been shipped.
/// </summary>
public sealed record OrderShipped(Guid OrderId, string TrackingNumber) : IDispatchEvent;
