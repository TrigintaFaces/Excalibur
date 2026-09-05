// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Wolverine;

using DispatchContext = Excalibur.Dispatch.IMessageContext;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Comparative benchmarks: Excalibur vs Wolverine.
/// Measures relative performance for async messaging scenarios.
/// </summary>
/// <remarks>
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - Wolverine: 5.31.1
///
/// Dispatch uses lean AddDispatch() (no cache/dedupe/outbox middleware) for fair
/// comparison against Wolverine's InvokeAsync (bare in-process handler call).
/// Fresh context per iteration, warmup + freeze for production-representative numbers.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class WolverineComparisonBenchmarks
{
	private static readonly TimeSpan QueueCompletionTimeout = TimeSpan.FromSeconds(5);

	// Excalibur infrastructure — standard (lean) path
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// Excalibur infrastructure — direct-local path (no middleware)
	private IServiceProvider? _dispatchDirectServiceProvider;
	private IDispatcher? _contextLessDispatcher;

	// Wolverine infrastructure
	private IHost? _wolverineHost;
	private IMessageBus? _wolverineBus;

	/// <summary>
	/// Initialize both Dispatch and Wolverine before benchmarks.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		WolverineBenchmarkCompletionTracker.Reset();

		// Setup Excalibur — lean default (no cache/dedupe/outbox)
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddDispatch();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineTestCommand>, DispatchWolverineCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineTestQuery, int>, DispatchWolverineQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		_dispatchContextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

		// Setup Excalibur — strict direct-local (no middleware, for ultra-local comparison)
		var directDispatchServices = new ServiceCollection();
		_ = directDispatchServices.AddLogging();
		_ = directDispatchServices.AddDispatch(builder =>
		{
			_ = builder.ConfigurePipeline("DirectLocal", pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Direct));
			_ = builder.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.EnablePipelineSynthesis = false;
				options.Features.EnableMetrics = false;
				options.Features.EnableAuthorization = false;
				options.Features.ValidateMessageSchemas = false;
				options.Features.EnableVersioning = false;
				options.Features.EnableMultiTenancy = false;
				options.Features.EnableTransactions = false;
			});
		});
		_ = directDispatchServices.AddTransient<IActionHandler<WolverineTestCommand>, DispatchWolverineCommandHandler>();
		_ = directDispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler1>();
		_ = directDispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler2>();
		_ = directDispatchServices.AddTransient<IActionHandler<WolverineTestQuery, int>, DispatchWolverineQueryHandler>();

		_dispatchDirectServiceProvider = directDispatchServices.BuildServiceProvider();
		_contextLessDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>();

		// Setup Wolverine (local bus only, no external transports)
		_wolverineHost = await Host.CreateDefaultBuilder()
			// Host.CreateDefaultBuilder installs Console, Debug and EventSource logging providers. The
			// Dispatch side of every comparison is a bare ServiceCollection with no provider, so leaving
			// these on measures Wolverine's logging pipeline against our silence — a per-message console
			// write inside the measured region, biasing the result in our favour. Clear them so both
			// sides log nothing and the comparison is of dispatch overhead.
			.ConfigureLogging(logging => logging.ClearProviders())
			.UseWolverine(opts =>
			{
				// Local bus only (no external transports for fair comparison)
				_ = opts.LocalQueueFor<WolverineCommandMessage>();
				_ = opts.LocalQueueFor<WolverineEventMessage>();
				_ = opts.LocalQueueFor<WolverineQueryMessage>();

				// Keep benchmark handler discovery deterministic across BDN worker processes.
				opts.Discovery.IncludeAssembly(typeof(WolverineComparisonBenchmarks).Assembly);
				opts.Discovery.IncludeType(typeof(WolverineCommandHandler));
				opts.Discovery.IncludeType(typeof(WolverineEventHandler));
				opts.Discovery.IncludeType(typeof(WolverineEventHandler2));
				opts.Discovery.IncludeType(typeof(WolverineQueryHandler));
			})
			.StartAsync();

		_wolverineBus = _wolverineHost.Services.GetRequiredService<IMessageBus>();

		await VerifyInlineFanOutInvokesBothHandlers();

		// Warm and freeze Dispatch caches so benchmark reflects optimized production mode.
		WarmupAndFreezeDispatchCaches();
	}

	/// <summary>
	/// Proves the fan-out row measures BOTH handlers before any timing is collected.
	/// </summary>
	/// <remarks>
	/// The fan-out row calls <c>InvokeAsync</c> and does not await a completion tracker, because Wolverine
	/// runs merged non-sticky handlers inline. That is a claim about Wolverine's code generation, so it is
	/// verified here rather than assumed: if a future Wolverine version routed the second handler through a
	/// queue, or if a handler were made sticky, the row would silently measure half the work and report it
	/// as a speedup. This throws instead.
	/// </remarks>
	private async Task VerifyInlineFanOutInvokesBothHandlers()
	{
		var probeId = Guid.NewGuid();
		var bothHandlersSignalled = WolverineBenchmarkCompletionTracker.Register(probeId, expectedSignals: 2);

		await _wolverineBus!.InvokeAsync(new WolverineEventMessage { Message = "fanout-probe", BenchmarkId = probeId });

		// InvokeAsync has already returned. If the handlers ran inline, both signals are in and this task is
		// already complete; a short grace period only guards scheduler jitter, it does not wait for a queue.
		if (bothHandlersSignalled != await Task.WhenAny(bothHandlersSignalled, Task.Delay(TimeSpan.FromSeconds(5))))
		{
			throw new InvalidOperationException(
				"Wolverine InvokeAsync did not invoke both event handlers inline. The fan-out benchmark would "
				+ "measure fewer handlers than the Dispatch row it is compared against, producing a false "
				+ "speedup. Check whether a handler became sticky or Wolverine changed its handler merging.");
		}

		WolverineBenchmarkCompletionTracker.Reset();
	}

	/// <summary>
	/// Cleanup after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_dispatchServiceProvider is IDisposable dispatchDisposable)
		{
			dispatchDisposable.Dispose();
		}

		if (_dispatchDirectServiceProvider is IDisposable directDisposable)
		{
			directDisposable.Dispose();
		}

		if (_wolverineHost != null)
		{
			await _wolverineHost.StopAsync();
			_wolverineHost.Dispose();
		}

		WolverineBenchmarkCompletionTracker.Reset();
	}

	// ============================================================================
	// CATEGORY 1: Command Handler Invocation
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch single command handler invocation (standard path).
	/// </summary>
	[Benchmark(Baseline = true, Description = "Dispatch: Single command")]
	public Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new WolverineTestCommand { Value = 42 };
		return DispatchWithFreshContextAsync(command);
	}

	/// <summary>
	/// Excalibur.Dispatch ultra-local path (no middleware, no IMessageResult materialization).
	/// Closest apples-to-apples comparison with Wolverine InvokeAsync.
	/// </summary>
	[Benchmark(Description = "Dispatch: Single command (context-less 2-arg)")]
	public Task<IMessageResult> Dispatch_SingleCommand_UltraLocal()
	{
		var command = new WolverineTestCommand { Value = 42 };
		return _contextLessDispatcher!.DispatchAsync(command, CancellationToken.None);
	}

	/// <summary>
	/// Wolverine: Single command invocation (InvokeAsync - in-process execution).
	/// </summary>
	[Benchmark(Description = "Wolverine: Single command (InvokeAsync)")]
	public Task Wolverine_SingleCommandInvoke()
	{
		var command = new WolverineCommandMessage { Value = 42 };
		return _wolverineBus.InvokeAsync(command, CancellationToken.None);
	}

	/// <summary>
	/// Wolverine: Single command via local queue (SendAsync - queued execution).
	/// </summary>
	[Benchmark(Description = "Wolverine: Single command (SendAsync)")]
	public async Task Wolverine_SingleCommandSend()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = WolverineBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1);
		var command = new WolverineCommandMessage
		{
			Value = 42,
			BenchmarkId = benchmarkId,
		};
		await _wolverineBus.SendAsync(command);
		await completionTask.WaitAsync(QueueCompletionTimeout);
	}

	// ============================================================================
	// CATEGORY 2: Event Broadcasting
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch event to multiple handlers (1 event → 2 handlers).
	/// </summary>
	[Benchmark(Description = "Dispatch: Event to 2 handlers")]
	public Task<IMessageResult> Dispatch_EventMultipleHandlers()
	{
		var @event = new WolverineTestEvent { Message = "test" };
		return DispatchWithFreshContextAsync(@event);
	}

	/// <summary>
	/// Wolverine: Event fan-out to 2 handlers, invoked inline.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This row previously used <c>PublishAsync</c> plus a completion tracker. That awaited real handler
	/// execution, so it was not the never-awaits defect — but it compared Wolverine's LOCAL QUEUE against
	/// Dispatch running both handlers on the calling thread. The published 53.7x result was therefore
	/// measuring a thread-pool handoff and a cross-thread continuation, not dispatch overhead.
	/// </para>
	/// <para>
	/// <c>InvokeAsync</c> is the parity call. Wolverine merges every non-sticky handler for a message type
	/// into a single generated <c>HandleAsync</c> that runs them inline and sequentially; the queue-based
	/// fan-out path is reachable only when EVERY handler is sticky, which requires <c>[StickyHandler]</c> or
	/// <c>AddStickyHandler</c>. This benchmark uses neither, so both handlers run inline here — matching the
	/// Dispatch side's execution model rather than its API name.
	/// </para>
	/// <para>
	/// That both handlers genuinely run is asserted once at startup by
	/// <see cref="VerifyInlineFanOutInvokesBothHandlers"/>. Without it, a change that silently invoked one
	/// handler would make this row ~2x faster and look like an improvement.
	/// </para>
	/// </remarks>
	[Benchmark(Description = "Wolverine: Event to 2 handlers (inline)")]
	public Task Wolverine_EventPublish()
	{
		var @event = new WolverineEventMessage
		{
			Message = "test",
			BenchmarkId = Guid.Empty,
		};
		return _wolverineBus.InvokeAsync(@event);
	}

	// ============================================================================
	// CATEGORY 3: Concurrent Operations
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch 10 concurrent commands.
	/// </summary>
	[Benchmark(Description = "Dispatch: 10 concurrent commands")]
	public Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new Task<IMessageResult>[10];
		for (int i = 0; i < 10; i++)
		{
			var command = new WolverineTestCommand { Value = i };
			tasks[i] = DispatchWithFreshContextAsync(command);
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: 10 concurrent commands (InvokeAsync).
	/// </summary>
	[Benchmark(Description = "Wolverine: 10 concurrent commands")]
	public Task Wolverine_ConcurrentCommands10()
	{
		var tasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new WolverineCommandMessage { Value = i };
			tasks.Add(_wolverineBus.InvokeAsync(command, CancellationToken.None));
		}

		return Task.WhenAll(tasks);
	}

	// ============================================================================
	// CATEGORY 4: Query / Return Value
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch query with return value.
	/// </summary>
	[Benchmark(Description = "Dispatch: Query with return value")]
	public Task<IMessageResult<int>> Dispatch_QueryWithReturnValue()
	{
		var query = new WolverineTestQuery { Id = 123 };
		return DispatchWithFreshContextTypedAsync<WolverineTestQuery, int>(query);
	}

	/// <summary>
	/// Wolverine: Query with return value (InvokeAsync with response).
	/// </summary>
	[Benchmark(Description = "Wolverine: Query with return value")]
	public Task<int> Wolverine_QueryWithReturnValue()
	{
		var query = new WolverineQueryMessage { Id = 123 };
		return _wolverineBus.InvokeAsync<int>(query, CancellationToken.None);
	}

	// ============================================================================
	// CATEGORY 5: High Concurrency
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch 100 concurrent commands.
	/// </summary>
	[Benchmark(Description = "Dispatch: 100 concurrent commands")]
	public Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new Task<IMessageResult>[100];
		for (int i = 0; i < 100; i++)
		{
			var command = new WolverineTestCommand { Value = i };
			tasks[i] = DispatchWithFreshContextAsync(command);
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: 100 concurrent commands (InvokeAsync).
	/// </summary>
	[Benchmark(Description = "Wolverine: 100 concurrent commands")]
	public Task Wolverine_ConcurrentCommands100()
	{
		var tasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			var command = new WolverineCommandMessage { Value = i };
			tasks.Add(_wolverineBus.InvokeAsync(command, CancellationToken.None));
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch batch queries (10 queries).
	/// </summary>
	[Benchmark(Description = "Dispatch: Batch queries (10)")]
	public Task Dispatch_BatchQueries10()
	{
		var tasks = new Task<IMessageResult<int>>[10];
		for (int i = 0; i < 10; i++)
		{
			var query = new WolverineTestQuery { Id = i };
			tasks[i] = DispatchWithFreshContextTypedAsync<WolverineTestQuery, int>(query);
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: Batch queries (10 queries).
	/// </summary>
	[Benchmark(Description = "Wolverine: Batch queries (10)")]
	public Task Wolverine_BatchQueries10()
	{
		var tasks = new List<Task<int>>(10);
		for (int i = 0; i < 10; i++)
		{
			var query = new WolverineQueryMessage { Id = i };
			tasks.Add(_wolverineBus.InvokeAsync<int>(query, CancellationToken.None));
		}

		return Task.WhenAll(tasks);
	}

	// ============================================================================
	// Helper Methods
	// ============================================================================

	private void WarmupAndFreezeDispatchCaches()
	{
		_ = DispatchWithFreshContextAsync(new WolverineTestCommand { Value = 1 })
			.GetAwaiter().GetResult();
		_ = DispatchWithFreshContextAsync(new WolverineTestEvent { Message = "warmup" })
			.GetAwaiter().GetResult();

		if (_contextLessDispatcher is not null)
		{
			_ = _contextLessDispatcher.DispatchAsync(new WolverineTestCommand { Value = 1 }, CancellationToken.None)
				.GetAwaiter().GetResult();
		}

		HandlerInvoker.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();
		HandlerActivator.FreezeCache();
		FinalDispatchHandler.FreezeResultFactoryCache();
	}

	// Deliberately NOT async, and neither are the single-call benchmark methods that use it. An async
	// frame whose result is a non-null reference allocates a Task<T> (AsyncTaskMethodBuilder<T> only
	// caches the default-result task), so an async helper plus an async benchmark body charged the
	// Dispatch arm ~144 B that a competitor arm returning its own library Task never paid. Returning
	// the dispatcher's own Task preserves fresh-context-per-invocation while keeping the harness frame
	// count at zero for every arm, so the allocation column measures the library, not the harness.
	private Task<IMessageResult> DispatchWithFreshContextAsync<TMessage>(TMessage message)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_dispatchContextFactory);

		var context = _dispatchContextFactory.CreateContext();
		var dispatchTask = _dispatcher.DispatchAsync(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			_dispatchContextFactory.Return(context);
			return dispatchTask;
		}

		return AwaitAndReturnContextAsync(dispatchTask, _dispatchContextFactory, context);
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

	// Deliberately NOT async, and neither are the single-call benchmark methods that use it. An async
	// frame whose result is a non-null reference allocates a Task<T> (AsyncTaskMethodBuilder<T> only
	// caches the default-result task), so an async helper plus an async benchmark body charged the
	// Dispatch arm ~144 B that a competitor arm returning its own library Task never paid. Returning
	// the dispatcher's own Task preserves fresh-context-per-invocation while keeping the harness frame
	// count at zero for every arm, so the allocation column measures the library, not the harness.
	private Task<IMessageResult<TResponse>> DispatchWithFreshContextTypedAsync<TMessage, TResponse>(TMessage message)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_dispatchContextFactory);

		var context = _dispatchContextFactory.CreateContext();
		var dispatchTask = _dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			_dispatchContextFactory.Return(context);
			return dispatchTask;
		}

		return AwaitAndReturnTypedContextAsync(dispatchTask, _dispatchContextFactory, context);
	}

	private static async Task<IMessageResult<TResponse>> AwaitAndReturnTypedContextAsync<TResponse>(
		Task<IMessageResult<TResponse>> dispatchTask,
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
}

// ============================================================================
// Test Messages and Handlers (Excalibur)
// ============================================================================

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Test command for Dispatch/Wolverine comparison benchmarks.
/// </summary>
public record WolverineTestCommand : IDispatchAction
{
	public int Value { get; init; }
}

/// <summary>
/// Handler for WolverineTestCommand (Dispatch).
/// </summary>
public class DispatchWolverineCommandHandler : IActionHandler<WolverineTestCommand>
{
	public Task HandleAsync(WolverineTestCommand message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test event for Dispatch/Wolverine comparison benchmarks.
/// </summary>
public record WolverineTestEvent : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler 1 for WolverineTestEvent (Dispatch).
/// </summary>
public class DispatchWolverineEventHandler1 : IEventHandler<WolverineTestEvent>
{
	public Task HandleAsync(WolverineTestEvent message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 2 for WolverineTestEvent (Dispatch).
/// </summary>
public class DispatchWolverineEventHandler2 : IEventHandler<WolverineTestEvent>
{
	public Task HandleAsync(WolverineTestEvent message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test query for Dispatch/Wolverine comparison benchmarks.
/// </summary>
public record WolverineTestQuery : IDispatchAction<int>
{
	public int Id { get; init; }
}

/// <summary>
/// Handler for WolverineTestQuery (Dispatch).
/// </summary>
public class DispatchWolverineQueryHandler : IActionHandler<WolverineTestQuery, int>
{
	public Task<int> HandleAsync(WolverineTestQuery message, CancellationToken cancellationToken)
	{
		// Simulate query processing
		var result = message.Id * 2;
		return Task.FromResult(result);
	}
}

// ============================================================================
// Test Messages and Handlers (Wolverine)
// ============================================================================

/// <summary>
/// Test command message for Wolverine benchmarks.
/// </summary>
public record WolverineCommandMessage
{
	public int Value { get; set; }
	public Guid BenchmarkId { get; set; }
}

/// <summary>
/// Wolverine handler for WolverineCommandMessage (convention-based, auto-discovered).
/// </summary>
public static class WolverineCommandHandler
{
	public static Task Handle(WolverineCommandMessage command, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		_ = command.Value * 2;
		WolverineBenchmarkCompletionTracker.Signal(command.BenchmarkId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test event message for Wolverine benchmarks.
/// </summary>
public record WolverineEventMessage
{
	public string Message { get; set; } = string.Empty;
	public Guid BenchmarkId { get; set; }
}

/// <summary>
/// Wolverine handler for WolverineEventMessage (convention-based, auto-discovered).
/// </summary>
public static class WolverineEventHandler
{
	public static Task Handle(WolverineEventMessage @event, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		WolverineBenchmarkCompletionTracker.Signal(@event.BenchmarkId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Second Wolverine handler for WolverineEventMessage to match Dispatch 2-handler fan-out.
/// </summary>
public static class WolverineEventHandler2
{
	public static Task Handle(WolverineEventMessage @event, CancellationToken cancellationToken)
	{
		// Simulate minimal processing (same as Dispatch)
		WolverineBenchmarkCompletionTracker.Signal(@event.BenchmarkId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test query message for Wolverine benchmarks.
/// </summary>
public record WolverineQueryMessage
{
	public int Id { get; set; }
}

/// <summary>
/// Wolverine handler for WolverineQueryMessage with return value (convention-based, auto-discovered).
/// </summary>
public static class WolverineQueryHandler
{
	public static Task<int> Handle(WolverineQueryMessage query, CancellationToken cancellationToken)
	{
		// Simulate query processing (same as Dispatch)
		return Task.FromResult(query.Id * 2);
	}
}

internal static class WolverineBenchmarkCompletionTracker
{
	private sealed class PendingCompletion
	{
		private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private int _remainingSignals;

		public PendingCompletion(int expectedSignals)
		{
			_remainingSignals = expectedSignals;
		}

		public Task CompletionTask => _completion.Task;

		public bool Signal()
		{
			if (Interlocked.Decrement(ref _remainingSignals) <= 0)
			{
				_ = _completion.TrySetResult(true);
				return true;
			}

			return false;
		}
	}

	private static readonly ConcurrentDictionary<Guid, PendingCompletion> Pending = new();

	public static Task Register(Guid benchmarkId, int expectedSignals)
	{
		var pending = new PendingCompletion(expectedSignals);
		if (!Pending.TryAdd(benchmarkId, pending))
		{
			throw new InvalidOperationException($"Duplicate benchmark completion id '{benchmarkId}'.");
		}

		return pending.CompletionTask;
	}

	public static void Signal(Guid benchmarkId)
	{
		if (benchmarkId == Guid.Empty)
		{
			return;
		}

		if (Pending.TryGetValue(benchmarkId, out var pending))
		{
			if (pending.Signal())
			{
				_ = Pending.TryRemove(benchmarkId, out _);
			}
		}
	}

	public static void Reset()
	{
		Pending.Clear();
	}
}

#pragma warning restore SA1402 // File may only contain a single type