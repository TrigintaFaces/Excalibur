// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
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
public class AotPathDispatchBenchmarks
{
    private IServiceProvider _jitProvider = null!;
    private IServiceProvider _aotProvider = null!;
    private IDispatcher _jitDispatcher = null!;
    private IDispatcher _aotDispatcher = null!;
    private IMessageContextFactory _jitContextFactory = null!;
    private IMessageContextFactory _aotContextFactory = null!;
    private BenchmarkCommand _command = null!;

    [GlobalSetup]
    public void Setup()
    {
        _command = new BenchmarkCommand { OrderId = Guid.NewGuid(), CustomerId = "bench-customer-001" };

        // JIT path: standard dispatch with expression-compiled handler activator
        var jitServices = new ServiceCollection();
        jitServices.AddLogging();
        jitServices.AddTransient<BenchmarkCommandHandler>();
        jitServices.AddTransient<IActionHandler<BenchmarkCommand>, BenchmarkCommandHandler>();
        jitServices.AddDispatch();

        _jitProvider = jitServices.BuildServiceProvider();
        _jitDispatcher = _jitProvider.GetRequiredService<IDispatcher>();
        _jitContextFactory = _jitProvider.GetRequiredService<IMessageContextFactory>();

        // Pre-warm JIT caches
        HandlerActivator.PreWarmCache([typeof(BenchmarkCommandHandler)]);
        HandlerActivator.FreezeCache();

        // AOT path: dispatch with AOT handler activator (service-provider based, no expression compilation)
        var aotServices = new ServiceCollection();
        aotServices.AddLogging();
        aotServices.AddTransient<BenchmarkCommandHandler>();
        aotServices.AddTransient<IActionHandler<BenchmarkCommand>, BenchmarkCommandHandler>();
        aotServices.AddDispatch();

        // Replace the handler activator with the AOT variant
        aotServices.AddSingleton<IHandlerActivator, AotHandlerActivator>();

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
    /// Expression activator: Full dispatch with expression-compiled handler activation (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    public Task<IMessageResult> ExpressionActivator_Dispatch()
    {
        return DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
    }

    /// <summary>
    /// Container activator: Full dispatch with service-provider handler activation.
    /// </summary>
    [Benchmark]
    public Task<IMessageResult> ContainerActivator_Dispatch()
    {
        return DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
    }

    /// <summary>
    /// Expression activator: 100 sequential dispatches (throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> ExpressionActivator_Throughput100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
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
    /// Container activator: 100 sequential dispatches (throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> ContainerActivator_Throughput100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
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
    /// Expression activator: 10 concurrent dispatches (parallel throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> ExpressionActivator_Concurrent10()
    {
        var tasks = new Task<IMessageResult>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = DispatchAndReturnAsync(_jitDispatcher, _jitContextFactory, _command);
        }

        var results = await Task.WhenAll(tasks);
        return results.Count(r => r.Succeeded);
    }

    /// <summary>
    /// Container activator: 10 concurrent dispatches (parallel throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> ContainerActivator_Concurrent10()
    {
        var tasks = new Task<IMessageResult>[10];
        for (var i = 0; i < 10; i++)
        {
            tasks[i] = DispatchAndReturnAsync(_aotDispatcher, _aotContextFactory, _command);
        }

        var results = await Task.WhenAll(tasks);
        return results.Count(r => r.Succeeded);
    }

    private static async Task<IMessageResult> DispatchAndReturnAsync(IDispatcher dispatcher,
        IMessageContextFactory factory, BenchmarkCommand command, bool verifyExecution = false)
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
        var cmd = new BenchmarkCommand { OrderId = Guid.NewGuid(), CustomerId = "warmup" };
        for (var i = 0; i < 5; i++)
        {
            await DispatchAndReturnAsync(dispatcher, contextFactory, cmd, verifyExecution: true);
        }
    }

    // Benchmark message types

    internal sealed record BenchmarkCommand : IDispatchAction
    {
        public Guid OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
    }

    internal sealed class BenchmarkCommandHandler : IActionHandler<BenchmarkCommand>, IMessageContextAware
    {
        public IMessageContext? Context { get; set; }

        public void SetContext(IMessageContext context) => Context = context;

        public Task HandleAsync(BenchmarkCommand command, CancellationToken cancellationToken)
        {
            var context = Context ?? throw new InvalidOperationException("Handler did not receive the message context.");
            context.Items["benchmark-handler"] = true;
            _ = command.OrderId;
            return Task.CompletedTask;
        }
    }
}