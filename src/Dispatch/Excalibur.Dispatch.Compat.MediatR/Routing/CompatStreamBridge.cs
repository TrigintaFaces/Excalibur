using System.Runtime.CompilerServices;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Closed-typed stream bridge. Resolves the consumer's compat
/// <see cref="IStreamRequestHandler{TRequest,TResponse}"/> and yields its items with no reflection and no
/// boxing of value-type elements (EC-9).
/// </summary>
/// <typeparam name="TRequest">The compat stream-request type.</typeparam>
/// <typeparam name="TResponse">The stream element type.</typeparam>
internal sealed class CompatStreamBridge<TRequest, TResponse> : ICompatStreamBridge<TResponse>
    where TRequest : IStreamRequest<TResponse>
{
    /// <inheritdoc/>
    public async IAsyncEnumerable<TResponse> CreateStream(
        object request,
        IServiceProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);

        var typedRequest = (TRequest)request;
        var handler = provider.GetRequiredService<IStreamRequestHandler<TRequest, TResponse>>();

        await foreach (var item in handler.Handle(typedRequest, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<object?> CreateStreamUntyped(
        object request,
        IServiceProvider provider,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in CreateStream(request, provider, cancellationToken).WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            yield return item;
        }
    }
}
