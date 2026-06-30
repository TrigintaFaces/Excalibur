using Excalibur.Dispatch.Compat.MediatR;

namespace MediatRMigration.Messages;

/// <summary>
/// A request/response message — incumbent MediatR <c>IRequest&lt;TResponse&gt;</c> shape.
/// Unchanged by the migration except the namespace of <c>IRequest&lt;&gt;</c>.
/// </summary>
public sealed record Ping(string Text) : IRequest<string>;

/// <summary>
/// A notification (publish/fan-out) message — incumbent MediatR <c>INotification</c> shape.
/// </summary>
public sealed record Pinged(string Text) : INotification;

/// <summary>
/// A streaming request — incumbent MediatR <c>IStreamRequest&lt;TResponse&gt;</c> shape.
/// </summary>
public sealed record Countdown(int From) : IStreamRequest<int>;
