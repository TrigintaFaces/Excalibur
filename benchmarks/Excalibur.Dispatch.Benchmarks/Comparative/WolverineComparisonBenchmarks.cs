// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

using DispatchContext = Excalibur.Dispatch.Abstractions.IMessageContext;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Comparative benchmarks: Excalibur vs Wolverine.
/// Measures relative performance for async messaging scenarios.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-i1pxd: Wolverine Comparison Enhancement (5+ additional scenarios).
///
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - Wolverine: 5.2.0
///
/// Wolverine Focus Areas:
/// - Async message handling (command/event patterns)
/// - Local message bus performance
/// - Handler invocation overhead
/// - Request/Response patterns (Sprint 185)
/// - Batch operations (Sprint 185)
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class WolverineComparisonBenchmarks
{
	private static readonly TimeSpan QueueCompletionTimeout = TimeSpan.FromSeconds(5);

	// Excalibur infrastructure
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private DispatchContext? _dispatchContext;

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

		// Setup Excalibur
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging(); // Required for FinalDispatchHandler
		_ = dispatchServices.AddBenchmarkDispatch(); // Register benchmark pipeline options
		_ = dispatchServices.AddTransient<IActionHandler<WolverineTestCommand>, DispatchWolverineCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineTestEvent>, DispatchWolverineEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineTestQuery, int>, DispatchWolverineQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();
		_dispatchContext = contextFactory.CreateContext();

		// Setup Wolverine (local bus only, no external transports)
		_wolverineHost = await Host.CreateDefaultBuilder()
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
	/// Baseline: Excalibur.Dispatch single command handler invocation.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Dispatch: Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new WolverineTestCommand { Value = 42 };
		return await _dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None);
	}

	/// <summary>
	/// Wolverine: Single command invocation (InvokeAsync - in-process execution).
	/// </summary>
	[Benchmark(Description = "Wolverine: Single command (InvokeAsync)")]
	public async Task Wolverine_SingleCommandInvoke()
	{
		var command = new WolverineCommandMessage { Value = 42 };
		await _wolverineBus.InvokeAsync(command, CancellationToken.None);
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
	public async Task<IMessageResult> Dispatch_EventMultipleHandlers()
	{
		var @event = new WolverineTestEvent { Message = "test" };
		return await _dispatcher.DispatchAsync(@event, _dispatchContext, CancellationToken.None);
	}

	/// <summary>
	/// Wolverine: Event publishing (PublishAsync - local bus).
	/// </summary>
	[Benchmark(Description = "Wolverine: Event publish")]
	public async Task Wolverine_EventPublish()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = WolverineBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var @event = new WolverineEventMessage
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};
		await _wolverineBus.PublishAsync(@event);
		await completionTask.WaitAsync(QueueCompletionTimeout);
	}

	// ============================================================================
	// CATEGORY 3: Concurrent Operations
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch 10 concurrent commands.
	/// </summary>
	[Benchmark(Description = "Dispatch: 10 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new List<Task<IMessageResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new WolverineTestCommand { Value = i };
			tasks.Add(_dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: 10 concurrent commands (InvokeAsync).
	/// </summary>
	[Benchmark(Description = "Wolverine: 10 concurrent commands")]
	public async Task Wolverine_ConcurrentCommands10()
	{
		var tasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new WolverineCommandMessage { Value = i };
			tasks.Add(_wolverineBus.InvokeAsync(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks);
	}

	// ============================================================================
	// Sprint 185 - New Benchmark Scenarios (bd-i1pxd)
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch query with return value.
	/// </summary>
	[Benchmark(Description = "Dispatch: Query with return value")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturnValue()
	{
		var query = new WolverineTestQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<WolverineTestQuery, int>(query, _dispatchContext, CancellationToken.None);
	}

	/// <summary>
	/// Wolverine: Query with return value (InvokeAsync with response).
	/// </summary>
	[Benchmark(Description = "Wolverine: Query with return value")]
	public async Task<int> Wolverine_QueryWithReturnValue()
	{
		var query = new WolverineQueryMessage { Id = 123 };
		return await _wolverineBus.InvokeAsync<int>(query, CancellationToken.None);
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch 100 concurrent commands.
	/// </summary>
	[Benchmark(Description = "Dispatch: 100 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new List<Task<IMessageResult>>(100);
		for (int i = 0; i < 100; i++)
		{
			var command = new WolverineTestCommand { Value = i };
			tasks.Add(_dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: 100 concurrent commands (InvokeAsync).
	/// </summary>
	[Benchmark(Description = "Wolverine: 100 concurrent commands")]
	public async Task Wolverine_ConcurrentCommands100()
	{
		var tasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			var command = new WolverineCommandMessage { Value = i };
			tasks.Add(_wolverineBus.InvokeAsync(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch batch queries (10 queries).
	/// </summary>
	[Benchmark(Description = "Dispatch: Batch queries (10)")]
	public async Task Dispatch_BatchQueries10()
	{
		var tasks = new List<Task<IMessageResult<int>>>(10);
		for (int i = 0; i < 10; i++)
		{
			var query = new WolverineTestQuery { Id = i };
			tasks.Add(_dispatcher.DispatchAsync<WolverineTestQuery, int>(query, _dispatchContext, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// Wolverine: Batch queries (10 queries).
	/// </summary>
	[Benchmark(Description = "Wolverine: Batch queries (10)")]
	public async Task Wolverine_BatchQueries10()
	{
		var tasks = new List<Task<int>>(10);
		for (int i = 0; i < 10; i++)
		{
			var query = new WolverineQueryMessage { Id = i };
			tasks.Add(_wolverineBus.InvokeAsync<int>(query, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
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
/// Test query for Dispatch/Wolverine comparison benchmarks (Sprint 185).
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
/// Test query message for Wolverine benchmarks (Sprint 185).
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
