// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;

using NServiceBus;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Comparative benchmarks: Excalibur vs NServiceBus.
/// Measures relative performance across handler invocation, pipeline overhead, memory allocations, and concurrency.
/// </summary>
/// <remarks>
/// Sprint 814 - NServiceBus Comparison Enhancement.
/// bd-g4o754: Add NServiceBus to comparative benchmark suite.
///
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - NServiceBus: 10.1.3 (latest stable)
///
/// NServiceBus is configured with LearningTransport for in-process benchmarking.
/// SendLocal is used for command dispatch (message stays in same endpoint).
///
/// Benchmark Categories:
/// 1. Handler Invocation (hot path) - Basic dispatch performance
/// 2. Notification to multiple handlers (Dispatch events vs NServiceBus pub/sub)
/// 3. Query with return value (Dispatch query vs NServiceBus callback pattern)
/// 4. 10 concurrent commands (Dispatch/NServiceBus)
/// 5. 100 concurrent commands (Dispatch/NServiceBus)
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class NServiceBusComparisonBenchmarks
{
    // Excalibur infrastructure
    private IServiceProvider? _dispatchServiceProvider;
    private IDispatcher? _dispatcher;
    private IMessageContextFactory? _dispatchContextFactory;
    private IServiceProvider? _dispatchDirectServiceProvider;
    private IDispatcher? _directDispatcher;
    private IDirectLocalDispatcher? _directLocalDispatcher;
    private IMessageContextFactory? _directContextFactory;

    // Singleton-promoted infrastructure
    private IServiceProvider? _singletonServiceProvider;
    private IDirectLocalDispatcher? _singletonDirectLocalDispatcher;

    // NServiceBus infrastructure
    private IEndpointInstance? _endpointInstance;
    private string? _learningTransportPath;

    /// <summary>
    /// Initialize both Dispatch and NServiceBus endpoints before benchmarks.
    /// </summary>
    [GlobalSetup]
    public async Task GlobalSetup()
    {
        // Setup Excalibur default local path
        var dispatchServices = new ServiceCollection();
        _ = dispatchServices.AddLogging();
        _ = dispatchServices.AddDispatch();
        _ = dispatchServices.AddTransient<NsbDispatchTestCommandHandler>();
        _ = dispatchServices.AddTransient<NsbDispatchTestQueryHandler>();
        _ = dispatchServices.AddTransient<NsbDispatchTestNotificationHandler1>();
        _ = dispatchServices.AddTransient<NsbDispatchTestNotificationHandler2>();
        _ = dispatchServices.AddTransient<NsbDispatchTestNotificationHandler3>();
        _ = dispatchServices.AddTransient<IActionHandler<NsbTestCommand>, NsbDispatchTestCommandHandler>();
        _ = dispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler1>();
        _ = dispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler2>();
        _ = dispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler3>();
        _ = dispatchServices.AddTransient<IActionHandler<NsbTestQuery, int>, NsbDispatchTestQueryHandler>();

        _dispatchServiceProvider = dispatchServices.BuildServiceProvider();
        _dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
        _dispatchContextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

        // Setup Excalibur strict direct-local profile
        var directDispatchServices = new ServiceCollection();
        _ = directDispatchServices.AddLogging();
        _ = directDispatchServices.AddDispatch(builder =>
        {
            _ = builder.ConfigurePipeline("DirectLocal", pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Direct));
            _ = builder.WithOptions(options =>
            {
                options.UseLightMode = true;
                options.EnablePipelineSynthesis = false;
                options.Features.EnableCacheMiddleware = false;
                options.Features.EnableMetrics = false;
                options.Features.EnableAuthorization = false;
                options.Features.ValidateMessageSchemas = false;
                options.Features.EnableVersioning = false;
                options.Features.EnableMultiTenancy = false;
                options.Features.EnableTransactions = false;
            });
        });
        _ = directDispatchServices.AddTransient<NsbDispatchTestCommandHandler>();
        _ = directDispatchServices.AddTransient<NsbDispatchTestQueryHandler>();
        _ = directDispatchServices.AddTransient<NsbDispatchTestNotificationHandler1>();
        _ = directDispatchServices.AddTransient<NsbDispatchTestNotificationHandler2>();
        _ = directDispatchServices.AddTransient<NsbDispatchTestNotificationHandler3>();
        _ = directDispatchServices.AddTransient<IActionHandler<NsbTestCommand>, NsbDispatchTestCommandHandler>();
        _ = directDispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler1>();
        _ = directDispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler2>();
        _ = directDispatchServices.AddTransient<IEventHandler<NsbTestNotification>, NsbDispatchTestNotificationHandler3>();
        _ = directDispatchServices.AddTransient<IActionHandler<NsbTestQuery, int>, NsbDispatchTestQueryHandler>();

        _dispatchDirectServiceProvider = directDispatchServices.BuildServiceProvider();
        _directDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>();
        _directLocalDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;
        _directContextFactory = _dispatchDirectServiceProvider.GetRequiredService<IMessageContextFactory>();

        // Setup Excalibur singleton-promoted path
        var singletonServices = new ServiceCollection();
        _ = singletonServices.AddLogging();
        _ = singletonServices.AddDispatch(builder =>
        {
            _ = builder.ConfigurePipeline("DirectLocal", pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Direct));
            _ = builder.WithOptions(options =>
            {
                options.UseLightMode = true;
                options.EnablePipelineSynthesis = false;
                options.Features.EnableCacheMiddleware = false;
                options.Features.EnableMetrics = false;
                options.Features.EnableAuthorization = false;
                options.Features.ValidateMessageSchemas = false;
                options.Features.EnableVersioning = false;
                options.Features.EnableMultiTenancy = false;
                options.Features.EnableTransactions = false;
                options.CrossCutting.Performance.AutoPromoteStatelessHandlersToSingleton = true;
            });
        });
        _ = singletonServices.AddTransient<NsbDispatchTestCommandHandler>();
        _ = singletonServices.AddTransient<NsbDispatchTestQueryHandler>();
        _ = singletonServices.AddTransient<IActionHandler<NsbTestCommand>, NsbDispatchTestCommandHandler>();
        _ = singletonServices.AddTransient<IActionHandler<NsbTestQuery, int>, NsbDispatchTestQueryHandler>();

        _singletonServiceProvider = singletonServices.BuildServiceProvider();
        _singletonDirectLocalDispatcher = _singletonServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;

        // Setup NServiceBus with LearningTransport (in-process, no real transport)
        _learningTransportPath = Path.Combine(Path.GetTempPath(), $"nsb-bench-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_learningTransportPath);

        var endpointConfig = new EndpointConfiguration("ExcaliburBenchmarks");
        endpointConfig.UseSerialization<SystemJsonSerializer>();
        var transport = endpointConfig.UseTransport<LearningTransport>();
        transport.StorageDirectory(_learningTransportPath);

        endpointConfig.UsePersistence<LearningPersistence>();
        endpointConfig.SendFailedMessagesTo("error");
        endpointConfig.AuditProcessedMessagesTo("audit");
        endpointConfig.EnableInstallers();

        // Disable unnecessary features for benchmark fairness
        endpointConfig.Recoverability().Delayed(delayed => delayed.NumberOfRetries(0));
        endpointConfig.Recoverability().Immediate(immediate => immediate.NumberOfRetries(0));

        // Route commands to self (SendLocal pattern)
        var routing = transport.Routing();
        routing.RouteToEndpoint(typeof(NsbCommand), "ExcaliburBenchmarks");

        _endpointInstance = await Endpoint.Start(endpointConfig).ConfigureAwait(false);

        // Warm and freeze Dispatch caches
        WarmupAndFreezeDispatchCaches();
    }

    /// <summary>
    /// Cleanup service providers and NServiceBus endpoint after benchmarks.
    /// </summary>
    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        if (_endpointInstance is not null)
        {
            await _endpointInstance.Stop().ConfigureAwait(false);
        }

        if (_dispatchServiceProvider is IDisposable dispatchDisposable)
        {
            dispatchDisposable.Dispose();
        }

        if (_dispatchDirectServiceProvider is IDisposable directDispatchDisposable)
        {
            directDispatchDisposable.Dispose();
        }

        if (_singletonServiceProvider is IDisposable singletonDisposable)
        {
            singletonDisposable.Dispose();
        }

        // Clean up learning transport temp directory
        if (_learningTransportPath is not null && Directory.Exists(_learningTransportPath))
        {
            try
            {
                Directory.Delete(_learningTransportPath, recursive: true);
            }
            catch
            {
                // Best-effort cleanup; temp directory will be cleaned by OS eventually
            }
        }
    }

    // ============================================================================
    // CATEGORY 1: Handler Invocation (Hot Path)
    // ============================================================================

    /// <summary>
    /// Baseline: Excalibur.Dispatch single command handler invocation.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Dispatch: Single command handler")]
    public async Task<IMessageResult> Dispatch_SingleCommandHandler()
    {
        var command = new NsbTestCommand { Value = 42 };
        return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command).ConfigureAwait(false);
    }

    /// <summary>
    /// Excalibur.Dispatch strict direct-local profile (explicit no-middleware profile).
    /// </summary>
    [Benchmark(Description = "Dispatch: Single command strict direct-local")]
    public async Task<IMessageResult> Dispatch_SingleCommand_StrictDirectLocal()
    {
        var command = new NsbTestCommand { Value = 42 };
        return await DispatchWithFreshContextAsync(_directDispatcher, _directContextFactory, command).ConfigureAwait(false);
    }

    /// <summary>
    /// Excalibur.Dispatch ultra-local API path (ValueTask, no IMessageResult materialization on success).
    /// </summary>
    [Benchmark(Description = "Dispatch: Single command ultra-local API")]
    public async Task Dispatch_SingleCommand_UltraLocalApi()
    {
        var command = new NsbTestCommand { Value = 42 };
        await DispatchUltraLocalAsync(_directLocalDispatcher, command).ConfigureAwait(false);
    }

    /// <summary>
    /// NServiceBus: Single command handler invocation via SendLocal.
    /// </summary>
    [Benchmark(Description = "NServiceBus: Single command handler (SendLocal)")]
    public async Task NServiceBus_SingleCommandHandler()
    {
        var command = new NsbCommand { Value = 42 };
        await _endpointInstance!.SendLocal(command).ConfigureAwait(false);
    }

    /// <summary>
    /// Baseline: Excalibur.Dispatch notification to multiple handlers (1 notification -> 3 handlers).
    /// </summary>
    [Benchmark(Description = "Dispatch: Notification to 3 handlers")]
    public async Task<IMessageResult> Dispatch_NotificationMultipleHandlers()
    {
        var notification = new NsbTestNotification { Message = "test" };
        return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, notification).ConfigureAwait(false);
    }

    /// <summary>
    /// NServiceBus: Publish event to multiple handlers.
    /// </summary>
    [Benchmark(Description = "NServiceBus: Publish to 3 handlers")]
    public async Task NServiceBus_NotificationMultipleHandlers()
    {
        var evt = new NsbEvent { Message = "test" };
        await _endpointInstance!.Publish(evt).ConfigureAwait(false);
    }

    /// <summary>
    /// Baseline: Excalibur.Dispatch query with return value.
    /// </summary>
    [Benchmark(Description = "Dispatch: Query with return value")]
    public async Task<IMessageResult> Dispatch_QueryWithReturnValue()
    {
        var query = new NsbTestQuery { Id = 123 };
        return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Excalibur.Dispatch query via strict direct-local profile.
    /// </summary>
    [Benchmark(Description = "Dispatch: Query strict direct-local")]
    public async Task<IMessageResult> Dispatch_QueryWithReturnValue_StrictDirectLocal()
    {
        var query = new NsbTestQuery { Id = 123 };
        return await DispatchWithFreshContextAsync(_directDispatcher, _directContextFactory, query).ConfigureAwait(false);
    }

    /// <summary>
    /// Excalibur.Dispatch ultra-local query path.
    /// </summary>
    [Benchmark(Description = "Dispatch: Query ultra-local API")]
    public async Task<int?> Dispatch_QueryWithReturnValue_UltraLocalApi()
    {
        var query = new NsbTestQuery { Id = 123 };
        return await DispatchUltraLocalWithResponseAsync<NsbTestQuery, int>(_directLocalDispatcher, query).ConfigureAwait(false);
    }

    // ============================================================================
    // CATEGORY 2: Optimization Variants
    // ============================================================================

    /// <summary>
    /// Excalibur.Dispatch with auto-promoted singleton handlers.
    /// </summary>
    [Benchmark(Description = "Dispatch: Ultra-local singleton-promoted")]
    public async Task Dispatch_SingleCommand_SingletonPromoted()
    {
        var command = new NsbTestCommand { Value = 42 };
        await DispatchUltraLocalAsync(_singletonDirectLocalDispatcher, command).ConfigureAwait(false);
    }

    /// <summary>
    /// Excalibur.Dispatch with auto-promoted singleton handlers (query with response).
    /// </summary>
    [Benchmark(Description = "Dispatch: Query singleton-promoted")]
    public async Task<int?> Dispatch_Query_SingletonPromoted()
    {
        var query = new NsbTestQuery { Id = 123 };
        return await DispatchUltraLocalWithResponseAsync<NsbTestQuery, int>(_singletonDirectLocalDispatcher, query).ConfigureAwait(false);
    }

    // ============================================================================
    // CATEGORY 3: Concurrent Operations
    // ============================================================================

    /// <summary>
    /// Baseline: Excalibur.Dispatch 10 concurrent command dispatches.
    /// </summary>
    [Benchmark(Description = "Dispatch: 10 concurrent commands")]
    public async Task Dispatch_ConcurrentCommands10()
    {
        var tasks = new Task<IMessageResult>[10];
        for (int i = 0; i < 10; i++)
        {
            var command = new NsbTestCommand { Value = i };
            tasks[i] = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command);
        }

        _ = await Task.WhenAll(tasks);
    }

    /// <summary>
    /// NServiceBus: 10 concurrent command dispatches via SendLocal.
    /// </summary>
    [Benchmark(Description = "NServiceBus: 10 concurrent commands")]
    public async Task NServiceBus_ConcurrentCommands10()
    {
        var tasks = new Task[10];
        for (int i = 0; i < 10; i++)
        {
            var command = new NsbCommand { Value = i };
            tasks[i] = _endpointInstance!.SendLocal(command);
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Baseline: Excalibur.Dispatch 100 concurrent command dispatches.
    /// </summary>
    [Benchmark(Description = "Dispatch: 100 concurrent commands")]
    public async Task Dispatch_ConcurrentCommands100()
    {
        var tasks = new Task<IMessageResult>[100];
        for (int i = 0; i < 100; i++)
        {
            var command = new NsbTestCommand { Value = i };
            tasks[i] = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command);
        }

        _ = await Task.WhenAll(tasks);
    }

    /// <summary>
    /// NServiceBus: 100 concurrent command dispatches via SendLocal.
    /// </summary>
    [Benchmark(Description = "NServiceBus: 100 concurrent commands")]
    public async Task NServiceBus_ConcurrentCommands100()
    {
        var tasks = new Task[100];
        for (int i = 0; i < 100; i++)
        {
            var command = new NsbCommand { Value = i };
            tasks[i] = _endpointInstance!.SendLocal(command);
        }

        await Task.WhenAll(tasks);
    }

    private void WarmupAndFreezeDispatchCaches()
    {
        _ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new NsbTestCommand { Value = 1 })
            .GetAwaiter().GetResult();
        _ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new NsbTestNotification { Message = "warmup" })
            .GetAwaiter().GetResult();
        _ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new NsbTestQuery { Id = 1 })
            .GetAwaiter().GetResult();

        // Warmup singleton-promoted path
        if (_singletonDirectLocalDispatcher is not null)
        {
            DispatchUltraLocalAsync(_singletonDirectLocalDispatcher, new NsbTestCommand { Value = 1 })
                .AsTask().GetAwaiter().GetResult();
            DispatchUltraLocalWithResponseAsync<NsbTestQuery, int>(_singletonDirectLocalDispatcher, new NsbTestQuery { Id = 1 })
                .AsTask().GetAwaiter().GetResult();
        }

        HandlerInvoker.FreezeCache();
        HandlerInvokerRegistry.FreezeCache();
        HandlerActivator.FreezeCache();
        FinalDispatchHandler.FreezeResultFactoryCache();
        MiddlewareApplicabilityEvaluator.FreezeCache();
    }

    private static async Task<IMessageResult> DispatchWithFreshContextAsync<TMessage>(
        IDispatcher? dispatcher,
        IMessageContextFactory? contextFactory,
        TMessage message)
        where TMessage : IDispatchMessage
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var context = contextFactory.CreateContext();
        var dispatchTask = dispatcher.DispatchAsync(message, context, CancellationToken.None);
        if (dispatchTask.IsCompletedSuccessfully)
        {
            try
            {
                return dispatchTask.Result;
            }
            finally
            {
                contextFactory.Return(context);
            }
        }

        return await AwaitAndReturnContextAsync(dispatchTask, contextFactory, context).ConfigureAwait(false);
    }

    private static async Task<IMessageResult> AwaitAndReturnContextAsync(
        Task<IMessageResult> dispatchTask,
        IMessageContextFactory contextFactory,
        IMessageContext context)
    {
        try
        {
            return await dispatchTask.ConfigureAwait(false);
        }
        finally
        {
            contextFactory.Return(context);
        }
    }

    private static ValueTask DispatchUltraLocalAsync<TMessage>(
        IDirectLocalDispatcher? dispatcher,
        TMessage message)
        where TMessage : IDispatchAction
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchLocalAsync(message, CancellationToken.None);
    }

    private static ValueTask<TResponse?> DispatchUltraLocalWithResponseAsync<TMessage, TResponse>(
        IDirectLocalDispatcher? dispatcher,
        TMessage message)
        where TMessage : IDispatchAction<TResponse>
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        return dispatcher.DispatchLocalAsync<TMessage, TResponse>(message, CancellationToken.None);
    }
}

// ============================================================================
// Test Messages and Handlers (Excalibur - NServiceBus comparison specific)
// ============================================================================

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Test command for Dispatch benchmarks (NServiceBus comparison).
/// </summary>
public record NsbTestCommand : IDispatchAction
{
    public int Value { get; init; }
}

/// <summary>
/// Handler for NsbTestCommand (Dispatch).
/// </summary>
public class NsbDispatchTestCommandHandler : IActionHandler<NsbTestCommand>
{
    public Task HandleAsync(NsbTestCommand message, CancellationToken cancellationToken)
    {
        _ = message.Value * 2;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Test notification for Dispatch benchmarks (NServiceBus comparison).
/// </summary>
public record NsbTestNotification : IDispatchEvent
{
    public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler 1 for NsbTestNotification (Dispatch).
/// </summary>
public class NsbDispatchTestNotificationHandler1 : IEventHandler<NsbTestNotification>
{
    public Task HandleAsync(NsbTestNotification message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handler 2 for NsbTestNotification (Dispatch).
/// </summary>
public class NsbDispatchTestNotificationHandler2 : IEventHandler<NsbTestNotification>
{
    public Task HandleAsync(NsbTestNotification message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Handler 3 for NsbTestNotification (Dispatch).
/// </summary>
public class NsbDispatchTestNotificationHandler3 : IEventHandler<NsbTestNotification>
{
    public Task HandleAsync(NsbTestNotification message, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>
/// Test query for Dispatch benchmarks (NServiceBus comparison).
/// </summary>
public record NsbTestQuery : IDispatchAction<int>
{
    public int Id { get; init; }
}

/// <summary>
/// Handler for NsbTestQuery (Dispatch).
/// </summary>
public class NsbDispatchTestQueryHandler : IActionHandler<NsbTestQuery, int>
{
    public Task<int> HandleAsync(NsbTestQuery message, CancellationToken cancellationToken)
    {
        var result = message.Id * 2;
        return Task.FromResult(result);
    }
}

// ============================================================================
// Test Messages and Handlers (NServiceBus)
// ============================================================================

/// <summary>
/// NServiceBus command message for benchmarks.
/// </summary>
public class NsbCommand : NServiceBus.ICommand
{
    public int Value { get; set; }
}

/// <summary>
/// NServiceBus event message for benchmarks.
/// </summary>
public class NsbEvent : NServiceBus.IEvent
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// NServiceBus command handler for benchmarks.
/// </summary>
public class NsbCommandHandler : NServiceBus.IHandleMessages<NsbCommand>
{
    public Task Handle(NsbCommand message, NServiceBus.IMessageHandlerContext context)
    {
        _ = message.Value * 2;
        return Task.CompletedTask;
    }
}

/// <summary>
/// NServiceBus event handler 1 for benchmarks.
/// </summary>
public class NsbEventHandler1 : NServiceBus.IHandleMessages<NsbEvent>
{
    public Task Handle(NsbEvent message, NServiceBus.IMessageHandlerContext context) => Task.CompletedTask;
}

/// <summary>
/// NServiceBus event handler 2 for benchmarks.
/// </summary>
public class NsbEventHandler2 : NServiceBus.IHandleMessages<NsbEvent>
{
    public Task Handle(NsbEvent message, NServiceBus.IMessageHandlerContext context) => Task.CompletedTask;
}

/// <summary>
/// NServiceBus event handler 3 for benchmarks.
/// </summary>
public class NsbEventHandler3 : NServiceBus.IHandleMessages<NsbEvent>
{
    public Task Handle(NsbEvent message, NServiceBus.IMessageHandlerContext context) => Task.CompletedTask;
}

#pragma warning restore SA1402 // File may only contain a single type