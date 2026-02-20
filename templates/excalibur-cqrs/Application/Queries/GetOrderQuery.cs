namespace Company.ExcaliburCqrs.Application.Queries;

/// <summary>
/// Query to retrieve an order read model by its identifier.
/// </summary>
public sealed record GetOrderQuery(Guid OrderId);
