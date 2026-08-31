// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Configuration;
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
/// <para>
/// Framework Versions:
/// - Excalibur: 1.0.0 (local build)
/// - MassTransit: 8.5.9
/// </para>
/// <para>
/// BOTH SIDES PUBLISH TWO TIERS, because each framework's idiomatic usage spans two shapes and
/// publishing only one of each would compare a tuned configuration against an untuned one.
/// </para>
/// <para>
/// MassTransit exposes two mediator entry points with different scope behaviour, and the difference is
/// measurable: <c>IScopedMediator</c> reuses the ambient scope, so a scope created once outside the
/// measured region serves every message; plain <c>IMediator</c> creates a DI scope per message. Measured
/// with a scoped dependency and two publishes: <c>IScopedMediator</c> yields the same instance both
/// times, plain <c>IMediator</c> yields distinct instances. Measuring only <c>IScopedMediator</c> lifts
/// MassTransit's per-message scope cost out of the comparison — which flatters MassTransit, not us, but
/// is still not the default shape a consumer gets.
/// </para>
/// <para>
/// Dispatch likewise publishes its standard <c>AddDispatch()</c> path and its tuned direct-local path,
/// matching what the MediatR, Wolverine and NServiceBus pairings already did. These two classes were
/// previously the only comparisons lacking the tuned tier, so MassTransit was being compared against
/// Dispatch's untuned path alone.
/// </para>
/// <para>
/// Consumers should read the row whose configuration matches their own, which is why each row names its
/// configuration rather than leaving the reader to assume parity.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MassTransitMediatorComparisonBenchmarks
{
	// Excalibur infrastructure — standard AddDispatch() path
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// Excalibur infrastructure — tuned direct-local path (no middleware chain)
	private IServiceProvider? _dispatchDirectServiceProvider;
	private IDirectLocalDispatcher? _directLocalDispatcher;

	// MassTransit Mediator infrastructure — IScopedMediator, reuses one ambient scope
	private IServiceProvider? _mediatorServiceProvider;
	private IServiceScope? _mediatorScope;
	private MassTransit.Mediator.IScopedMediator? _mediator;

	// MassTransit Mediator infrastructure — plain IMediator, creates a scope PER MESSAGE
	private MassTransit.Mediator.IMediator? _scopePerMessageMediator;

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

		// Setup Excalibur — tuned direct-local, mirroring the MediatR/Wolverine pairings exactly so the
		// tuned tier means the same thing in every comparison.
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
		_ = directDispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchCommand>, MassTransitMediatorDispatchCommandHandler>();
		_ = directDispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler1>();
		_ = directDispatchServices.AddTransient<IEventHandler<MassTransitMediatorDispatchEvent>, MassTransitMediatorDispatchEventHandler2>();
		_ = directDispatchServices.AddTransient<IActionHandler<MassTransitMediatorDispatchQuery, int>, MassTransitMediatorDispatchQueryHandler>();

		_dispatchDirectServiceProvider = directDispatchServices.BuildServiceProvider();
		_directLocalDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;

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

		// Tier 1: IScopedMediator resolved from a scope created ONCE. Reuses that ambient scope for every
		// message, so MassTransit's per-message scope cost does not appear in the measured region.
		_mediatorScope = _mediatorServiceProvider.CreateScope();
		_mediator = _mediatorScope.ServiceProvider.GetRequiredService<MassTransit.Mediator.IScopedMediator>();

		// Tier 2: plain IMediator, resolved from the root. Creates a DI scope PER MESSAGE, which is the
		// default standalone shape and the one that carries the scope cost.
		_scopePerMessageMediator = _mediatorServiceProvider.GetRequiredService<MassTransit.Mediator.IMediator>();

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

	[Benchmark(Description = "MassTransit Mediator (ambient scope): Single command")]
	public async Task MassTransitMediator_SingleCommand()
	{
		var command = new MassTransitMediatorCommandMessage
		{
			Value = 42,
		};

		await _mediator.Publish(command, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// MassTransit Mediator via plain <c>IMediator</c>, which creates a DI scope per message.
	/// </summary>
	/// <remarks>
	/// The row above uses <c>IScopedMediator</c> against a scope built once in setup, so MassTransit's
	/// per-message scope creation happens outside the measured region. This row uses the plain
	/// <c>IMediator</c> that a standalone consumer gets, which opens a scope for every message. The delta
	/// between the two rows IS MassTransit's per-message scope cost, and publishing only the cheaper one
	/// would understate what a default consumer pays.
	/// </remarks>
	[Benchmark(Description = "MassTransit Mediator (scope per message): Single command")]
	public async Task MassTransitMediator_SingleCommand_ScopePerMessage()
	{
		var command = new MassTransitMediatorCommandMessage
		{
			Value = 42,
		};

		await _scopePerMessageMediator!.Publish(command, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Dispatch via the tuned direct-local path, matching the tuned tier the other pairings publish.
	/// </summary>
	/// <remarks>
	/// Until this was added, the two MassTransit classes were the only comparisons without a tuned Dispatch
	/// tier, so MassTransit was measured against Dispatch's standard path while MediatR, Wolverine and
	/// NServiceBus were measured against both. That made the MassTransit summary conservative rather than
	/// wrong, but it was undeclared and not comparable across pairings.
	/// </remarks>
	[Benchmark(Description = "Dispatch (tuned direct-local): Single command")]
	public async Task Dispatch_SingleCommand_DirectLocal()
	{
		var command = new MassTransitMediatorDispatchCommand { Value = 42 };
		await _directLocalDispatcher!.DispatchLocalAsync(command, CancellationToken.None).ConfigureAwait(false);
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