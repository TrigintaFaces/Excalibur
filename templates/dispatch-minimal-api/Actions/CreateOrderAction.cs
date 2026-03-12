using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchMinimalApi.Actions;

/// <summary>
/// Represents a request to create a new order. Returns the new order ID.
/// </summary>
public sealed record CreateOrderAction(string ProductId, int Quantity) : IDispatchAction<Guid>;
