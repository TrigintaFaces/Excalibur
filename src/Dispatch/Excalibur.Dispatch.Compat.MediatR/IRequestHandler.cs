namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> and produces a
/// <typeparamref name="TResponse"/>. Provides the <c>IRequestHandler&lt;TRequest,TResponse&gt;</c> shape
/// used by MediatR-based code; maps to the canonical <c>IActionHandler</c> / <c>IDispatchHandler</c>.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
/// <typeparam name="TResponse">The response type produced.</typeparam>
public interface IRequestHandler<in TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    /// <summary>Handles the request.</summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the response.</returns>
    Task<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Handles a request of type <typeparamref name="TRequest"/> that produces no meaningful response.
/// Provides the <c>IRequestHandler&lt;TRequest&gt;</c> shape used by MediatR-based code; a specialization
/// returning <see cref="Unit"/>.
/// </summary>
/// <typeparam name="TRequest">The request type handled.</typeparam>
public interface IRequestHandler<in TRequest> : IRequestHandler<TRequest, Unit>
    where TRequest : IRequest<Unit>;
