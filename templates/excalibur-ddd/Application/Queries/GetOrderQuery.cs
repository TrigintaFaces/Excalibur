namespace Company.ExcaliburDdd.Application.Queries;

/// <summary>
/// Query to retrieve an order by its identifier.
/// </summary>
public sealed record GetOrderQuery(Guid OrderId);
