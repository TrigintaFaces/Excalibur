using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Closed-typed request bridge. Resolves the consumer's compat
/// <see cref="IRequestHandler{TRequest,TResponse}"/>, composes the registration-ordered
/// <see cref="IPipelineBehavior{TRequest,TResponse}"/> chain (MediatR semantics), and executes it as the
/// terminal handler of the canonical <see cref="IDispatchMiddlewareInvoker"/> pipeline — so canonical
/// middleware genuinely wraps the compat invocation, with no reflection on the dispatch path.
/// </summary>
/// <typeparam name="TRequest">The compat request type.</typeparam>
/// <typeparam name="TResponse">The response type.</typeparam>
internal sealed class CompatActionBridge<TRequest, TResponse> : ICompatRequestBridge
    where TRequest : notnull, IRequest<TResponse>
{
    /// <inheritdoc/>
    public async Task<object?> SendAsync(object request, IServiceProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);
        cancellationToken.ThrowIfCancellationRequested();

        var typedRequest = (TRequest)request;
        var handler = provider.GetRequiredService<IRequestHandler<TRequest, TResponse>>();

        // Compose behaviors in registration order: the first-registered behavior is outermost
        // (A -> B -> C -> handler -> C -> B -> A), so wrap from the handler outward in reverse.
        RequestHandlerDelegate<TResponse> pipeline = ct => handler.Handle(typedRequest, ct);
        var behaviors = provider.GetServices<IPipelineBehavior<TRequest, TResponse>>();
        foreach (var behavior in behaviors.Reverse())
        {
            var next = pipeline;
            var current = behavior;
            pipeline = ct => current.Handle(typedRequest, next, ct);
        }

        var response = await InvokeUnderCanonicalPipelineAsync(provider, typedRequest, pipeline, cancellationToken)
            .ConfigureAwait(false);
        return response;
    }

    private static async Task<TResponse> InvokeUnderCanonicalPipelineAsync(
        IServiceProvider provider,
        TRequest request,
        RequestHandlerDelegate<TResponse> pipeline,
        CancellationToken cancellationToken)
    {
        var invoker = provider.GetService<IDispatchMiddlewareInvoker>();
        var context = provider.GetService<IMessageContextFactory>()?.CreateContext();

        // No canonical pipeline available (e.g. a bare unit-test container): run the compat chain directly.
        if (invoker is null || context is null)
        {
            return await pipeline(cancellationToken).ConfigureAwait(false);
        }

        var wrapper = new CompatActionWrapper<TRequest, TResponse>(request);
        var result = await invoker.InvokeAsync<IMessageResult<TResponse>>(
            wrapper,
            context,
            async (_, _, ct) => MessageResult.Success(await pipeline(ct).ConfigureAwait(false)),
            cancellationToken).ConfigureAwait(false);

        return result.ReturnValue!;
    }
}
