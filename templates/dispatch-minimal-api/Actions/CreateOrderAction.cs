using Excalibur.Dispatch;

namespace Company.DispatchMinimalApi.Actions;

/// <summary>
/// Represents a request to create a new order. Returns the created order result.
/// </summary>
public sealed record CreateOrderAction(string ProductId, int Quantity) : IDispatchAction<CreateOrderResult>;

/// <summary>
/// The result of creating an order. A reference type so it can flow through the
/// <c>DispatchPostAction</c> + <c>ToHttpResult</c> railway-to-HTTP bridge.
/// </summary>
public sealed record CreateOrderResult(Guid OrderId);
