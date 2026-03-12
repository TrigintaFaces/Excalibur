using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburOutbox.Messages;

/// <summary>
/// Command to place a new order, published reliably via the outbox pattern.
/// </summary>
public sealed record PlaceOrderCommand(string ProductId, int Quantity, decimal UnitPrice) : IDispatchAction;
