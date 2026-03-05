// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// In-process parity benchmarks: Dispatch local path vs MassTransit Mediator path.
/// </summary>
/// <remarks>
/// Dispatch uses lean AddDispatch() (no cache/dedupe/outbox middleware) for fair
/// comparison against MassTransit Mediator's in-process execution.
/// Fresh context per iteration, warmup + freeze for production-representative numbers.
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MassTransitMediatorComparisonBenchmarks
{
	// Excalibur infrastructure
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// MassTransit Mediator infrastructure
	private IServiceProvider? _mediatorServiceProvider;
	private IServiceScope? _mediatorScope;
	private MassTransit.Mediator.IScopedMediator? _mediator;

	[GlobalSetup]
	public void GlobalSetup()
	{
		// Setup Excalibur — lean default (no cache/dedupe/outbox)
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddDispatch();
		_ = dispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchCommand>, MassTransitMediatorDispatchCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchQuery, int>, MassTransitMediatorDispatchQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		_dispatchContextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

		// Setup MassTransit Mediator
		var mediatorServices = new ServiceCollection();
		_ = mediatorServices.AddMediator(cfg =>
		{
			_ = cfg.AddConsumer<MassTransitMediatorCommandConsumer>();
			_ = cfg.AddConsumer<MassTransitMediatorEventConsumer1>();
			_ = cfg.AddConsumer<MassTransitMediatorEventConsumer2>();
			_ = cfg.AddConsumer<MassTransitMediatorQueryConsumer>();
			cfg.AddRequestClient<MassTransitMediatorQueryMessage>();
		});

		_mediatorServiceProvider = mediatorServices.BuildServiceProvider();
		_mediatorScope = _mediatorServiceProvider.CreateScope();
		_mediator = _mediatorScope.ServiceProvider.GetRequiredService<MassTransit.Mediator.IScopedMediator>();

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

		if (_mediatorScope is IAsyncDisposable mediatorAsyncScope)
		{
			await mediatorAsyncScope.DisposeAsync().ConfigureAwait(false);
		}
		else
		{
			_mediatorScope?.Dispose();
		}

		if (_mediatorServiceProvider is IAsyncDisposable mediatorAsyncProvider)
		{
			await mediatorAsyncProvider.DisposeAsync().ConfigureAwait(false);
		}
		else if (_mediatorServiceProvider is IDisposable mediatorDisposable)
		{
			mediatorDisposable.Dispose();
		}
	}

	// ============================================================================
	// Single Command
	// ============================================================================

	[Benchmark(Baseline = true, Description = "Dispatch (local): Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new MassTransitMediatorDispatchCommand { Value = 42 };
		return await DispatchWithFreshContextAsync(command).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): Single command")]
	public async Task MassTransitMediator_SingleCommand()
	{
		var command = new MassTransitMediatorCommandMessage
		{
			Value = 42,
		};

		await _mediator.Publish(command, CancellationToken.None).ConfigureAwait(false);
	}

	// ============================================================================
	// Notification / Event Fan-Out
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Notification to 2 handlers")]
	public async Task<IMessageResult> Dispatch_NotificationTwoHandlers()
	{
		var evt = new MassTransitMediatorDispatchEvent { Message = "test" };
		return await DispatchWithFreshContextAsync(evt).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): Notification to 2 consumers")]
	public async Task MassTransitMediator_NotificationTwoConsumers()
	{
		var evt = new MassTransitMediatorEventMessage
		{
			Message = "test",
		};

		await _mediator.Publish(evt, CancellationToken.None).ConfigureAwait(false);
	}

	// ============================================================================
	// Query with Return Value
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Query with return")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturn()
	{
		var query = new MassTransitMediatorDispatchQuery { Id = 123 };
		return await DispatchWithFreshContextTypedAsync<MassTransitMediatorDispatchQuery, int>(query).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): Query with return")]
	public async Task<int> MassTransitMediator_QueryWithReturn()
	{
		var queryClient = _mediator.CreateRequestClient<MassTransitMediatorQueryMessage>();
		var response = await queryClient.GetResponse<MassTransitMediatorQueryResponse>(
			new MassTransitMediatorQueryMessage { Id = 123 },
			CancellationToken.None)
			.ConfigureAwait(false);
		return response.Message.Value;
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
				new MassTransitMediatorDispatchCommand { Value = i });
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): 10 concurrent commands")]
	public async Task MassTransitMediator_ConcurrentCommands10()
	{
		var publishTasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			publishTasks.Add(_mediator.Publish(
				new MassTransitMediatorCommandMessage { Value = i },
				CancellationToken.None));
		}

		await Task.WhenAll(publishTasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "Dispatch (local): 100 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new Task<IMessageResult>[100];
		for (int i = 0; i < 100; i++)
		{
			tasks[i] = DispatchWithFreshContextAsync(
				new MassTransitMediatorDispatchCommand { Value = i });
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): 100 concurrent commands")]
	public async Task MassTransitMediator_ConcurrentCommands100()
	{
		var publishTasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			publishTasks.Add(_mediator.Publish(
				new MassTransitMediatorCommandMessage { Value = i },
				CancellationToken.None));
		}

		await Task.WhenAll(publishTasks).ConfigureAwait(false);
	}

	// ============================================================================
	// Helper Methods
	// ============================================================================

	private void WarmupAndFreezeDispatchCaches()
	{
		_ = DispatchWithFreshContextAsync(new MassTransitMediatorDispatchCommand { Value = 1 })
			.GetAwaiter().GetResult();
		_ = DispatchWithFreshContextAsync(new MassTransitMediatorDispatchEvent { Message = "warmup" })
			.GetAwaiter().GetResult();

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

public record MassTransitMediatorDispatchCommand : IDispatchAction
{
	public int Value { get; init; }
}

public class MassTransitMediatorDispatchCommandHandler : IActionHandler<MassTransitMediatorDispatchCommand>
{
	public Task HandleAsync(MassTransitMediatorDispatchCommand message, CancellationToken cancellationToken)
	{
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

public record MassTransitMediatorDispatchEvent : IDispatchEvent
{
	public string Message { get; init; } = string.Empty;
}

public class MassTransitMediatorDispatchEventHandler1 : IEventHandler<MassTransitMediatorDispatchEvent>
{
	public Task HandleAsync(MassTransitMediatorDispatchEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public class MassTransitMediatorDispatchEventHandler2 : IEventHandler<MassTransitMediatorDispatchEvent>
{
	public Task HandleAsync(MassTransitMediatorDispatchEvent message, CancellationToken cancellationToken) => Task.CompletedTask;
}

public record MassTransitMediatorDispatchQuery : IDispatchAction<int>
{
	public int Id { get; init; }
}

public class MassTransitMediatorDispatchQueryHandler : IActionHandler<MassTransitMediatorDispatchQuery, int>
{
	public Task<int> HandleAsync(MassTransitMediatorDispatchQuery message, CancellationToken cancellationToken)
		=> Task.FromResult(message.Id * 2);
}

public record MassTransitMediatorCommandMessage
{
	public int Value { get; set; }
}

public class MassTransitMediatorCommandConsumer : IConsumer<MassTransitMediatorCommandMessage>
{
	public Task Consume(ConsumeContext<MassTransitMediatorCommandMessage> context)
	{
		_ = context.Message.Value * 2;
		return Task.CompletedTask;
	}
}

public record MassTransitMediatorEventMessage
{
	public string Message { get; set; } = string.Empty;
}

public class MassTransitMediatorEventConsumer1 : IConsumer<MassTransitMediatorEventMessage>
{
	public Task Consume(ConsumeContext<MassTransitMediatorEventMessage> context) => Task.CompletedTask;
}

public class MassTransitMediatorEventConsumer2 : IConsumer<MassTransitMediatorEventMessage>
{
	public Task Consume(ConsumeContext<MassTransitMediatorEventMessage> context) => Task.CompletedTask;
}

public record MassTransitMediatorQueryMessage
{
	public int Id { get; set; }
}

public record MassTransitMediatorQueryResponse
{
	public int Value { get; set; }
}

public class MassTransitMediatorQueryConsumer : IConsumer<MassTransitMediatorQueryMessage>
{
	public Task Consume(ConsumeContext<MassTransitMediatorQueryMessage> context)
	{
		return context.RespondAsync(new MassTransitMediatorQueryResponse { Value = context.Message.Id * 2 });
	}
}

#pragma warning restore SA1402 // File may only contain a single type
