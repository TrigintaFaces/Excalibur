using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchServerless.Messages;

/// <summary>
/// Action to create a new order.
/// </summary>
public record CreateOrderAction(
    string OrderId,
    string CustomerId,
    decimal TotalAmount) : IDispatchMessage;
