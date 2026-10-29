using Excalibur.Dispatch;
using Excalibur.EventSourcing;

namespace Company.ExcaliburCqrs.Application.Commands;

/// <summary>
/// Command to create a new order.
/// </summary>
public sealed record CreateOrderCommand(string ProductId, int Quantity) : IDispatchAction<CreateOrderResult>;

/// <summary>The new identifier and the committed write receipt for a subsequent consistent read.</summary>
public sealed record CreateOrderResult(Guid OrderId, SaveResult Session);
