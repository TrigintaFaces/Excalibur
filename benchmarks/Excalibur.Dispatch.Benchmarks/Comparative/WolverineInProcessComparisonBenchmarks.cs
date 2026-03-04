// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using System.Collections.Concurrent;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

using DispatchContext = Excalibur.Dispatch.Abstractions.IMessageContext;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// In-process parity benchmarks: Dispatch vs Wolverine (InvokeAsync/local in-process only).
/// </summary>
/// <remarks>
/// Dispatch uses lean AddDispatch() (no cache/dedupe/outbox middleware) for fair
/// comparison against Wolverine's InvokeAsync (bare in-process handler call).
/// Fresh context per iteration, warmup + freeze for production-representative numbers.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class WolverineInProcessComparisonBenchmarks
{
	private static readonly TimeSpan CompletionTimeout = TimeSpan.FromSeconds(5);

	// Excalibur infrastructure — standard (lean) path
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// Excalibur infrastructure — direct-local path (no middleware)
	private IServiceProvider? _dispatchDirectServiceProvider;
	private IDirectLocalDispatcher? _directLocalDispatcher;

	// Wolverine infrastructure
	private IHost? _wolverineHost;
	private IMessageBus? _wolverineBus;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		WolverineInProcessCompletionTracker.Reset();

		// Setup Excalibur — lean default (no cache/dedupe/outbox)
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddDispatch();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchCommand>, WolverineInProcessDispatchCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchQuery, int>, WolverineInProcessDispatchQueryHandler>();

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
				options.Features.EnableCacheMiddleware = false;
				options.Features.EnableMetrics = false;
				options.Features.EnableAuthorization = false;
				options.Features.ValidateMessageSchemas = false;
				options.Features.EnableVersioning = false;
				options.Features.EnableMultiTenancy = false;
				options.Features.EnableTransactions = false;
			});
		});
		_ = directDispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchCommand>, WolverineInProcessDispatchCommandHandler>();
		_ = directDispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler1>();
		_ = directDispatchServices.AddTransient<IEventHandler<WolverineInProcessDispatchEvent>, WolverineInProcessDispatchEventHandler2>();
		_ = directDispatchServices.AddTransient<IActionHandler<WolverineInProcessDispatchQuery, int>, WolverineInProcessDispatchQueryHandler>();

		_dispatchDirectServiceProvider = directDispatchServices.BuildServiceProvider();
		_directLocalDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;

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

		// Warm and freeze Dispatch caches so benchmark reflects optimized production mode.
		WarmupAndFreezeDispatchCaches();
	}

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

		if (_wolverineHost is not null)
		{
			await _wolverineHost.StopAsync().ConfigureAwait(false);
			_wolverineHost.Dispose();
		}

		WolverineInProcessCompletionTracker.Reset();
	}

	// ============================================================================
	// Single Command
	// ============================================================================

	[Benchmark(Baseline = true, Description = "Dispatch (local): Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new WolverineInProcessDispatchCommand { Value = 42 };
		return await DispatchWithFreshContextAsync(command).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (ultra-local): Single command")]
	public async Task Dispatch_SingleCommand_UltraLocal()
	{
		var command = new WolverineInProcessDispatchCommand { Value = 42 };
		await _directLocalDispatcher!.DispatchLocalAsync(command, CancellationToken.None).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine (in-process): Single command InvokeAsync")]
	public async Task Wolverine_SingleCommandInvoke()
	{
		var command = new WolverineInProcessCommand { Value = 42 };
		await _wolverineBus.InvokeAsync(command, CancellationToken.None);
	}

	// ============================================================================
	// Notification / Event Fan-Out
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Notification to 2 handlers")]
	public async Task<IMessageResult> Dispatch_NotificationTwoHandlers()
	{
		var evt = new WolverineInProcessDispatchEvent { Message = "test" };
		return await DispatchWithFreshContextAsync(evt).ConfigureAwait(false);
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

	// ============================================================================
	// Query with Return Value
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Query with return")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturn()
	{
		var query = new WolverineInProcessDispatchQuery { Id = 123 };
		return await DispatchWithFreshContextTypedAsync<WolverineInProcessDispatchQuery, int>(query).ConfigureAwait(false);
	}

	[Benchmark(Description = "Wolverine (in-process): Query with return InvokeAsync")]
	public async Task<int> Wolverine_QueryWithReturn()
	{
		var query = new WolverineInProcessQuery { Id = 123 };
		return await _wolverineBus.InvokeAsync<int>(query, CancellationToken.None);
	}

	// ============================================================================
	// Concurrent Commands
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): 10 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new Task<IMessageResult>[10];
		for (int i = 0; i < 10; i++)
		{
			tasks[i] = DispatchWithFreshContextAsync(
				new WolverineInProcessDispatchCommand { Value = i });
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
		var tasks = new Task<IMessageResult>[100];
		for (int i = 0; i < 100; i++)
		{
			tasks[i] = DispatchWithFreshContextAsync(
				new WolverineInProcessDispatchCommand { Value = i });
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

	// ============================================================================
	// Helper Methods
	// ============================================================================

	private void WarmupAndFreezeDispatchCaches()
	{
		_ = DispatchWithFreshContextAsync(new WolverineInProcessDispatchCommand { Value = 1 })
			.GetAwaiter().GetResult();
		_ = DispatchWithFreshContextAsync(new WolverineInProcessDispatchEvent { Message = "warmup" })
			.GetAwaiter().GetResult();

		if (_directLocalDispatcher is not null)
		{
			_directLocalDispatcher.DispatchLocalAsync(new WolverineInProcessDispatchCommand { Value = 1 }, CancellationToken.None)
				.AsTask().GetAwaiter().GetResult();
		}

		HandlerInvoker.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();
		HandlerActivator.FreezeCache();
		FinalDispatchHandler.FreezeResultFactoryCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();
	}

	private async Task<IMessageResult> DispatchWithFreshContextAsync<TMessage>(TMessage message)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_dispatchContextFactory);

		var context = _dispatchContextFactory.CreateContext();
		var dispatchTask = _dispatcher.DispatchAsync(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				_dispatchContextFactory.Return(context);
			}
		}

		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			_dispatchContextFactory.Return(context);
		}
	}

	private async Task<IMessageResult<TResponse>> DispatchWithFreshContextTypedAsync<TMessage, TResponse>(TMessage message)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_dispatchContextFactory);

		var context = _dispatchContextFactory.CreateContext();
		var dispatchTask = _dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				_dispatchContextFactory.Return(context);
			}
		}

		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			_dispatchContextFactory.Return(context);
		}
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
