namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Non-generic base for a compat stream bridge — used by the non-generic
/// <see cref="ISender.CreateStream(object, System.Threading.CancellationToken)"/> overload (items boxed).
/// </summary>
internal interface ICompatStreamBridge
{
    /// <summary>Streams the response items, boxed as <see cref="object"/> (non-generic overload).</summary>
    /// <param name="request">The compat stream-request instance.</param>
    /// <param name="provider">The service provider used to resolve the stream handler.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The asynchronous stream of response items, boxed.</returns>
    IAsyncEnumerable<object?> CreateStreamUntyped(object request, IServiceProvider provider, CancellationToken cancellationToken);
}

/// <summary>
/// Closed-typed stream bridge. The generic element type keeps value-type items unboxed (EC-9) on the
/// common <see cref="ISender.CreateStream{TResponse}(IStreamRequest{TResponse}, System.Threading.CancellationToken)"/>
/// path. The source generator emits one per discovered stream-request type, mapped by runtime
/// <see cref="System.Type"/> in the <see cref="CompatBridgeRegistry"/>.
/// </summary>
/// <typeparam name="TResponse">The stream element type.</typeparam>
internal interface ICompatStreamBridge<out TResponse> : ICompatStreamBridge
{
    /// <summary>Streams the response items (unboxed).</summary>
    /// <param name="request">The compat stream-request instance.</param>
    /// <param name="provider">The service provider used to resolve the stream handler.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The asynchronous stream of response items.</returns>
    IAsyncEnumerable<TResponse> CreateStream(object request, IServiceProvider provider, CancellationToken cancellationToken);
}
