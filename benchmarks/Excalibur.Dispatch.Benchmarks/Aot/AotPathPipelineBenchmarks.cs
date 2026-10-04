// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Aot;

/// <summary>Managed-process comparison of expression and container handler activation.</summary>
/// <remarks>
/// Both arms run under HostProcess with the same runtime-selected handler invoker. The second arm
/// selects AotHandlerActivator, but this is not a Native AOT versus JIT runtime comparison.
/// Warm-up validates actual handler/context execution before timing; owned contexts are returned.
/// Publish and execute a native consumer separately to establish Native AOT compatibility.
/// </remarks>
[BenchmarkCategory("ManagedActivatorComparison")]
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AotPathPipelineBenchmarks
{
    private IServiceProvider _jitProvider = null!;
    private IServiceProvider _aotProvider = null!;
    private IDispatcher _jitDispatcher = null!;
    private IDispatcher _aotDispatcher = null!;
    private IMessageContextFactory _jitContextFactory = null!;
    private IMessageContextFactory _aotContextFactory = null!;
    private PipelineBenchCommand _command = null!;

    [GlobalSetup]
    public void Setup()
    {
        _command = new PipelineBenchCommand { OrderId = Guid.NewGuid(), Amount = 99.99m };

        // JIT path: standard pipeline with 3 middleware layers
        var jitServices = ConfigureServices(useAotActivator: false);
        _jitProvider = jitServices.BuildServiceProvider();
        _jitDispatcher = _jitProvider.GetRequiredService<IDispatcher>();
        _jitContextFactory = _jitProvider.GetRequiredService<IMessageContextFactory>();

        // Pre-warm JIT caches
        HandlerActivator.PreWarmCache([typeof(PipelineBenchHandler)]);
        HandlerActivator.FreezeCache();

        // AOT path: same pipeline with AOT handler activator
        var aotServices = ConfigureServices(useAotActivator: true);
        _aotProvider = aotServices.BuildServiceProvider();
        _aotDispatcher = _aotProvider.GetRequiredService<IDispatcher>();
        _aotContextFactory = _aotProvider.GetRequiredService<IMessageContextFactory>();

        if (_jitProvider.GetRequiredService<IHandlerActivator>() is not HandlerActivator
            || _aotProvider.GetRequiredService<IHandlerActivator>() is not AotHandlerActivator
            || _jitProvider.GetRequiredService<IHandlerInvoker>().GetType() != _aotProvider.GetRequiredService<IHandlerInvoker>().GetType())
        {
            throw new InvalidOperationException("Activator comparison selected unexpected activation or invocation services.");
        }

        // Warm up both paths
        WarmUp(_jitDispatcher, _jitContextFactory).GetAwaiter().GetResult();
        WarmUp(_aotDispatcher, _aotContextFactory).GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_jitProvider as IDisposable)?.Dispose();
        (_aotProvider as IDisposable)?.Dispose();
    }

    /// <summary>
    /// Expression activator: Pipeline with 3 middleware layers (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    public Task<IMessageResult> ExpressionActivator_Pipeline3Middleware()
    {
        return DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
    }

    /// <summary>
    /// Container activator: Pipeline with 3 middleware layers.
    /// </summary>
    [Benchmark]
    public Task<IMessageResult> ContainerActivator_Pipeline3Middleware()
    {
        return DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
    }

    /// <summary>
    /// Expression activator: 50 sequential pipeline dispatches.
    /// </summary>
    [Benchmark]
    public async Task<int> ExpressionActivator_PipelineThroughput50()
    {
        var count = 0;
        for (var i = 0; i < 50; i++)
        {
            var result = await DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
            if (result.Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Container activator: 50 sequential pipeline dispatches.
    /// </summary>
    [Benchmark]
    public async Task<int> ContainerActivator_PipelineThroughput50()
    {
        var count = 0;
        for (var i = 0; i < 50; i++)
        {
            var result = await DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
            if (result.Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Expression activator: Mixed concurrent + sequential pipeline.
    /// </summary>
    [Benchmark]
    public async Task<int> ExpressionActivator_MixedWorkload()
    {
        var count = 0;

        // Sequential batch
        for (var i = 0; i < 25; i++)
        {
            var result = await DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
            if (result.Succeeded)
            {
                count++;
            }
        }

        // Concurrent batch
        var tasks = new Task<IMessageResult>[25];
        for (var i = 0; i < 25; i++)
        {
            tasks[i] = DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
        }

        var results = await Task.WhenAll(tasks);
        count += results.Count(r => r.Succeeded);

        return count;
    }

    /// <summary>
    /// Container activator: Mixed concurrent + sequential pipeline.
    /// </summary>
    [Benchmark]
    public async Task<int> ContainerActivator_MixedWorkload()
    {
        var count = 0;

        // Sequential batch
        for (var i = 0; i < 25; i++)
        {
            var result = await DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
            if (result.Succeeded)
            {
                count++;
            }
        }

        // Concurrent batch
        var tasks = new Task<IMessageResult>[25];
        for (var i = 0; i < 25; i++)
        {
            tasks[i] = DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
        }

        var results = await Task.WhenAll(tasks);
        count += results.Count(r => r.Succeeded);

        return count;
    }

    private static ServiceCollection ConfigureServices(bool useAotActivator)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddTransient<PipelineBenchHandler>();
        services.AddTransient<IActionHandler<PipelineBenchCommand>, PipelineBenchHandler>();
        services.AddDispatch(dispatch =>
        {
            dispatch.UseMiddleware<PreProcessMiddleware>();
            dispatch.UseMiddleware<ValidationMiddleware>();
            dispatch.UseMiddleware<PostProcessMiddleware>();
        });

        if (useAotActivator)
        {
            services.AddSingleton<IHandlerActivator, AotHandlerActivator>();
        }

        return services;
    }

    private static async Task<IMessageResult> DispatchAndReturnAsync(IDispatcher dispatcher,
        IMessageContextFactory factory, PipelineBenchCommand command, bool verifyExecution = false)
    {
        var context = factory.CreateContext();
        try
        {
            var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None).ConfigureAwait(false);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException(result.ErrorMessage ?? "Benchmark dispatch failed.");
            }
            if (verifyExecution)
            {
                if (!context.Items.ContainsKey("benchmark-handler"))
                {
                    throw new InvalidOperationException("Benchmark did not execute its handler.");
                }
                if (!context.Items.ContainsKey("benchmark-pre") || !context.Items.ContainsKey("benchmark-validation")
                    || !context.Items.ContainsKey("benchmark-post"))
                {
                    throw new InvalidOperationException("Benchmark did not execute all three middleware.");
                }
            }
            return result;
        }
        finally
        {
            factory.Return(context);
        }
    }

    private static async Task WarmUp(IDispatcher dispatcher, IMessageContextFactory contextFactory)
    {
        var cmd = new PipelineBenchCommand { OrderId = Guid.NewGuid(), Amount = 1.00m };
        for (var i = 0; i < 5; i++)
        {
            await DispatchAndReturnAsync(dispatcher, contextFactory, cmd, verifyExecution: true);
        }
    }

    // Benchmark types

    internal sealed record PipelineBenchCommand : IDispatchAction
    {
        public Guid OrderId { get; init; }
        public decimal Amount { get; init; }
    }

    internal sealed class PipelineBenchHandler : IActionHandler<PipelineBenchCommand>, IMessageContextAware
    {
        public IMessageContext? Context { get; set; }

        public void SetContext(IMessageContext context) => Context = context;

        public Task HandleAsync(PipelineBenchCommand command, CancellationToken cancellationToken)
        {
            var context = Context ?? throw new InvalidOperationException("Handler did not receive the message context.");
            context.Items["benchmark-handler"] = true;
            _ = command.OrderId;
            return Task.CompletedTask;
        }
    }

    internal sealed class PreProcessMiddleware : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
        public MessageKinds ApplicableMessageKinds => MessageKinds.All;

        public ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message,
            IMessageContext context,
            DispatchRequestDelegate nextDelegate,
            CancellationToken cancellationToken)
        {
            // Minimal pre-processing simulation
            _ = message.GetType().Name;
            context.Items["benchmark-pre"] = true;
            return nextDelegate(message, context, cancellationToken);
        }
    }

    internal sealed class ValidationMiddleware : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
        public MessageKinds ApplicableMessageKinds => MessageKinds.All;

        public ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message,
            IMessageContext context,
            DispatchRequestDelegate nextDelegate,
            CancellationToken cancellationToken)
        {
            context.Items["benchmark-validation"] = true;
            return nextDelegate(message, context, cancellationToken);
        }
    }

    internal sealed class PostProcessMiddleware : IDispatchMiddleware
    {
        public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;
        public MessageKinds ApplicableMessageKinds => MessageKinds.All;

        public ValueTask<IMessageResult> InvokeAsync(
            IDispatchMessage message,
            IMessageContext context,
            DispatchRequestDelegate nextDelegate,
            CancellationToken cancellationToken)
        {
            context.Items["benchmark-post"] = true;
            return nextDelegate(message, context, cancellationToken);
        }
    }
}