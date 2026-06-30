namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Handles a streaming request of type <typeparamref name="TRequest"/>, yielding an asynchronous
/// sequence of <typeparamref name="TResponse"/>. Provides the
/// <c>IStreamRequestHandler&lt;TRequest,TResponse&gt;</c> shape used by MediatR-based code; maps to the
/// canonical <c>IStreamingDispatcher</c>.
/// </summary>
/// <typeparam name="TRequest">The streaming request type handled.</typeparam>
/// <typeparam name="TResponse">The element type produced.</typeparam>
public interface IStreamRequestHandler<in TRequest, TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <summary>Handles the streaming request.</summary>
    /// <param name="request">The streaming request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>An asynchronous sequence of response items.</returns>
    IAsyncEnumerable<TResponse> Handle(TRequest request, CancellationToken cancellationToken);
}
