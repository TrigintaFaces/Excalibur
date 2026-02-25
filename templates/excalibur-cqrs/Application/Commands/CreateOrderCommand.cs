namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(string ProductId, int Quantity);
