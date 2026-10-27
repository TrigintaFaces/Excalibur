using Excalibur.Dispatch.Compat.MediatR;

using MediatRMigration.Messages;

namespace MediatRMigration.Handlers;

/// <summary>
/// Request handler — incumbent MediatR <c>IRequestHandler&lt;TRequest,TResponse&gt;</c> shape.
/// Source-identical to the pre-migration MediatR handler.
/// </summary>
public sealed class PingHandler : IRequestHandler<Ping, string>
{
    /// <inheritdoc />
    public Task<string> Handle(Ping request, CancellationToken cancellationToken)
        => Task.FromResult($"pong:{request.Text}");
}
