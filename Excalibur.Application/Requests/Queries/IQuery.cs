using MediatR;

namespace Excalibur.Application.Requests.Queries;

/// <summary>
///     Represents a query operation in the system, combining the properties of <see cref="IActivity" /> and MediatR's <see cref="IRequest{TResponse}" />.
/// </summary>
/// <typeparam name="TResponse"> The type of response the query produces. </typeparam>
public interface IQuery<out TResponse> : IActivity, IRequest<TResponse>
{
}
