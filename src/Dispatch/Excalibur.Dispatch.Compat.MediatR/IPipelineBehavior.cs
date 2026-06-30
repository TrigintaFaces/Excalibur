namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Pipeline behavior that surrounds handling of a request, providing the
/// <c>IPipelineBehavior&lt;TRequest,TResponse&gt;</c> shape used by MediatR-based code; maps to the
/// canonical <c>IDispatchMiddleware</c>.
/// </summary>
/// <typeparam name="TRequest">The request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
public interface IPipelineBehavior<in TRequest, TResponse>
    where TRequest : notnull
{
    /// <summary>Runs surrounding behavior and invokes <paramref name="next"/> to continue the pipeline.</summary>
    /// <param name="request">The request instance.</param>
    /// <param name="next">The continuation that runs the next behavior or the handler.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the response.</returns>
    Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken);
}
