namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// A closed-typed bridge that routes one compat request type through the canonical dispatch pipeline.
/// The source generator emits one registration per discovered request type and maps the request's
/// runtime <see cref="Type"/> to its bridge in the <see cref="CompatBridgeRegistry"/>, so the facade
/// resolves the right bridge by <c>request.GetType</c> without reflection.
/// </summary>
internal interface ICompatRequestBridge
{
    /// <summary>Routes the request through canonical middleware to its compat handler and returns the response.</summary>
    /// <param name="request">The compat request instance.</param>
    /// <param name="provider">The service provider used to resolve the handler, behaviors, and pipeline.</param>
    /// <param name="cancellationToken">A token to observe for cancellation.</param>
    /// <returns>The response, boxed as <see cref="object"/> (or <see langword="null"/> for a <see cref="Unit"/> response).</returns>
    Task<object?> SendAsync(object request, IServiceProvider provider, CancellationToken cancellationToken);
}
