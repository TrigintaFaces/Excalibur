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
/// In-process parity benchmarks: Dispatch vs Wolverine (InvokeAsync/local in-process only).
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class WolverineInProcessComparisonBenchmarks
{
	private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private DispatchContext? _dispatchContext;

	private IHost? _wolverineHost;
	private IMessageBus? _wolverineBus;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		WolverineInProcessCompletionTracker.Reset();

		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddBenchmarkDispatch();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchCommand>, WolverineInProcessDispatchCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchQuery, int>, WolverineInProcessDispatchQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();
		_dispatchContext = contextFactory.CreateContext();

		_wolverineHost = await Host.CreateDefaultBuilder()
			.UseWolverine(opts =>
			{
				opts.Discovery.IncludeAssembly(typeof(WolverineInProcessComparisonBenchmarks).Assembly);
				opts.Discovery.IncludeType(typeof(WolverineInProcessCommandHandler));
				opts.Discovery.IncludeType(typeof(WolverineInProcessEventHandler1));
				opts.Discovery.IncludeType(typeof(WolverineInProcessEventHandler2));
				opts.Discovery.IncludeType(typeof(WolverineInProcessQueryHandler));
			})
			.StartAsync()
			.ConfigureAwait(false);

		_wolverineBus = _wolverineHost.Services.GetRequiredService<IMessageBus>();
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

		WolverineInProcessCompletionTracker.Reset();
	}

	[Benchmark(Baseline = true, Description = "Dispatch (local): Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new WolverineInProcessDispatchCommand { Value = 42 };
		return await _dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None);
	}

	[Benchmark(Description = "Wolverine (in-process): Single command InvokeAsync")]
	public async Task Wolverine_SingleCommandInvoke()
	{
		var command = new WolverineInProcessCommand { Value = 42 };
		await _wolverineBus.InvokeAsync(command, CancellationToken.None);
	}

	[Benchmark(Description = "Dispatch (local): Notification to 2 handlers")]
	public async Task<IMessageResult> Dispatch_NotificationTwoHandlers()
	{
		var evt = new WolverineInProcessDispatchEvent { Message = "test" };
		return await _dispatcher.DispatchAsync(evt, _dispatchContext, CancellationToken.None);
	}

	[Benchmark(Description = "Wolverine (in-process): Notification to 2 handlers")]
	public async Task Wolverine_NotificationTwoHandlers()
	{
		var benchmarkId = Guid.NewGuid();
		var completion = WolverineInProcessCompletionTracker.Register(benchmarkId, expectedSignals: 2);
		var evt = new WolverineInProcessEvent
		{
			Message = "test",
			BenchmarkId = benchmarkId,
		};

		await _wolverineBus.PublishAsync(evt).ConfigureAwait(false);
		await completion.WaitAsync(CompletionTimeout).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (local): Query with return")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturn()
	{
		var query = new WolverineInProcessDispatchQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<WolverineInProcessDispatchQuery, int>(query, _dispatchContext, CancellationToken.None);
	}

	[Benchmark(Description = "Wolverine (in-process): Query with return InvokeAsync")]
	public async Task<int> Wolverine_QueryWithReturn()
	{
		var query = new WolverineInProcessQuery { Id = 123 };
		return await _wolverineBus.InvokeAsync<int>(query, CancellationToken.None);
	}

	[Benchmark(Description = "Dispatch (local): 10 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new List<Task<IMessageResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			tasks.Add(_dispatcher.DispatchAsync(
				new WolverineInProcessDispatchCommand { Value = i },
				_dispatchContext,
				CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine (in-process): 10 concurrent commands")]
	public async Task Wolverine_ConcurrentCommands10()
	{
		var tasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			tasks.Add(_wolverineBus.InvokeAsync(new WolverineInProcessCommand { Value = i }, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (local): 100 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new List<Task<IMessageResult>>(100);
		for (int i = 0; i < 100; i++)
		{
			tasks.Add(_dispatcher.DispatchAsync(
				new WolverineInProcessDispatchCommand { Value = i },
				_dispatchContext,
				CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine (in-process): 100 concurrent commands")]
	public async Task Wolverine_ConcurrentCommands100()
	{
		var tasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			tasks.Add(_wolverineBus.InvokeAsync(new WolverineInProcessCommand { Value = i }, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}
}

#pragma warning disable SA1402 // File may only contain a single type

public record WolverineInProcessDispatchCommand : IDispatchAction
{
	public int Value { get; init; }
}

public class WolverineInProcessDispatchCommandHandler : IActionHandler<WolverineInProcessDispatchCommand>
{
	public Task HandleAsync(WolverineInProcessDispatchCommand message, CancellationToken cancellationToken)
	{
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

public record WolverineInProcessDispatchEvent : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
}

public class WolverineInProcessDispatchEventHandler1 : IEventHandler<WolverineInProcessDispatchEvent>
{
	public Task HandleAsync(WolverineInProcessDispatchEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class WolverineInProcessDispatchEventHandler2 : IEventHandler<WolverineInProcessDispatchEvent>
{
	public Task HandleAsync(WolverineInProcessDispatchEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public record WolverineInProcessDispatchQuery : IDispatchAction<int>
{
	public int Id { get; init; }
}

public class WolverineInProcessDispatchQueryHandler : IActionHandler<WolverineInProcessDispatchQuery, int>
{
	public Task<int> HandleAsync(WolverineInProcessDispatchQuery message, CancellationToken cancellationToken)
		=> Task.FromResult(message.Id * 2);
}

public record WolverineInProcessCommand
{
	public int Value { get; set; }
}

public static class WolverineInProcessCommandHandler
{
	public static Task Handle(WolverineInProcessCommand command, CancellationToken cancellationToken)
	{
		_ = command.Value * 2;
		return Task.CompletedTask;
	}
}

public record WolverineInProcessEvent
{
	public string Message { get; set; } = string.Empty;
	public Guid BenchmarkId { get; set; }
}

public static class WolverineInProcessEventHandler1
{
	public static Task Handle(WolverineInProcessEvent evt, CancellationToken cancellationToken)
	{
		WolverineInProcessCompletionTracker.Signal(evt.BenchmarkId);
		return Task.CompletedTask;
	}
}

public static class WolverineInProcessEventHandler2
{
	public static Task Handle(WolverineInProcessEvent evt, CancellationToken cancellationToken)
	{
		WolverineInProcessCompletionTracker.Signal(evt.BenchmarkId);
		return Task.CompletedTask;
	}
}

public record WolverineInProcessQuery
{
	public int Id { get; set; }
}

public static class WolverineInProcessQueryHandler
{
	public static Task<int> Handle(WolverineInProcessQuery query, CancellationToken cancellationToken)
		=> Task.FromResult(query.Id * 2);
}

internal static class WolverineInProcessCompletionTracker
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
