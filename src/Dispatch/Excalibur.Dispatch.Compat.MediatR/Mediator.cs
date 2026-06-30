using Excalibur.Dispatch.Compat.MediatR.Routing;

namespace Excalibur.Dispatch.Compat.MediatR;

/// <summary>
/// Default <see cref="IMediator"/> facade. Bridges the MediatR-compatible request/notification/stream
/// surface onto Excalibur.Dispatch: a request is routed by its runtime type to a
/// source-generated closed bridge that runs the compat handler + pipeline behaviors under the canonical
/// dispatch middleware pipeline — no reflection on the dispatch path.
/// </summary>
internal sealed class Mediator : IMediator
{
    private readonly CompatBridgeRegistry _registry;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>Initializes a new instance of the <see cref="Mediator"/> class.</summary>
    /// <param name="registry">The request→bridge registry populated by the source generator.</param>
    /// <param name="serviceProvider">The service provider used to resolve handlers, behaviors, and the canonical pipeline.</param>
    public Mediator(CompatBridgeRegistry registry, IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _registry = registry;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc/>
    public async Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var result = await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
        return (TResponse)result!;
    }

    /// <inheritdoc/>
    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return SendCoreAsync(request, cancellationToken);
    }

    private Task<object?> SendCoreAsync(object request, CancellationToken cancellationToken)
    {
        var bridge = _registry.GetRequestBridge(request.GetType())
            ?? throw HandlerNotFoundException.ForRequest(request.GetType());
        return bridge.SendAsync(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bridge = _registry.GetStreamBridge(request.GetType())
            ?? throw HandlerNotFoundException.ForRequest(request.GetType());
        return ((ICompatStreamBridge<TResponse>)bridge).CreateStream(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var bridge = _registry.GetStreamBridge(request.GetType())
            ?? throw HandlerNotFoundException.ForRequest(request.GetType());
        return bridge.CreateStreamUntyped(request, _serviceProvider, cancellationToken);
    }

    /// <inheritdoc/>
    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishCoreAsync(notification, cancellationToken);
    }

    /// <inheritdoc/>
    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        ArgumentNullException.ThrowIfNull(notification);
        return PublishCoreAsync(notification, cancellationToken);
    }

    private Task PublishCoreAsync(object notification, CancellationToken cancellationToken)
    {
        // No registered handlers for this notification type → publish is a no-op (MediatR semantics).
        var bridge = _registry.GetNotificationBridge(notification.GetType());
        return bridge is null
            ? Task.CompletedTask
            : bridge.PublishAsync(notification, _serviceProvider, cancellationToken);
    }
}
