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
/// AOT vs JIT dispatch throughput benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Compares the AOT-safe dispatch path (source-generated handler activation, registry-based lookup)
/// against the JIT reflection path (expression-compiled delegates, generic type construction).
/// </para>
/// <para>
/// Phase D1 requirement R-D1: AOT-specific benchmarks for dispatch throughput.
/// Target: AOT path dispatch throughput >= 100% of JIT path.
/// </para>
/// </remarks>
[BenchmarkCategory("AOT")]
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
    /// JIT path: Full dispatch with expression-compiled handler activation (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    public Task<IMessageResult> JitPath_Dispatch()
    {
        var context = _jitContextFactory.CreateContext();
        return _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
    }

    /// <summary>
    /// AOT path: Full dispatch with service-provider handler activation.
    /// </summary>
    [Benchmark]
    public Task<IMessageResult> AotPath_Dispatch()
    {
        var context = _aotContextFactory.CreateContext();
        return _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
    }

    /// <summary>
    /// JIT path: 100 sequential dispatches (throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> JitPath_Throughput100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
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
    /// AOT path: 100 sequential dispatches (throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> AotPath_Throughput100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
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
    /// JIT path: 10 concurrent dispatches (parallel throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> JitPath_Concurrent10()
    {
        var tasks = new Task<IMessageResult>[10];
        for (var i = 0; i < 10; i++)
        {
            var context = _jitContextFactory.CreateContext();
            tasks[i] = _jitDispatcher.DispatchAsync(_command, context, CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);
        return results.Count(r => r.Succeeded);
    }

    /// <summary>
    /// AOT path: 10 concurrent dispatches (parallel throughput).
    /// </summary>
    [Benchmark]
    public async Task<int> AotPath_Concurrent10()
    {
        var tasks = new Task<IMessageResult>[10];
        for (var i = 0; i < 10; i++)
        {
            var context = _aotContextFactory.CreateContext();
            tasks[i] = _aotDispatcher.DispatchAsync(_command, context, CancellationToken.None);
        }

        var results = await Task.WhenAll(tasks);
        return results.Count(r => r.Succeeded);
    }

    private static async Task WarmUp(IDispatcher dispatcher, IMessageContextFactory contextFactory)
    {
        var cmd = new BenchmarkCommand { OrderId = Guid.NewGuid(), CustomerId = "warmup" };
        for (var i = 0; i < 5; i++)
        {
            var ctx = contextFactory.CreateContext();
            await dispatcher.DispatchAsync(cmd, ctx, CancellationToken.None);
        }
    }

    // Benchmark message types

    internal sealed record BenchmarkCommand : IDispatchAction
    {
        public Guid OrderId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
    }

    internal sealed class BenchmarkCommandHandler : IActionHandler<BenchmarkCommand>
    {
        public IMessageContext? Context { get; set; }

        public Task HandleAsync(BenchmarkCommand command, CancellationToken cancellationToken)
        {
            _ = command.OrderId;
            return Task.CompletedTask;
        }
    }
}