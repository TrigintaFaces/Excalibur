using Excalibur.Dispatch;
using Excalibur.EventSourcing;
using Company.ExcaliburCqrs.ReadModel;

namespace Company.ExcaliburCqrs.Application.Queries;

/// <summary>
/// Query to retrieve an order read model by its identifier.
/// </summary>
public sealed record GetOrderQuery(Guid OrderId, SaveResult Session = default) : IDispatchAction<ProjectionRead<OrderReadModel>>;
