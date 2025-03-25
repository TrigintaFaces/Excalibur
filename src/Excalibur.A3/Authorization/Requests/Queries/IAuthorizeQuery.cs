using Excalibur.Application.Requests.Queries;

namespace Excalibur.A3.Authorization.Requests.Queries;

/// <summary>
///     Represents a query that requires authorization.
/// </summary>
/// <typeparam name="TResponse"> The type of response produced by the query. </typeparam>
/// <remarks>
///     Combines the functionalities of <see cref="IQuery{TResponse}" /> and <see cref="IAmAuthorizable" />, enabling access control for
///     queries within the system.
/// </remarks>
public interface IAuthorizeQuery<out TResponse> : IQuery<TResponse>, IAmAuthorizable
{
}
