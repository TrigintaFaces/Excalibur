// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

using DispatchMessageBus = Excalibur.Dispatch.Abstractions.Transport.IMessageBus;
using DispatchMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;
using WolverineMessageBus = Wolverine.IMessageBus;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention
#pragma warning disable SA1402 // File may only contain a single type - benchmarks with supporting types

/// <summary>
/// Queued/bus end-to-end parity benchmark across Dispatch remote routing, Wolverine Send/Publish, and MassTransit Publish.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class TransportQueueParityComparisonBenchmarks
{
	private static readonly TimeSpan QueueCompletionTimeout = TimeSpan.FromSeconds(5);
	private static readonly RoutingDecision DispatchQueuedRoutingDecision =
		RoutingDecision.Success("dispatch-queued", ["dispatch-queued"]);

	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _contextFactory;
	private DispatchQueuedParityMessageBus? _dispatchQueueBus;

	private IHost? _wolverineHost;
	private WolverineMessageBus? _wolverineBus;

	private IServiceProvider? _massTransitServiceProvider;
	private IBusControl? _massTransitBusControl;
	private IBus? _massTransitBus;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		DispatchQueuedBenchmarkCompletionTracker.Reset();
		WolverineBenchmarkCompletionTracker.Reset();
		MassTransitBenchmarkCompletionTracker.Reset();

		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddBenchmarkDispatch();

		_dispatchQueueBus = new DispatchQueuedParityMessageBus();
		_ = dispatchServices.AddRemoteMessageBus("dispatch-queued", _ => _dispatchQueueBus);

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

		_wolverineHost = await Host.CreateDefaultBuilder()
			.UseWolverine(opts =>
			{
				_ = opts.LocalQueueFor<WolverineCommandMessage>();
				_ = opts.LocalQueueFor<WolverineEventMessage>();

				opts.Discovery.IncludeAssembly(typeof(TransportQueueParityComparisonBenchmarks).Assembly);
				opts.Discovery.IncludeType(typeof(WolverineCommandHandler));
				opts.Discovery.IncludeType(typeof(WolverineEventHandler));
				opts.Discovery.IncludeType(typeof(WolverineEventHandler2));
			})
			.StartAsync()
			.ConfigureAwait(false);
		_wolverineBus = _wolverineHost.Services.GetRequiredService<WolverineMessageBus>();

		var massTransitServices = new ServiceCollection();
		_ = massTransitServices.AddMassTransit(x =>
		{
			_ = x.AddConsumer<MassTransitCommandConsumer>();
			_ = x.AddConsumer<MassTransitEventConsumer1>();
			_ = x.AddConsumer<MassTransitEventConsumer2>();
			x.UsingInMemory((context, cfg) => cfg.ConfigureEndpoints(context));
		});

		_massTransitServiceProvider = massTransitServices.BuildServiceProvider();
		_massTransitBusControl = _massTransitServiceProvider.GetRequiredService<IBusControl>();
		await _massTransitBusControl.StartAsync(CancellationToken.None).ConfigureAwait(false);
		_massTransitBus = _massTransitBusControl;
	}

	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_dispatchServiceProvider is IDisposable dispatchDisposable)
		{
			dispatchDisposable.Dispose();
		}

		if (_wolverineHost is not null)
		{
			await _wolverineHost.StopAsync().ConfigureAwait(false);
			_wolverineHost.Dispose();
		}

		if (_massTransitBusControl is not null)
		{
			await _massTransitBusControl.StopAsync(CancellationToken.None).ConfigureAwait(false);
		}

		if (_massTransitServiceProvider is IAsyncDisposable massTransitAsyncDisposable)
		{
			await massTransitAsyncDisposable.DisposeAsync().ConfigureAwait(false);
		}
		else if (_massTransitServiceProvider is IDisposable massTransitDisposable)
		{
			massTransitDisposable.Dispose();
		}

		DispatchQueuedBenchmarkCompletionTracker.Reset();
		WolverineBenchmarkCompletionTracker.Reset();
		MassTransitBenchmarkCompletionTracker.Reset();
	}

	[Benchmark(Baseline = true, Description = "Dispatch (remote): queued command end-to-end")]
	public async Task Dispatch_QueuedCommand_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = DispatchQueuedBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1);
		var command = new DispatchQueuedCommandMessage
		{
			Value = 42,
			BenchmarkId = benchmarkId,
		};

		_ = await DispatchWithRouteAsync(command, DispatchQueuedRoutingDecision).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine: queued command end-to-end (SendAsync)")]
	public async Task Wolverine_QueuedCommand_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = WolverineBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1);
		var command = new WolverineCommandMessage
		{
			Value = 42,
			BenchmarkId = benchmarkId,
		};

		await _wolverineBus!.SendAsync(command).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit: queued command end-to-end (Publish)")]
	public async Task MassTransit_QueuedCommand_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1);
		var command = new MassTransitCommandMessage
		{
			Value = 42,
			BenchmarkId = benchmarkId,
		};

		await _massTransitBus!.Publish(command, CancellationToken.None).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (remote): queued event fan-out end-to-end")]
	public async Task Dispatch_QueuedEventFanOut_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = DispatchQueuedBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var evt = new DispatchQueuedEventMessage
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};

		_ = await DispatchWithRouteAsync(evt, DispatchQueuedRoutingDecision).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine: queued event fan-out end-to-end (PublishAsync)")]
	public async Task Wolverine_QueuedEventFanOut_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = WolverineBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var evt = new WolverineEventMessage
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};

		await _wolverineBus!.PublishAsync(evt).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit: queued event fan-out end-to-end (Publish)")]
	public async Task MassTransit_QueuedEventFanOut_EndToEnd()
	{
		var benchmarkId = Guid.NewGuid();
		var completionTask = MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var evt = new MassTransitEventMessage
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};

		await _massTransitBus!.Publish(evt, CancellationToken.None).ConfigureAwait(false);
		await completionTask.WaitAsync(QueueCompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (remote): queued commands end-to-end (10 concurrent)")]
	public async Task Dispatch_QueuedCommand_Concurrent10_EndToEnd()
	{
		var dispatchTasks = new List<Task<IMessageResult>>(10);
		var completionTasks = new List<Task>(10);
		for (var i = 0; i < 10; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(DispatchQueuedBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			var command = new DispatchQueuedCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			};
			dispatchTasks.Add(DispatchWithRouteAsync(command, DispatchQueuedRoutingDecision));
		}

		_ = await Task.WhenAll(dispatchTasks).ConfigureAwait(false);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout))).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine: queued commands end-to-end (10 concurrent)")]
	public async Task Wolverine_QueuedCommand_Concurrent10_EndToEnd()
	{
		var publishTasks = new List<Task>(10);
		var completionTasks = new List<Task>(10);
		for (var i = 0; i < 10; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(WolverineBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			var command = new WolverineCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			};
			publishTasks.Add(_wolverineBus!.SendAsync(command).AsTask());
		}

		await Task.WhenAll(publishTasks).ConfigureAwait(false);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout))).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit: queued commands end-to-end (10 concurrent)")]
	public async Task MassTransit_QueuedCommand_Concurrent10_EndToEnd()
	{
		var publishTasks = new List<Task>(10);
		var completionTasks = new List<Task>(10);
		for (var i = 0; i < 10; i++)
		{
			var benchmarkId = Guid.NewGuid();
			completionTasks.Add(MassTransitBenchmarkCompletionTracker.Register(benchmarkId, expectedSignals: 1));
			var command = new MassTransitCommandMessage
			{
				Value = i,
				BenchmarkId = benchmarkId,
			};
			publishTasks.Add(_massTransitBus!.Publish(command, CancellationToken.None));
		}

		await Task.WhenAll(publishTasks).ConfigureAwait(false);
		await Task.WhenAll(completionTasks.Select(task => task.WaitAsync(QueueCompletionTimeout))).ConfigureAwait(false);
	}

	private async Task<IMessageResult> DispatchWithRouteAsync(IDispatchMessage message, RoutingDecision routingDecision)
	{
		var dispatcher = _dispatcher!;
		var contextFactory = _contextFactory!;
		var context = contextFactory.CreateContext();
		context.RoutingDecision = routingDecision;

		try
		{
			return await dispatcher.DispatchAsync(message, context, CancellationToken.None).ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
	}
}

public sealed record DispatchQueuedCommandMessage : IDispatchAction
{
	public int Value { get; init; }
	public Guid BenchmarkId { get; init; }
}

public sealed record DispatchQueuedEventMessage : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
	public Guid BenchmarkId { get; init; }
}

internal sealed class DispatchQueuedParityMessageBus : DispatchMessageBus
{
	public Task PublishAsync(IDispatchAction action, DispatchMessageContext context, CancellationToken cancellationToken)
	{
		if (action is DispatchQueuedCommandMessage command)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				static (DispatchQueuedCommandMessage message) =>
				{
					_ = message.Value * 2;
					DispatchQueuedBenchmarkCompletionTracker.Signal(message.BenchmarkId);
				},
				command,
				preferLocal: true);
		}

		return Task.CompletedTask;
	}

	public Task PublishAsync(IDispatchEvent evt, DispatchMessageContext context, CancellationToken cancellationToken)
	{
		if (evt is DispatchQueuedEventMessage dispatchEvent)
		{
			ThreadPool.UnsafeQueueUserWorkItem(
				static (DispatchQueuedEventMessage message) =>
				{
					DispatchQueuedBenchmarkCompletionTracker.Signal(message.BenchmarkId);
					DispatchQueuedBenchmarkCompletionTracker.Signal(message.BenchmarkId);
				},
				dispatchEvent,
				preferLocal: true);
		}

		return Task.CompletedTask;
	}

	public Task PublishAsync(IDispatchDocument doc, DispatchMessageContext context, CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}

internal static class DispatchQueuedBenchmarkCompletionTracker
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

		if (Pending.TryGetValue(benchmarkId, out var pending) && pending.Signal())
		{
			_ = Pending.TryRemove(benchmarkId, out _);
		}
	}

	public static void Reset()
	{
		Pending.Clear();
	}
}

#pragma warning restore SA1402 // File may only contain a single type
