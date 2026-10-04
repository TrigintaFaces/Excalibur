// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

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
    public async Task PublishAsync(object notification, IServiceProvider provider, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);
        ArgumentNullException.ThrowIfNull(provider);

        var typed = (TNotification)notification;
        var invoker = provider.GetService<IDispatchMiddlewareInvoker>();
        var factory = provider.GetService<IMessageContextFactory>();

        if (invoker is null || factory is null)
        {
            await FanOutAsync(provider, typed, cancellationToken).ConfigureAwait(false);
            return;
        }

        var context = factory.CreateContext();
        try
        {
            context.RequestServices = provider;
            var wrapper = new CompatNotificationWrapper<TNotification>(typed);
            var result = await invoker.InvokeAsync<IMessageResult>(
                wrapper,
                context,
                async (_, _, ct) =>
                {
                    await FanOutAsync(provider, typed, ct).ConfigureAwait(false);
                    return MessageResult.Success();
                },
                cancellationToken).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? result.ProblemDetails?.Detail ?? "The Dispatch pipeline rejected the notification.");
            }
        }
        finally
        {
            factory.Return(context);
        }
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
