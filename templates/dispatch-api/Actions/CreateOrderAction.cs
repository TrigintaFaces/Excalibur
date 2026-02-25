namespace Company.DispatchApi.Actions;

/// <summary>
/// Represents a request to create a new order.
/// </summary>
public sealed record CreateOrderAction(string ProductId, int Quantity);
