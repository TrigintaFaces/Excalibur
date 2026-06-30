using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Compat.MediatR.Routing;

/// <summary>
/// Closed-typed notification bridge. Resolves every registered compat
/// <see cref="INotificationHandler{TNotification}"/> and invokes them sequentially (MediatR's default
/// publish semantics), hosted under the canonical <see cref="IDispatchMiddlewareInvoker"/> pipeline so
/// canonical middleware wraps the fan-out — no reflection on the dispatch path.
/// </summary>
/// <typeparam name="TNotification">The compat notification type.</typeparam>
internal sealed class CompatNotificationBridge<TNotification> : ICompatNotificationBridge
    where TNotification : notnull, INotification
{
    /// <inheritdoc/>
    public Task PublishAsync(object notification, IServiceProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(provider);

        var typed = (TNotification)notification;
        var invoker = provider.GetService<IDispatchMiddlewareInvoker>();
        var context = provider.GetService<IMessageContextFactory>()?.CreateContext();

        if (invoker is null || context is null)
        {
            return FanOutAsync(provider, typed, cancellationToken);
        }

        var wrapper = new CompatNotificationWrapper<TNotification>(typed);
        return invoker.InvokeAsync<IMessageResult>(
            wrapper,
            context,
            async (_, _, ct) =>
            {
                await FanOutAsync(provider, typed, ct).ConfigureAwait(false);
                return MessageResult.Success();
            },
            cancellationToken).AsTask();
    }

    private static async Task FanOutAsync(IServiceProvider provider, TNotification notification, CancellationToken cancellationToken)
    {
        // Sequential await in registration order — MediatR's default publish strategy.
        foreach (var handler in provider.GetServices<INotificationHandler<TNotification>>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            await handler.Handle(notification, cancellationToken).ConfigureAwait(false);
        }
    }
}
