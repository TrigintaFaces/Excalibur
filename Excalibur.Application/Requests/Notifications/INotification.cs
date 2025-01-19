namespace Excalibur.Application.Requests.Notifications;

/// <summary>
///     Represents a notification in the system, combining the properties of <see cref="IActivity" /> and MediatR notifications.
/// </summary>
public interface INotification : IActivity, MediatR.INotification
{
}
