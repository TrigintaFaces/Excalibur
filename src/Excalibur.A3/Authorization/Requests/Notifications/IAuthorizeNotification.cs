using Excalibur.Application.Requests.Notifications;

namespace Excalibur.A3.Authorization.Requests.Notifications;

/// <summary>
///     Represents a notification that requires authorization.
/// </summary>
/// <remarks>
///     Combines the functionalities of <see cref="INotification" /> and <see cref="IAmAuthorizable" />, enabling access control for
///     notifications within the system.
/// </remarks>
public interface IAuthorizeNotification : INotification, IAmAuthorizable
{
}
