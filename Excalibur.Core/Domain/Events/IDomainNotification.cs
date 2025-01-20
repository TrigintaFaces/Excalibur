using MediatR;

namespace Excalibur.Core.Domain.Events;

/// <summary>
///     Marker interface for domain notifications, integrating with MediatR.
/// </summary>
public interface IDomainNotification : IDomainEvent, INotification
{
}
