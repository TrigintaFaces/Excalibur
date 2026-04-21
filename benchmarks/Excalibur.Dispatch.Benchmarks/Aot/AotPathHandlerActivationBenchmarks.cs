// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Aot;

/// <summary>
/// AOT vs JIT handler activation benchmarks.
/// </summary>
/// <remarks>
/// <para>
/// Compares the JIT path (expression-compiled delegates via <see cref="HandlerActivator"/>)
/// against the AOT path (service-provider resolution via <see cref="AotHandlerActivator"/>).
/// </para>
/// <para>
/// Phase D1 requirement R-D1: AOT-specific benchmarks for handler activation.
/// Target: AOT path DI/activation throughput >= 98% of JIT path.
/// </para>
/// </remarks>
[BenchmarkCategory("AOT")]
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AotPathHandlerActivationBenchmarks
{
    private IServiceProvider _jitProvider = null!;
    private IServiceProvider _aotProvider = null!;
    private IHandlerActivator _jitActivator = null!;
    private IHandlerActivator _aotActivator = null!;
    private IMessageContext _jitContext = null!;
    private IMessageContext _aotContext = null!;
    private Type _handlerType = null!;

    [GlobalSetup]
    public void Setup()
    {
        _handlerType = typeof(ActivationBenchHandler);

        // JIT path: expression-compiled handler activator
        var jitServices = new ServiceCollection();
        jitServices.AddLogging();
        jitServices.AddTransient<ActivationBenchHandler>();
        jitServices.AddTransient<IActionHandler<ActivationBenchCommand>, ActivationBenchHandler>();
        jitServices.AddDispatch();

        _jitProvider = jitServices.BuildServiceProvider();
        _jitActivator = _jitProvider.GetRequiredService<IHandlerActivator>();
        _jitContext = _jitProvider.GetRequiredService<IMessageContextFactory>().CreateContext();

        // Pre-warm and freeze JIT caches
        HandlerActivator.PreWarmCache([_handlerType]);
        HandlerActivator.FreezeCache();

        // AOT path: service-provider handler activator
        var aotServices = new ServiceCollection();
        aotServices.AddLogging();
        aotServices.AddTransient<ActivationBenchHandler>();
        aotServices.AddTransient<IActionHandler<ActivationBenchCommand>, ActivationBenchHandler>();
        aotServices.AddDispatch();
        aotServices.AddSingleton<IHandlerActivator, AotHandlerActivator>();

        _aotProvider = aotServices.BuildServiceProvider();
        _aotActivator = _aotProvider.GetRequiredService<IHandlerActivator>();
        _aotContext = _aotProvider.GetRequiredService<IMessageContextFactory>().CreateContext();

        // Warm up both paths
        for (var i = 0; i < 10; i++)
        {
            _jitActivator.ActivateHandler(_handlerType, _jitContext, _jitProvider);
            _aotActivator.ActivateHandler(_handlerType, _aotContext, _aotProvider);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        (_jitProvider as IDisposable)?.Dispose();
        (_aotProvider as IDisposable)?.Dispose();
    }

    /// <summary>
    /// JIT path: Single handler activation with cached expression delegates (baseline).
    /// </summary>
    [Benchmark(Baseline = true)]
    public object JitPath_ActivateHandler()
    {
        return _jitActivator.ActivateHandler(_handlerType, _jitContext, _jitProvider);
    }

    /// <summary>
    /// AOT path: Single handler activation via service provider.
    /// </summary>
    [Benchmark]
    public object AotPath_ActivateHandler()
    {
        return _aotActivator.ActivateHandler(_handlerType, _aotContext, _aotProvider);
    }

    /// <summary>
    /// JIT path: 100 handler activations (batch throughput).
    /// </summary>
    [Benchmark]
    public int JitPath_ActivateBatch100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            var handler = _jitActivator.ActivateHandler(_handlerType, _jitContext, _jitProvider);
            if (handler != null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// AOT path: 100 handler activations (batch throughput).
    /// </summary>
    [Benchmark]
    public int AotPath_ActivateBatch100()
    {
        var count = 0;
        for (var i = 0; i < 100; i++)
        {
            var handler = _aotActivator.ActivateHandler(_handlerType, _aotContext, _aotProvider);
            if (handler != null)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// JIT path: Concurrent handler activations (thread-safety + throughput).
    /// </summary>
    [Benchmark]
    public int JitPath_ConcurrentActivation()
    {
        var count = 0;
        Parallel.For(0, 10, _ =>
        {
            var handler = _jitActivator.ActivateHandler(_handlerType, _jitContext, _jitProvider);
            if (handler != null)
            {
                Interlocked.Increment(ref count);
            }
        });
        return count;
    }

    /// <summary>
    /// AOT path: Concurrent handler activations (thread-safety + throughput).
    /// </summary>
    [Benchmark]
    public int AotPath_ConcurrentActivation()
    {
        var count = 0;
        Parallel.For(0, 10, _ =>
        {
            var handler = _aotActivator.ActivateHandler(_handlerType, _aotContext, _aotProvider);
            if (handler != null)
            {
                Interlocked.Increment(ref count);
            }
        });
        return count;
    }

    // Benchmark types

    internal sealed record ActivationBenchCommand : IDispatchAction
    {
        public Guid Id { get; init; }
    }

    internal sealed class ActivationBenchHandler : IActionHandler<ActivationBenchCommand>
    {
        public IMessageContext? Context { get; set; }

        public Task HandleAsync(ActivationBenchCommand command, CancellationToken cancellationToken)
        {
            _ = command.Id;
            return Task.CompletedTask;
        }
    }
}
