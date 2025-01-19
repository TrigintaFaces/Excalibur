using MediatR;

namespace Excalibur.Domain.Events;

/// <summary>
///     Marker interface for domain notifications, integrating with MediatR.
/// </summary>
public interface IDomainNotification : IDomainEvent, INotification
{
}
