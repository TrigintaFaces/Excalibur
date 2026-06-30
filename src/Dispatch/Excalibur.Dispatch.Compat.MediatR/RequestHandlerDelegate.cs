namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Represents the continuation of a request-handling pipeline — invoking it runs the next
/// <see cref="IPipelineBehavior{TRequest,TResponse}"/> (or the handler). Provides the
/// <c>RequestHandlerDelegate&lt;TResponse&gt;</c> shape used by MediatR-based code.
/// </summary>
/// <typeparam name="TResponse">The response type produced by the remainder of the pipeline.</typeparam>
/// <param name="cancellationToken">
/// A token to observe for cancellation. Defaulted so legacy <c>await next()</c> call sites continue
/// to compile, while MediatR 12.5+ <c>next(cancellationToken)</c> call sites bind the token.
/// </param>
/// <returns>A task producing the response.</returns>
public delegate Task<TResponse> RequestHandlerDelegate<TResponse>(CancellationToken cancellationToken = default);
