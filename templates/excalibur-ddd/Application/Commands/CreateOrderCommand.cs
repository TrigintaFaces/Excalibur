using Excalibur.Dispatch.Abstractions;

namespace Company.ExcaliburDdd.Application.Commands;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction;
