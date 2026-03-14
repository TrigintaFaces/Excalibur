using Excalibur.Dispatch.Abstractions;

namespace Company.DispatchMinimalApi.Actions;

/// <summary>
/// Represents a request to retrieve an order by its identifier.
/// </summary>
public sealed record GetOrderAction(Guid OrderId) : IDispatchAction<OrderResult?>;

/// <summary>
/// Represents the result of an order query.
/// </summary>
public sealed record OrderResult(Guid Id, string ProductId, int Quantity, string Status);
