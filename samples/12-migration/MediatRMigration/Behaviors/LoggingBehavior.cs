using Excalibur.Dispatch.Compat.MediatR;

namespace MediatRMigration.Behaviors;

/// <summary>
/// An open pipeline behavior — incumbent MediatR <c>IPipelineBehavior&lt;TRequest,TResponse&gt;</c> shape
/// (registered via <c>AddOpenBehavior(typeof(LoggingBehavior&lt;,&gt;))</c>). Wraps every request,
/// demonstrating that behavior ordering and the <see cref="RequestHandlerDelegate{TResponse}"/>
/// continuation are preserved after the migration.
/// </summary>
public sealed class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    /// <inheritdoc />
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"[pipeline] -> {typeof(TRequest).Name}");
        var response = await next(cancellationToken);
        Console.WriteLine($"[pipeline] <- {typeof(TRequest).Name}");
        return response;
    }
}
