using Excalibur.Dispatch.Compat.MediatR;

using MediatRMigration.Messages;

namespace MediatRMigration.Handlers;

/// <summary>
/// First notification handler — incumbent MediatR <c>INotificationHandler&lt;T&gt;</c> shape.
/// Demonstrates publish fan-out: multiple handlers for one notification are all invoked.
/// </summary>
public sealed class PingedLogHandler : INotificationHandler<Pinged>
{
    /// <inheritdoc />
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[log]   Pinged: {notification.Text}");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Second notification handler for the same notification — proves fan-out to all handlers.
/// </summary>
public sealed class PingedAuditHandler : INotificationHandler<Pinged>
{
    /// <inheritdoc />
    public Task Handle(Pinged notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"[audit] Pinged: {notification.Text}");
        return Task.CompletedTask;
    }
}
