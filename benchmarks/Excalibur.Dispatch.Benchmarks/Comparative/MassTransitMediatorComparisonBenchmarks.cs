// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Benchmarks.Infrastructure;
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
/// Both frameworks expose multiple configurations, which are reported separately.
/// </para>
/// <para>
/// The <c>IScopedMediator</c> row reuses a scope created once outside the measured region.
/// The plain <c>IMediator</c> row exercises the scope-per-message configuration. Their timing
/// difference includes all behavior of those entry points and does not isolate pure scope cost.
/// </para>
/// <para>
/// Dispatch reports its standard <c>AddDispatch()</c> path and a separately configured direct-local
/// path. These settings are disclosed rather than assumed to have identical behavior or latency.
/// </para>
/// <para>
/// Consumers should read the row whose configuration matches their own, which is why each row names its
/// configuration rather than leaving the reader to assume parity.
/// </para>
/// </remarks>
/// Allocation is NOT measured under this config. It runs one invocation per iteration, so an
/// Allocated column would report per-iteration setup rather than per-operation cost -- measured at a
/// ~30% swing across identical consecutive runs, which is not a figure anyone can publish or gate on.
/// The warm-path subclass carries the memory diagnoser instead, where the number resolves.
[Config(typeof(ComparativeBenchmarkConfig))]
public class MassTransitMediatorComparisonBenchmarks
{
	// Excalibur infrastructure — standard AddDispatch() path
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// Excalibur infrastructure — tuned direct-local path (no middleware chain)
	private IServiceProvider? _dispatchDirectServiceProvider;
	private IDispatcher? _contextLessDispatcher;

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
		_contextLessDispatcher = _dispatchDirectServiceProvider.GetRequiredService<IDispatcher>();

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

		// IScopedMediator reuses a scope created once during setup; this is a different
		// scope-lifetime configuration from the IMediator entry point below.
		_mediatorScope = _mediatorServiceProvider.CreateScope();
		_mediator = _mediatorScope.ServiceProvider.GetRequiredService<MassTransit.Mediator.IScopedMediator>();

		// Plain IMediator is resolved from the root and exercises its scope-per-message configuration.
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
	public Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new MassTransitMediatorDispatchCommand { Value = 42 };
		return DispatchWithFreshContextAsync(command);
	}

	[Benchmark(Description = "MassTransit Mediator (ambient scope): Single command")]
	public Task MassTransitMediator_SingleCommand()
	{
		var command = new MassTransitMediatorCommandMessage
		{
			Value = 42,
		};

		return _mediator.Publish(command, CancellationToken.None);
	}

	/// <summary>
	/// MassTransit Mediator via plain <c>IMediator</c>, which creates a DI scope per message.
	/// </summary>
	/// <remarks>
	/// The row above uses <c>IScopedMediator</c> and reuses a scope created once during setup;
	/// this is a different scope-lifetime configuration. This row uses the plain
	/// <c>IMediator</c> entry point. Compare the configuration matching the application’s usage;
	/// the timing difference includes all entry-point behavior and does not isolate pure scope cost.
	/// </remarks>
	[Benchmark(Description = "MassTransit Mediator (scope per message): Single command")]
	public Task MassTransitMediator_SingleCommand_ScopePerMessage()
	{
		var command = new MassTransitMediatorCommandMessage
		{
			Value = 42,
		};

		return _scopePerMessageMediator!.Publish(command, CancellationToken.None);
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
	public Task<IMessageResult> Dispatch_SingleCommand_DirectLocal()
	{
		var command = new MassTransitMediatorDispatchCommand { Value = 42 };
		return _contextLessDispatcher!.DispatchAsync(command, CancellationToken.None);
	}

	// ============================================================================
	// Notification / Event Fan-Out
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Notification to 2 handlers")]
	public Task<IMessageResult> Dispatch_NotificationTwoHandlers()
	{
		var evt = new MassTransitMediatorDispatchEvent { Message = "test" };
		return DispatchWithFreshContextAsync(evt);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): Notification to 2 consumers")]
	public Task MassTransitMediator_NotificationTwoConsumers()
	{
		var evt = new MassTransitMediatorEventMessage
		{
			Message = "test",
		};

		return _mediator.Publish(evt, CancellationToken.None);
	}

	// ============================================================================
	// Query with Return Value
	// ============================================================================

	[Benchmark(Description = "Dispatch (local): Query with return")]
	public Task<IMessageResult<int>> Dispatch_QueryWithReturn()
	{
		var query = new MassTransitMediatorDispatchQuery { Id = 123 };
		return DispatchWithFreshContextTypedAsync<MassTransitMediatorDispatchQuery, int>(query);
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
	public Task Dispatch_ConcurrentCommands10()
	{
		var tasks = new Task<IMessageResult>[10];
		for (int i = 0; i < 10; i++)
		{
			tasks[i] = DispatchWithFreshContextAsync(
				new MassTransitMediatorDispatchCommand { Value = i });
		}

		return Task.WhenAll(tasks);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): 10 concurrent commands")]
	public Task MassTransitMediator_ConcurrentCommands10()
	{
		var publishTasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			publishTasks.Add(_mediator.Publish(
				new MassTransitMediatorCommandMessage { Value = i },
				CancellationToken.None));
		}

		return Task.WhenAll(publishTasks);
	}

	[Benchmark(Description = "Dispatch (local): 100 concurrent commands")]
	public Task Dispatch_ConcurrentCommands100()
	{
		var tasks = new Task<IMessageResult>[100];
		for (int i = 0; i < 100; i++)
		{
			tasks[i] = DispatchWithFreshContextAsync(
				new MassTransitMediatorDispatchCommand { Value = i });
		}

		return Task.WhenAll(tasks);
	}

	[Benchmark(Description = "MassTransit Mediator (in-process): 100 concurrent commands")]
	public Task MassTransitMediator_ConcurrentCommands100()
	{
		var publishTasks = new List<Task>(100);
		for (int i = 0; i < 100; i++)
		{
			publishTasks.Add(_mediator.Publish(
				new MassTransitMediatorCommandMessage { Value = i },
				CancellationToken.None));
		}

		return Task.WhenAll(publishTasks);
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

	// Deliberately NOT async, and neither are the single-call benchmark methods that use it. An async
	// frame whose result is a non-null reference allocates a Task<T> (AsyncTaskMethodBuilder<T> only
	// caches the default-result task), so an async helper plus an async benchmark body charged the
	// Dispatch arm ~144 B that a competitor arm returning its own library Task never paid. Returning
	// the dispatcher's own Task preserves fresh-context-per-invocation while keeping the harness frame
	// count at zero for every arm, so the allocation column measures the library, not the harness.
	private Task<IMessageResult> DispatchWithFreshContextAsync<TMessage>(TMessage message)
		where TMessage : IDispatchMessage
	{
		return FreshContextBenchmarkDispatch.DispatchAsync<TMessage>(_dispatcher, _dispatchContextFactory, message);
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
		return FreshContextBenchmarkDispatch.DispatchAsync<TMessage, TResponse>(_dispatcher, _dispatchContextFactory, message);
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
