// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using MassTransit;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// In-process parity benchmarks: Dispatch local path vs MassTransit Mediator path.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MassTransitMediatorComparisonBenchmarks
{
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContext? _dispatchContext;

	private IServiceProvider? _mediatorServiceProvider;
	private IServiceScope? _mediatorScope;
	private MassTransit.Mediator.IScopedMediator? _mediator;

	[GlobalSetup]
	public void GlobalSetup()
	{
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddBenchmarkDispatch();
		_ = dispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchCommand>, MassTransitMediatorDispatchCommandHandler>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler1>();
		_ = dispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler2>();
		_ = dispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchQuery, int>, MassTransitMediatorDispatchQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		var contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();
		_dispatchContext = contextFactory.CreateContext();

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

	[Benchmark(Baseline = true, Description = "Dispatch (local): Single command")]
	public async Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new MassTransitMediatorDispatchCommand { Value = 42 };
		return await _dispatcher.DispatchAsync(command, _dispatchContext, CancellationToken.None);
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

	[Benchmark(Description = "Dispatch (local): Notification to 2 handlers")]
	public async Task<IMessageResult> Dispatch_NotificationTwoHandlers()
	{
		var evt = new MassTransitMediatorDispatchEvent { Message = "test" };
		return await _dispatcher.DispatchAsync(evt, _dispatchContext, CancellationToken.None);
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

	[Benchmark(Description = "Dispatch (local): Query with return")]
	public async Task<IMessageResult<int>> Dispatch_QueryWithReturn()
	{
		var query = new MassTransitMediatorDispatchQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<MassTransitMediatorDispatchQuery, int>(query, _dispatchContext, CancellationToken.None);
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

	[Benchmark(Description = "Dispatch (local): 10 concurrent commands")]
	public async Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new List<Task<IMessageResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			tasks.Add(_dispatcher.DispatchAsync(
				new MassTransitMediatorDispatchCommand { Value = i },
				_dispatchContext,
				CancellationToken.None));
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
		var tasks = new List<Task<IMessageResult>>(100);
		for (int i = 0; i < 100; i++)
		{
			tasks.Add(_dispatcher.DispatchAsync(
				new MassTransitMediatorDispatchCommand { Value = i },
				_dispatchContext,
				CancellationToken.None));
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
