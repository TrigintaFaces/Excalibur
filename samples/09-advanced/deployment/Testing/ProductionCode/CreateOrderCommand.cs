using Excalibur.Dispatch.Abstractions;

namespace Testing.ProductionCode;

/// <summary>
/// Command that creates an order and returns the order ID.
/// Implements <see cref="IDispatchAction{TResponse}"/> so the handler returns a string.
/// </summary>
public sealed record CreateOrderCommand(string ProductName, int Quantity) : IDispatchAction<string>;
