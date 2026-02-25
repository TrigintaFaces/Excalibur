namespace Company.DispatchApi.Actions;

/// <summary>
/// Represents a request to retrieve an order by its identifier.
/// </summary>
public sealed record GetOrderAction(Guid OrderId);
