using Excalibur.Application.Requests.Commands;

namespace Excalibur.A3.Authorization.Requests.Commands;

/// <summary>
///     Represents a command that requires authorization and produces a response.
/// </summary>
/// <typeparam name="TResponse"> The type of response produced by the command. </typeparam>
/// <remarks>
///     Combines the functionalities of <see cref="ICommand{TResponse}" /> and <see cref="IAmAuthorizable" />. This interface is intended
///     for commands that need to verify user permissions or access control before execution.
/// </remarks>
public interface IAuthorizeCommand<out TResponse> : ICommand<TResponse>, IAmAuthorizable
{
}
