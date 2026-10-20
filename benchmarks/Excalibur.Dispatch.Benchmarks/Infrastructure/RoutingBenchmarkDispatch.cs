// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Features;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Benchmarks.Infrastructure;

/// <summary>Owns a benchmark context through routing setup and dispatch completion.</summary>
internal static class RoutingBenchmarkDispatch
{
    /// <summary>Preserves the queued fixture's always-awaited, interface-typed dispatch.</summary>
    internal static async Task<IMessageResult> DispatchQueuedAsync(
        IDispatcher dispatcher, IMessageContextFactory contextFactory,
        IDispatchMessage message, RoutingDecision decision)
    {
        var context = contextFactory.CreateContext();
        try
        {
            context.GetOrCreateRoutingFeature().RoutingDecision = decision;
            return await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    /// <summary>Dispatches the original explicit-context workload and returns its rental.</summary>
    internal static async Task<IMessageResult> DispatchAsync<TMessage>(
        IDispatcher? dispatcher,
        IMessageContextFactory? contextFactory,
        TMessage message,
        RoutingDecision decision,
        Action<IMessageContext>? configureContext = null)
        where TMessage : IDispatchMessage
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        try
        {
            context.GetOrCreateRoutingFeature().RoutingDecision = decision;
            configureContext?.Invoke(context);
            var dispatchTask = dispatcher.DispatchAsync(message, context, CancellationToken.None);
            return dispatchTask.IsCompletedSuccessfully
                ? dispatchTask.Result
                : await dispatchTask.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    /// <summary>Dispatches the original typed workload and returns its rental.</summary>
    internal static async Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
        IDispatcher? dispatcher,
        IMessageContextFactory? contextFactory,
        TMessage message,
        RoutingDecision decision)
        where TMessage : IDispatchAction<TResponse>
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        try
        {
            context.GetOrCreateRoutingFeature().RoutingDecision = decision;
            var dispatchTask = dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
            return dispatchTask.IsCompletedSuccessfully
                ? dispatchTask.Result
                : await dispatchTask.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }
}
