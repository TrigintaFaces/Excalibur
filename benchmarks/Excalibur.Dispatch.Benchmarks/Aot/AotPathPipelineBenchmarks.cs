// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Aot;

/// <summary>
/// AOT vs JIT pipeline orchestration benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Compares full pipeline dispatch (middleware chain + handler activation + invocation)
/// through both the JIT path (expression-compiled delegates) and AOT path
/// (service-provider resolution). Measures end-to-end pipeline overhead.
/// </para>
/// <para>
/// Phase D1 requirement R-D1: AOT-specific benchmarks for pipeline orchestration.
/// Target: AOT path pipeline throughput >= 90% of JIT path.
/// </para>
/// </remarks>
[BenchmarkCategory("AOT")]
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
    /// JIT path: Pipeline with 3 middleware layers (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    public Task<IMessageResult> JitPath_Pipeline3Middleware()
    {
        var context = _jitContextFactory.CreateContext();
        return _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
    }

    /// <summary>
    /// AOT path: Pipeline with 3 middleware layers.
    /// </summary>
    [Benchmark]
    public Task<IMessageResult> AotPath_Pipeline3Middleware()
    {
        var context = _aotContextFactory.CreateContext();
        return _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
    }

    /// <summary>
    /// JIT path: 50 sequential pipeline dispatches.
    /// </summary>
    [Benchmark]
    public async Task<int> JitPath_PipelineThroughput50()
    {
        var count = 0;
        for (var i = 0; i < 50; i++)
        {
            var context = _jitContextFactory.CreateContext();
            var result = await _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
            if (result.Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// AOT path: 50 sequential pipeline dispatches.
    /// </summary>
    [Benchmark]
    public async Task<int> AotPath_PipelineThroughput50()
    {
        var count = 0;
        for (var i = 0; i < 50; i++)
        {
            var context = _aotContextFactory.CreateContext();
            var result = await _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
            if (result.Succeeded)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// JIT path: Mixed concurrent + sequential pipeline.
    /// </summary>
    [Benchmark]
    public async Task<int> JitPath_MixedWorkload()
    {
        var count = 0;

        // Sequential batch
        for (var i = 0; i < 25; i++)
        {
            var context = _jitContextFactory.CreateContext();
            var result = await _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
            if (result.Succeeded)
            {
                count++;
            }
        }

        // Concurrent batch
        var tasks = new Task<IMessageResult>[25];
        for (var i = 0; i < 25; i++)
        {
            var context = _jitContextFactory.CreateContext();
            tasks[i] = _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);
        count += results.Count(r => r.Succeeded);

        return count;
    }

    /// <summary>
    /// AOT path: Mixed concurrent + sequential pipeline.
    /// </summary>
    [Benchmark]
    public async Task<int> AotPath_MixedWorkload()
    {
        var count = 0;

        // Sequential batch
        for (var i = 0; i < 25; i++)
        {
            var context = _aotContextFactory.CreateContext();
            var result = await _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
            if (result.Succeeded)
            {
                count++;
            }
        }

        // Concurrent batch
        var tasks = new Task<IMessageResult>[25];
        for (var i = 0; i < 25; i++)
        {
            var context = _aotContextFactory.CreateContext();
            tasks[i] = _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
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
        services.AddDispatch();

        // Add representative middleware stack (3 layers)
        services.AddMiddleware<PreProcessMiddleware>();
        services.AddMiddleware<ValidationMiddleware>();
        services.AddMiddleware<PostProcessMiddleware>();

        if (useAotActivator)
        {
            services.AddSingleton<IHandlerActivator, AotHandlerActivator>();
        }

        return services;
    }

    private static async Task WarmUp(IDispatcher dispatcher, IMessageContextFactory contextFactory)
    {
        var cmd = new PipelineBenchCommand { OrderId = Guid.NewGuid(), Amount = 1.00m };
        for (var i = 0; i < 5; i++)
        {
            var ctx = contextFactory.CreateContext();
            await dispatcher.DispatchAsync(cmd, ctx, CancellationToken.None);
        }
    }

    // Benchmark types

    internal sealed record PipelineBenchCommand : IDispatchAction
    {
        public Guid OrderId { get; init; }
        public decimal Amount { get; init; }
    }

    internal sealed class PipelineBenchHandler : IActionHandler<PipelineBenchCommand>
    {
        public IMessageContext? Context { get; set; }

        public Task HandleAsync(PipelineBenchCommand command, CancellationToken cancellationToken)
        {
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
            return nextDelegate(message, context, cancellationToken);
        }
    }
}