// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Comparative benchmarks: Excalibur vs MassTransit.
/// Measures relative performance for in-memory messaging scenarios.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-fch68: MassTransit Comparison NEW (5 scenarios).
///
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - MassTransit: 8.x (latest stable)
///
/// MassTransit Focus Areas:
/// - In-memory bus performance
/// - Consumer-based message handling
/// - Publish/Send patterns
/// - Concurrency and throughput
///
/// Scenarios (5 total):
/// 1. Dispatch vs MassTransit: Single message
/// 2. Dispatch vs MassTransit: Event publish (multi-consumer)
/// 3. Dispatch vs MassTransit: 10 concurrent messages
/// 4. Dispatch vs MassTransit: 100 concurrent messages
/// 5. Dispatch vs MassTransit: Batch send (10 messages)
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MassTransitComparisonBenchmarks
{
	private static readonly TimeSpan QueueCompletionTimeout = TimeSpan.FromSeconds(5);

	// Excalibur infrastructure
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContext? _dispatchContext;

	// MassTransit infrastructure
	private IServiceProvider? _massTransitServiceProvider;
	private IBusControl? _busControl;
	private IBus? _bus;

	/// <summary>
	/// Initialize both Dispatch and MassTransit before benchmarks.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		MassTransitBenchmarkCompletionTracker.Reset();

		// Setup Excalibur
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging(); // Required for FinalDispatchHandler
		_ = dispatchServices.AddBenchmarkDispatch(); // Register benchmark pipeline options
		_ = dispatchServices.AddTransient<IActionHandler<MassTransitTestCommand>, DispatchMassTransitCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitTestEvent>, DispatchMassTransitEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitTestEvent>, DispatchMassTransitEventHandler2>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();
		_dispatchContext = contextFactory.CreateContext();

		// Setup MassTransit (in-memory bus only for fair comparison)
		var massTransitServices = new ServiceCollection();
		_ = massTransitServices.AddMassTransit(x =>
		{
			_ = x.AddConsumer<MassTransitCommandConsumer>();
			_ = x.AddConsumer<MassTransitEventConsumer1>();
			_ = x.AddConsumer<MassTransitEventConsumer2>();

			x.UsingInMemory((context, cfg) =>
			{
				cfg.ConfigureEndpoints(context);
			});
		});

		_massTransitServiceProvider = massTransitServices.BuildServiceProvider();
		_busControl = _massTransitServiceProvider.GetRequiredService<IBusControl>();
		await _busControl.StartAsync(CancellationToken.None).ConfigureAwait(false);
		_bus = _busControl;
	}

	/// <summary>
	/// Cleanup after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		try
		{
			if (_dispatchServiceProvider is IDisposable dispatchDisposable)
			{
				dispatchDisposable.Dispose();
			}
		}
		catch
		{
			// Suppress disposal exceptions
		}

		try
		{
			if (_busControl is not null)
			{
				await _busControl.StopAsync(CancellationToken.None).ConfigureAwait(false);
			}

			// MassTransit uses IAsyncDisposable for its UsageTracker
			if (_massTransitServiceProvider is IAsyncDisposable massTransitAsyncDisposable)
			{
				await massTransitAsyncDisposable.DisposeAsync().ConfigureAwait(false);
			}
			else if (_massTransitServiceProvider is IDisposable massTransitDisposable)
			{
				massTransitDisposable.Dispose();
			}
		}
		catch
		{
			// Suppress disposal exceptions - MassTransit's UsageTracker async disposal can throw
		}

		MassTransitBenchmarkCompletionTracker.Reset();
	}

	// ============================================================================
	// CATEGORY 1: Single Message Dispatch
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch single command handler invocation.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Dispatch: Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new MassTransitTestCommand { Value = 42 };
		return await _dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None);
	}

	/// <summary>
	/// MassTransit: Single command via in-memory bus.
	/// </summary>
	[Benchmark(Description = "MassTransit: Single command")]
	public async Task MassTransit_SingleCommand()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1);
		var command = new MassTransitCommandMessage
		{
			Value = 42,
			BenchmarkId = benchmarkId,
		};
		await _bus.Publish(command, CancellationToken.None);
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
		var @event = new MassTransitTestEvent { Message = "test" };
		return await _dispatcher.DispatchAsync(@event, _dispatchContext, CancellationToken.None);
	}

	/// <summary>
	/// MassTransit: Event publishing to multiple consumers (1 event → 2 consumers).
	/// </summary>
	[Benchmark(Description = "MassTransit: Event to 2 consumers")]
	public async Task MassTransit_EventMultipleConsumers()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var @event = new MassTransitEventMessage
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};
		await _bus.Publish(@event, CancellationToken.None);
		await completionTask.WaitAsync(QueueCompletionTimeout);
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
		var tasks = new List<Task<IMessageResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new MassTransitTestCommand { Value = i };
			tasks.Add(_dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// MassTransit: 10 concurrent command publishes.
	/// </summary>
	[Benchmark(Description = "MassTransit: 10 concurrent commands")]
	public async Task MassTransit_ConcurrentCommands10()
	{
		var tasks = new List<Task>(10);
		var completionTasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			var command = new MassTransitCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			};
			tasks.Add(_bus.Publish(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout)));
	}

	/// <summary>
	/// Baseline: Excalibur.Dispatch 100 concurrent command dispatches.
	/// </summary>
	[Benchmark(Description = "Dispatch: 100 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new List<Task<IMessageResult>>(100);
		for (int i = 0; i < 100; i++)
		{
			var command = new MassTransitTestCommand { Value = i };
			tasks.Add(_dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks);
	}

	/// <summary>
	/// MassTransit: 100 concurrent command publishes.
	/// </summary>
	[Benchmark(Description = "MassTransit: 100 concurrent commands")]
	public async Task MassTransit_ConcurrentCommands100()
	{
		var tasks = new List<Task>(100);
		var completionTasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			var command = new MassTransitCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			};
			tasks.Add(_bus.Publish(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout)));
	}

	// ============================================================================
	// CATEGORY 4: Batch Operations
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur.Dispatch batch send (10 messages sequentially).
	/// </summary>
	[Benchmark(Description = "Dispatch: Batch send (10)")]
	public async Task Dispatch_BatchSend10()
	{
		for (int i = 0; i < 10; i++)
		{
			var command = new MassTransitTestCommand { Value = i };
			_ = await _dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None);
		}
	}

	/// <summary>
	/// MassTransit: Batch send (10 messages via PublishBatch).
	/// </summary>
	[Benchmark(Description = "MassTransit: Batch send (10)")]
	public async Task MassTransit_BatchSend10()
	{
		var completionTasks = new List<Task>(10);
		var messages = new List<MassTransitCommandMessage>(10);
		for (int i = 0; i < 10; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			messages.Add(new MassTransitCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			});
		}

		await _bus.PublishBatch(messages, CancellationToken.None);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout)));
	}
}

// ============================================================================
// Test Messages and Handlers (Excalibur)
// ============================================================================

#pragma warning disable SA1402 // File may only contain a single type

/// <summary>
/// Test command for Dispatch/MassTransit comparison benchmarks.
/// </summary>
public record MassTransitTestCommand : IDispatchAction
{
	public int Value { get; init; }
}

/// <summary>
/// Handler for MassTransitTestCommand (Dispatch).
/// </summary>
public class DispatchMassTransitCommandHandler : IActionHandler<MassTransitTestCommand>
{
	public Task HandleAsync(MassTransitTestCommand message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test event for Dispatch/MassTransit comparison benchmarks.
/// </summary>
public record MassTransitTestEvent : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
}

/// <summary>
/// Handler 1 for MassTransitTestEvent (Dispatch).
/// </summary>
public class DispatchMassTransitEventHandler1 : IEventHandler<MassTransitTestEvent>
{
	public Task HandleAsync(MassTransitTestEvent message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

/// <summary>
/// Handler 2 for MassTransitTestEvent (Dispatch).
/// </summary>
public class DispatchMassTransitEventHandler2 : IEventHandler<MassTransitTestEvent>
{
	public Task HandleAsync(MassTransitTestEvent message, CancellationToken cancellationToken)
	{
		// Simulate minimal processing
		return Task.CompletedTask;
	}
}

// ============================================================================
// Test Messages and Consumers (MassTransit)
// ============================================================================

/// <summary>
/// Test command message for MassTransit benchmarks.
/// </summary>
public record MassTransitCommandMessage
{
	public int Value { get; set; }
	public Guid BenchmarkId { get; set; }
}

/// <summary>
/// MassTransit consumer for MassTransitCommandMessage.
/// </summary>
public class MassTransitCommandConsumer : IConsumer<MassTransitCommandMessage>
{
	public Task Consume(ConsumeContext<MassTransitCommandMessage> context)
	{
		// Simulate minimal processing (same as Dispatch)
		_ = context.Message.Value * 2;
		MassTransitBenchmarkCompletionTracker.Signal(context.Message.BenchmarkId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// Test event message for MassTransit benchmarks.
/// </summary>
public record MassTransitEventMessage
{
	public string Message { get; set; } = string.Empty;
	public Guid BenchmarkId { get; set; }
}

/// <summary>
/// MassTransit consumer 1 for MassTransitEventMessage.
/// </summary>
public class MassTransitEventConsumer1 : IConsumer<MassTransitEventMessage>
{
	public Task Consume(ConsumeContext<MassTransitEventMessage> context)
	{
		// Simulate minimal processing (same as Dispatch)
		MassTransitBenchmarkCompletionTracker.Signal(context.Message.BenchmarkId);
		return Task.CompletedTask;
	}
}

/// <summary>
/// MassTransit consumer 2 for MassTransitEventMessage.
/// </summary>
public class MassTransitEventConsumer2 : IConsumer<MassTransitEventMessage>
{
	public Task Consume(ConsumeContext<MassTransitEventMessage> context)
	{
		// Simulate minimal processing (same as Dispatch)
		MassTransitBenchmarkCompletionTracker.Signal(context.Message.BenchmarkId);
		return Task.CompletedTask;
	}
}

internal static class MassTransitBenchmarkCompletionTracker
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
