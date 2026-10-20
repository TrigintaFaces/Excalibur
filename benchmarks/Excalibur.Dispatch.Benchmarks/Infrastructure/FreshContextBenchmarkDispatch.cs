// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Benchmarks.Infrastructure;

/// <summary>Returns benchmark rentals while preserving each fixture's completion shape.</summary>
internal static class FreshContextBenchmarkDispatch
{
    internal static Task<IMessageResult> DispatchAsync<TMessage>(
        IDispatcher? dispatcher, IMessageContextFactory? contextFactory, TMessage message)
        where TMessage : IDispatchMessage
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        Task<IMessageResult> task;
        try
        {
            task = dispatcher.DispatchAsync(message, context, CancellationToken.None);
        }
        catch
        {
            contextFactory.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            contextFactory.Return(context);
            return task;
        }

        return AwaitAndReturnAsync(task, contextFactory, context);
    }

    internal static Task<IMessageResult<TResponse>> DispatchAsync<TMessage, TResponse>(
        IDispatcher? dispatcher, IMessageContextFactory? contextFactory, TMessage message)
        where TMessage : IDispatchAction<TResponse>
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        Task<IMessageResult<TResponse>> task;
        try
        {
            task = dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
        }
        catch
        {
            contextFactory.Return(context);
            throw;
        }

        if (task.IsCompletedSuccessfully)
        {
            contextFactory.Return(context);
            return task;
        }

        return AwaitAndReturnAsync(task, contextFactory, context);
    }

    internal static async Task<IMessageResult> DispatchAwaitedAsync<TMessage>(
        IDispatcher? dispatcher, IMessageContextFactory? contextFactory, TMessage message)
        where TMessage : IDispatchMessage
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        try
        {
            var task = dispatcher.DispatchAsync(message, context, CancellationToken.None);
            return task.IsCompletedSuccessfully ? task.Result : await task.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    internal static async Task<IMessageResult<TResponse>> DispatchAwaitedAsync<TMessage, TResponse>(
        IDispatcher? dispatcher, IMessageContextFactory? contextFactory, TMessage message)
        where TMessage : IDispatchAction<TResponse>
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        try
        {
            var task = dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
            return task.IsCompletedSuccessfully ? task.Result : await task.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    private static async Task<IMessageResult> AwaitAndReturnAsync(
        Task<IMessageResult> task, IMessageContextFactory contextFactory, IMessageContext context)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    private static async Task<IMessageResult<TResponse>> AwaitAndReturnAsync<TResponse>(
        Task<IMessageResult<TResponse>> task, IMessageContextFactory contextFactory, IMessageContext context)
    {
        try
        {
            return await task.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }
}
