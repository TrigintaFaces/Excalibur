namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Marker interface for a notification (event) that may be handled by zero or more handlers.
/// Provides the <c>INotification</c> shape used by MediatR-based code; maps to a canonical Dispatch event.
/// </summary>
public interface INotification;
