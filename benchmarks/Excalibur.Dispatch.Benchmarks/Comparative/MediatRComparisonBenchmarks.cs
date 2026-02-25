// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Comparative benchmarks: Excalibur vs MediatR.
/// Measures relative performance across handler invocation, pipeline overhead, memory allocations, and concurrency.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-924kc: MediatR Comparison Enhancement (10 scenarios) - COMPLETE.
///
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - MediatR: 13.0+ (latest stable)
///
/// Benchmark Categories:
/// 1. Handler Invocation (hot path) - Basic dispatch performance
/// 2. Pipeline Overhead - Cost of middleware/behaviors
/// 3. Memory Allocations - Gen0/Gen1 collections, bytes allocated
/// 4. Concurrent Operations - Scalability under load
///
/// Scenarios (10 total):
/// 1-2. Single command handler (Dispatch/MediatR baseline)
/// 3-4. Notification to 3 handlers (Dispatch/MediatR)
/// 5-6. Query with return value (Dispatch/MediatR)
/// 7-8. 10 concurrent commands (Dispatch/MediatR)
/// 9-10. 100 concurrent commands (Dispatch/MediatR)
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MediatRComparisonBenchmarks
{
	// Excalibur infrastructure
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;
	private IServiceProvider? _dispatchDirectServiceProvider;
	private IDispatcher? _directDispatcher;
	private IDirectLocalDispatcher? _directLocalDispatcher;
	private IMessageContextFactory? _directContextFactory;

	// Singleton-promoted infrastructure (auto-promotion optimization)
	private IServiceProvider? _singletonServiceProvider;
	private IDirectLocalDispatcher? _singletonDirectLocalDispatcher;

	// MediatR infrastructure
	private IServiceProvider? _mediatrServiceProvider;
	private IMediator? _mediator;

	/// <summary>
	/// Initialize both Dispatch and MediatR service providers before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Setup Excalibur default local path
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging(); // Required for FinalDispatchHandler
		_ = dispatchServices.AddDispatch(); // Register lean default pipeline services
		_ = dispatchServices.AddTransient<DispatchTestCommandHandler>();
		_ = dispatchServices.AddTransient<DispatchTestQueryHandler>();
		_ = dispatchServices.AddTransient<DispatchTestNotificationHandler1>();
		_ = dispatchServices.AddTransient<DispatchTestNotificationHandler2>();
		_ = dispatchServices.AddTransient<DispatchTestNotificationHandler3>();
		_ = dispatchServices.AddTransient<IActionHandler<TestCommand>, DispatchTestCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler2>();
		_ = dispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler3>();
		_ = dispatchServices.AddTransient<IActionHandler<TestQuery, int>, DispatchTestQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		_dispatchContextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

		// Setup Excalibur strict direct-local profile (explicit no-middleware profile)
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
		_ = directDispatchServices.AddTransient<DispatchTestCommandHandler>();
		_ = directDispatchServices.AddTransient<DispatchTestQueryHandler>();
		_ = directDispatchServices.AddTransient<DispatchTestNotificationHandler1>();
		_ = directDispatchServices.AddTransient<DispatchTestNotificationHandler2>();
		_ = directDispatchServices.AddTransient<DispatchTestNotificationHandler3>();
		_ = directDispatchServices.AddTransient<IActionHandler<TestCommand>, DispatchTestCommandHandler>();
		_ = directDispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler1>();
		_ = directDispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler2>();
		_ = directDispatchServices.AddTransient<IEventHandler<TestNotification>, DispatchTestNotificationHandler3>();
		_ = directDispatchServices.AddTransient<IActionHandler<TestQuery, int>, DispatchTestQueryHandler>();

		_dispatchDirectServiceProvider = directDispatchServices.BuildServiceProvider();
		_directDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>();
		_directLocalDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;
		_directContextFactory = _dispatchDirectServiceProvider.GetRequiredService<IMessageContextFactory>();

		// Setup Excalibur singleton-promoted path (auto-promote stateless handlers)
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
		_ = singletonServices.AddTransient<DispatchTestCommandHandler>();
		_ = singletonServices.AddTransient<DispatchTestQueryHandler>();
		_ = singletonServices.AddTransient<IActionHandler<TestCommand>, DispatchTestCommandHandler>();
		_ = singletonServices.AddTransient<IActionHandler<TestQuery, int>, DispatchTestQueryHandler>();

		_singletonServiceProvider = singletonServices.BuildServiceProvider();
		_singletonDirectLocalDispatcher = _singletonServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;

		// Setup MediatR
		var mediatrServices = new ServiceCollection();
		_ = mediatrServices.AddLogging(); // For fair comparison
		_ = mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRComparisonBenchmarks>());

		_mediatrServiceProvider = mediatrServices.BuildServiceProvider();
		_mediator = _mediatrServiceProvider.GetRequiredService<IMediator>();

		// Warm and freeze Dispatch caches so benchmark reflects optimized production mode.
		WarmupAndFreezeDispatchCaches();
	}

	/// <summary>
	/// Cleanup service providers after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
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

		if (_mediatrServiceProvider is IDisposable mediatrDisposable)
		{
			mediatrDisposable.Dispose();
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
		var command = new TestCommand { Value = 42 };
		return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command).ConfigureAwait(false);
	}

	/// <summary>
	/// Excalibur.Dispatch strict direct-local profile (explicit no-middleware profile).
	/// </summary>
	[Benchmark(Description = "Dispatch: Single command strict direct-local")]
	public async Task<IMessageResult> Dispatch_SingleCommand_StrictDirectLocal()
	{
		var command = new TestCommand { Value = 42 };
		return await DispatchWithFreshContextAsync(_directDispatcher, _directContextFactory, command).ConfigureAwait(false);
	}

	/// <summary>
	/// Excalibur.Dispatch ultra-local API path (ValueTask, no IMessageResult materialization on success).
	/// </summary>
	[Benchmark(Description = "Dispatch: Single command ultra-local API")]
	public async Task Dispatch_SingleCommand_UltraLocalApi()
	{
		var command = new TestCommand { Value = 42 };
		await DispatchUltraLocalAsync(_directLocalDispatcher, command).ConfigureAwait(false);
	}

	/// <summary>
	/// MediatR: Single command handler invocation.
	/// </summary>
	[Benchmark(Description = "MediatR: Single command handler")]
	public async Task<Unit> MediatR_SingleCommandHandler()
	{
		var command = new MediatRTestCommand { Value = 42 };
		return await _mediator.Send(command, CancellationToken.None);
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch notification to multiple handlers (1 notification → 3 handlers).
	/// </summary>
	[Benchmark(Description = "Dispatch: Notification to 3 handlers")]
	public async Task<IMessageResult> Dispatch_NotificationMultipleHandlers()
	{
		var notification = new TestNotification { Message = "test" };
		return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, notification).ConfigureAwait(false);
	}

	/// <summary>
	/// MediatR: Notification to multiple handlers (1 notification → 3 handlers).
	/// </summary>
	[Benchmark(Description = "MediatR: Notification to 3 handlers")]
	public async Task MediatR_NotificationMultipleHandlers()
	{
		var notification = new MediatRTestNotification { Message = "test" };
		await _mediator.Publish(notification, CancellationToken.None);
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch query with return value.
	/// </summary>
	[Benchmark(Description = "Dispatch: Query with return value")]
	public async Task<IMessageResult> Dispatch_QueryWithReturnValue()
	{
		var query = new TestQuery { Id = 123 };
		return await DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, query).ConfigureAwait(false);
	}

	/// <summary>
	/// Excalibur.Dispatch typed query API path (IDispatchAction&lt;TResponse&gt;).
	/// </summary>
	[Benchmark(Description = "Dispatch: Query with return value (typed API)")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturnValue_TypedApi()
	{
		var query = new TestQuery { Id = 123 };
		return await DispatchWithFreshContextTypedAsync<TestQuery, int>(_dispatcher, _dispatchContextFactory, query)
			.ConfigureAwait(false);
	}

	/// <summary>
	/// Excalibur.Dispatch ultra-local query path (ValueTask&lt;T&gt;, no IMessageResult materialization on success).
	/// </summary>
	[Benchmark(Description = "Dispatch: Query ultra-local API")]
	public async Task<int?> Dispatch_QueryWithReturnValue_UltraLocalApi()
	{
		var query = new TestQuery { Id = 123 };
		return await DispatchUltraLocalWithResponseAsync<TestQuery, int>(_directLocalDispatcher, query).ConfigureAwait(false);
	}

	/// <summary>
	/// MediatR: Query with return value.
	/// </summary>
	[Benchmark(Description = "MediatR: Query with return value")]
	public async Task<int> MediatR_QueryWithReturnValue()
	{
		var query = new MediatRTestQuery { Id = 123 };
		return await _mediator.Send(query, CancellationToken.None);
	}

	// ============================================================================
	// CATEGORY 2: Optimization Variants
	// ============================================================================

	/// <summary>
	/// Excalibur.Dispatch with auto-promoted singleton handlers (PERF optimization).
	/// Stateless handlers are automatically promoted from transient to singleton lifetime.
	/// </summary>
	[Benchmark(Description = "Dispatch: Ultra-local singleton-promoted")]
	public async Task Dispatch_SingleCommand_SingletonPromoted()
	{
		var command = new TestCommand { Value = 42 };
		await DispatchUltraLocalAsync(_singletonDirectLocalDispatcher, command).ConfigureAwait(false);
	}

	/// <summary>
	/// Excalibur.Dispatch with auto-promoted singleton handlers (query with response).
	/// </summary>
	[Benchmark(Description = "Dispatch: Query singleton-promoted")]
	public async Task<int?> Dispatch_Query_SingletonPromoted()
	{
		var query = new TestQuery { Id = 123 };
		return await DispatchUltraLocalWithResponseAsync<TestQuery, int>(_singletonDirectLocalDispatcher, query).ConfigureAwait(false);
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
			var command = new TestCommand { Value = i };
			tasks[i] = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command);
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// MediatR: 10 concurrent command dispatches.
	/// </summary>
	[Benchmark(Description = "MediatR: 10 concurrent commands")]
	public async Task MediatR_ConcurrentCommands10()
	{
		var tasks = new Task<Unit>[10];
		for (int i = 0; i < 10; i++)
		{
			var command = new MediatRTestCommand { Value = i };
			tasks[i] = _mediator.Send(command, CancellationToken.None);
		}

		_ = await Task.WhenAll(tasks);
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
			var command = new TestCommand { Value = i };
			tasks[i] = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, command);
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// MediatR: 100 concurrent command dispatches.
	/// </summary>
	[Benchmark(Description = "MediatR: 100 concurrent commands")]
	public async Task MediatR_ConcurrentCommands100()
	{
		var tasks = new Task<Unit>[100];
		for (int i = 0; i < 100; i++)
		{
			var command = new MediatRTestCommand { Value = i };
			tasks[i] = _mediator.Send(command, CancellationToken.None);
		}

		_ = await Task.WhenAll(tasks);
	}

	private void WarmupAndFreezeDispatchCaches()
	{
		_ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new TestCommand { Value = 1 })
			.GetAwaiter().GetResult();
		_ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new TestNotification { Message = "warmup" })
			.GetAwaiter().GetResult();
		_ = DispatchWithFreshContextAsync(_dispatcher, _dispatchContextFactory, new TestQuery { Id = 1 })
			.GetAwaiter().GetResult();

		// Warmup singleton-promoted path
		if (_singletonDirectLocalDispatcher is not null)
		{
			DispatchUltraLocalAsync(_singletonDirectLocalDispatcher, new TestCommand { Value = 1 })
				.AsTask().GetAwaiter().GetResult();
			DispatchUltraLocalWithResponseAsync<TestQuery, int>(_singletonDirectLocalDispatcher, new TestQuery { Id = 1 })
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

	private static async Task<IMessageResult<TResponse>> DispatchWithFreshContextTypedAsync<TMessage, TResponse>(
		IDispatcher? dispatcher,
		IMessageContextFactory? contextFactory,
		TMessage message)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		ArgumentNullException.ThrowIfNull(contextFactory);

		var context = contextFactory.CreateContext();
		var dispatchTask = dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
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

		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
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
// Test Messages and Handlers (Excalibur)
// ============================================================================

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Test command for Dispatch benchmarks.
/// </summary>
public record TestCommand : IDispatchAction
{
	public int Value { get; init; }
}

/// <summary>
/// Handler for TestCommand (Dispatch).
/// </summary>
public class DispatchTestCommandHandler : IActionHandler<TestCommand>
{
	public Task HandleAsync(TestCommand message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test notification for Dispatch benchmarks.
/// </summary>
public record TestNotification : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler 1 for TestNotification (Dispatch).
/// </summary>
public class DispatchTestNotificationHandler1 : IEventHandler<TestNotification>
{
	public Task HandleAsync(TestNotification message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 2 for TestNotification (Dispatch).
/// </summary>
public class DispatchTestNotificationHandler2 : IEventHandler<TestNotification>
{
	public Task HandleAsync(TestNotification message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 3 for TestNotification (Dispatch).
/// </summary>
public class DispatchTestNotificationHandler3 : IEventHandler<TestNotification>
{
	public Task HandleAsync(TestNotification message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test query for Dispatch benchmarks.
/// </summary>
public record TestQuery : IDispatchAction<int>
{
	public int Id { get; init; }
}

/// <summary>
/// Handler for TestQuery (Dispatch).
/// </summary>
public class DispatchTestQueryHandler : IActionHandler<TestQuery, int>
{
	public Task<int> HandleAsync(TestQuery message, CancellationToken cancellationToken)
	{
		// Simulate query processing
		var result = message.Id * 2;
		return Task.FromResult(result);
	}
}

// ============================================================================
// Test Messages and Handlers (MediatR)
// ============================================================================

/// <summary>
/// Test command for MediatR benchmarks.
/// </summary>
public record MediatRTestCommand : IRequest<Unit>
{
	public int Value { get; init; }
}

/// <summary>
/// Handler for MediatRTestCommand.
/// </summary>
public class MediatRTestCommandHandler : IRequestHandler<MediatRTestCommand, Unit>
{
	public Task<Unit> Handle(MediatRTestCommand request, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		_ = request.Value * 2;
		return Task.FromResult(Unit.Value);
	}
}

/// <summary>
/// Test notification for MediatR benchmarks.
/// </summary>
public record MediatRTestNotification : INotification
{
	public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler 1 for MediatRTestNotification.
/// </summary>
public class MediatRTestNotificationHandler1 : INotificationHandler<MediatRTestNotification>
{
	public Task Handle(MediatRTestNotification notification, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 2 for MediatRTestNotification.
/// </summary>
public class MediatRTestNotificationHandler2 : INotificationHandler<MediatRTestNotification>
{
	public Task Handle(MediatRTestNotification notification, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 3 for MediatRTestNotification.
/// </summary>
public class MediatRTestNotificationHandler3 : INotificationHandler<MediatRTestNotification>
{
	public Task Handle(MediatRTestNotification notification, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test query for MediatR benchmarks.
/// </summary>
public record MediatRTestQuery : IRequest<int>
{
	public int Id { get; init; }
}

/// <summary>
/// Handler for MediatRTestQuery.
/// </summary>
public class MediatRTestQueryHandler : IRequestHandler<MediatRTestQuery, int>
{
	public Task<int> Handle(MediatRTestQuery request, CancellationToken cancellationToken)
	{
		// Simulate query processing (same as Dispatch)
		var result = request.Id * 2;
		return Task.FromResult(result);
	}
}

#pragma warning restore SA1402 // File may only contain a single type
