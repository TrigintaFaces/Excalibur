using System.Runtime.CompilerServices;

using Excalibur.Dispatch.Compat.MediatR;

using MediatRMigration.Messages;

namespace MediatRMigration.Handlers;

/// <summary>
/// Streaming request handler — incumbent MediatR <c>IStreamRequestHandler&lt;TRequest,TResponse&gt;</c>
/// shape. Yields the countdown from <see cref="Countdown.From"/> down to 1.
/// </summary>
public sealed class CountdownHandler : IStreamRequestHandler<Countdown, int>
{
    /// <inheritdoc />
    public async IAsyncEnumerable<int> Handle(
        Countdown request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        for (var i = request.From; i >= 1; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return i;
        }
    }
}
