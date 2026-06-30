namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Sends requests to a single handler and creates response streams. Provides the <c>ISender</c> shape
/// used by MediatR-based code; maps to the canonical <c>IDispatcher</c>.
/// </summary>
public interface ISender
{
    /// <summary>Sends a request to its handler and returns the response.</summary>
    /// <typeparam name="TResponse">The response type.</typeparam>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the response.</returns>
    Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
 /// Sends a request whose response type is not known at compile time. Provides the
    /// non-generic <c>Send(object, CancellationToken)</c> overload used by MediatR-based code.
    /// </summary>
    /// <param name="request">The request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>A task producing the response, boxed as <see cref="object"/>.</returns>
    Task<object?> Send(object request, CancellationToken cancellationToken = default);

    /// <summary>
 /// Creates an asynchronous stream of responses for a streaming request. Provides the
    /// <c>CreateStream&lt;TResponse&gt;</c> shape used by MediatR-based code.
    /// </summary>
    /// <typeparam name="TResponse">The element type of the stream.</typeparam>
    /// <param name="request">The streaming request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>An asynchronous sequence of response items.</returns>
    IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates an asynchronous stream of responses for a streaming request whose element type is not
 /// known at compile time. Provides the non-generic <c>CreateStream(object,...)</c>
    /// overload used by MediatR-based code.
    /// </summary>
    /// <param name="request">The streaming request instance.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>An asynchronous sequence of response items, boxed as <see cref="object"/>.</returns>
    IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default);
}
