using MediatR;

namespace Excalibur.Application.Requests.Commands;

/// <summary>
///     Represents a command with a specific response type.
/// </summary>
/// <typeparam name="TResponse"> The type of the response returned by the command. </typeparam>
public interface ICommand<out TResponse> : IActivity, IRequest<TResponse>
{
}
