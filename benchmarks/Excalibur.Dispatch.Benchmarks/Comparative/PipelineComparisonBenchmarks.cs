// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using MassTransit;

using MediatR;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Wolverine;

using IMessageContext = Excalibur.Dispatch.Abstractions.IMessageContext;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention
#pragma warning disable SA1402 // File may only contain a single type - benchmarks with supporting types

/// <summary>
/// 4-way pipeline overhead comparison: Dispatch vs MediatR vs Wolverine vs MassTransit.
/// All frameworks configured with 3 passthrough middleware/behaviors/filters.
/// </summary>
/// <remarks>
/// Measures the pure overhead of pipeline middleware infrastructure when all
/// behaviors are no-op passthroughs. This isolates framework pipeline cost
/// from actual business logic.
///
/// Dispatch: 3 IDispatchMiddleware implementations (logging, validation, timing stages)
/// MediatR: 3 IPipelineBehavior implementations (logging, validation, timing)
/// Wolverine: 3 convention-based middleware classes (Before/After methods)
/// MassTransit: Mediator with 3 IFilter&lt;ConsumeContext&gt; implementations
/// </remarks>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class PipelineComparisonBenchmarks
{
	// Excalibur infrastructure with pipeline behaviors
	private IServiceProvider? _dispatchPipelineProvider;
	private IDispatcher? _dispatchPipelineDispatcher;
	private IMessageContextFactory? _dispatchContextFactory;

	// MediatR infrastructure with pipeline behaviors
	private IServiceProvider? _mediatrPipelineProvider;
	private IMediator? _mediatrPipelineMediator;

	// Wolverine infrastructure with middleware
	private IHost? _wolverineHost;
	private IMessageBus? _wolverineBus;

	// MassTransit Mediator infrastructure with filters
	private IServiceProvider? _massTransitMediatorProvider;
	private IServiceScope? _massTransitMediatorScope;
	private MassTransit.Mediator.IScopedMediator? _massTransitMediator;

	/// <summary>
	/// Initialize all four frameworks with 3 pipeline behaviors/middleware/filters.
	/// </summary>
	[GlobalSetup]
	public async Task GlobalSetup()
	{
		// Setup Excalibur with 3 middleware behaviors
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging(); // Required for FinalDispatchHandler
		_ = dispatchServices.AddDispatch(builder =>
		{
			_ = builder.ConfigurePipeline("BenchmarkPipeline", pipeline => pipeline.UseProfile(DefaultPipelineProfiles.Default));
			_ = builder.WithOptions(options =>
			{
				options.UseLightMode = true;
				options.EnablePipelineSynthesis = false;
			});
		});
		_ = dispatchServices.AddTransient<IActionHandler<PipelineTestCommand>, PipelineTestCommandHandler>();

		// Register 3 middleware behaviors (logging, validation, timing)
		_ = dispatchServices.AddTransient<IDispatchMiddleware, PipelineLoggingMiddleware>();
		_ = dispatchServices.AddTransient<IDispatchMiddleware, PipelineValidationMiddleware>();
		_ = dispatchServices.AddTransient<IDispatchMiddleware, PipelineTimingMiddleware>();

		_dispatchPipelineProvider = dispatchServices.BuildServiceProvider();
		_dispatchPipelineDispatcher = _dispatchPipelineProvider.GetRequiredService<IDispatcher>();
		_dispatchContextFactory = _dispatchPipelineProvider.GetRequiredService<IMessageContextFactory>();

		// Setup MediatR with 3 pipeline behaviors
		var mediatrServices = new ServiceCollection();
		_ = mediatrServices.AddMediatR(cfg =>
		{
			_ = cfg.RegisterServicesFromAssemblyContaining<PipelineComparisonBenchmarks>();
			_ = cfg.AddBehavior<IPipelineBehavior<MediatRPipelineTestCommand, Unit>, MediatRLoggingBehavior>();
			_ = cfg.AddBehavior<IPipelineBehavior<MediatRPipelineTestCommand, Unit>, MediatRValidationBehavior>();
			_ = cfg.AddBehavior<IPipelineBehavior<MediatRPipelineTestCommand, Unit>, MediatRTimingBehavior>();
		});

		_mediatrPipelineProvider = mediatrServices.BuildServiceProvider();
		_mediatrPipelineMediator = _mediatrPipelineProvider.GetRequiredService<IMediator>();

		// Setup Wolverine with 3 convention-based middleware
		_wolverineHost = await Host.CreateDefaultBuilder()
			.UseWolverine(opts =>
			{
				opts.Discovery.IncludeAssembly(typeof(PipelineComparisonBenchmarks).Assembly);
				opts.Discovery.IncludeType(typeof(WolverinePipelineCommandHandler));

				// Register 3 passthrough middleware
				opts.Policies.AddMiddleware<WolverinePipelineMiddleware1>();
				opts.Policies.AddMiddleware<WolverinePipelineMiddleware2>();
				opts.Policies.AddMiddleware<WolverinePipelineMiddleware3>();
			})
			.StartAsync()
			.ConfigureAwait(false);

		_wolverineBus = _wolverineHost.Services.GetRequiredService<IMessageBus>();

		// Setup MassTransit Mediator with 3 consume filters
		var massTransitServices = new ServiceCollection();
		_ = massTransitServices.AddMediator(cfg =>
		{
			_ = cfg.AddConsumer<MassTransitPipelineCommandConsumer>();

			cfg.ConfigureMediator((context, mcfg) =>
			{
				mcfg.UseConsumeFilter(typeof(MassTransitPipelineFilter1<>), context);
				mcfg.UseConsumeFilter(typeof(MassTransitPipelineFilter2<>), context);
				mcfg.UseConsumeFilter(typeof(MassTransitPipelineFilter3<>), context);
			});
		});

		_massTransitMediatorProvider = massTransitServices.BuildServiceProvider();
		_massTransitMediatorScope = _massTransitMediatorProvider.CreateScope();
		_massTransitMediator = _massTransitMediatorScope.ServiceProvider.GetRequiredService<MassTransit.Mediator.IScopedMediator>();

		// Warm and freeze Dispatch caches so benchmark reflects optimized production mode.
		WarmupAndFreezeDispatchCaches();
	}

	/// <summary>
	/// Cleanup all service providers.
	/// </summary>
	[GlobalCleanup]
	public async Task GlobalCleanup()
	{
		if (_dispatchPipelineProvider is IDisposable dispatchDisposable)
		{
			dispatchDisposable.Dispose();
		}

		if (_mediatrPipelineProvider is IDisposable mediatrDisposable)
		{
			mediatrDisposable.Dispose();
		}

		if (_wolverineHost is not null)
		{
			await _wolverineHost.StopAsync().ConfigureAwait(false);
			_wolverineHost.Dispose();
		}

		if (_massTransitMediatorScope is IAsyncDisposable massTransitAsyncScope)
		{
			await massTransitAsyncScope.DisposeAsync().ConfigureAwait(false);
		}
		else
		{
			_massTransitMediatorScope?.Dispose();
		}

		if (_massTransitMediatorProvider is IAsyncDisposable massTransitAsyncProvider)
		{
			await massTransitAsyncProvider.DisposeAsync().ConfigureAwait(false);
		}
		else if (_massTransitMediatorProvider is IDisposable massTransitDisposable)
		{
			massTransitDisposable.Dispose();
		}
	}

	// ============================================================================
	// Single Command with Pipeline
	// ============================================================================

	/// <summary>
	/// Baseline: Excalibur with 3 middleware behaviors.
	/// </summary>
	[Benchmark(Baseline = true, Description = "Dispatch: 3 middleware behaviors")]
	public async Task<IMessageResult> Dispatch_WithPipelineBehaviors()
	{
		var command = new PipelineTestCommand { Value = 42 };
		return await DispatchWithFreshContextAsync(_dispatchPipelineDispatcher, _dispatchContextFactory, command).ConfigureAwait(false);
	}

	/// <summary>
	/// MediatR: With 3 pipeline behaviors.
	/// </summary>
	[Benchmark(Description = "MediatR: 3 pipeline behaviors")]
	public async Task<Unit> MediatR_WithPipelineBehaviors()
	{
		var command = new MediatRPipelineTestCommand { Value = 42 };
		return await _mediatrPipelineMediator.Send(command, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// Wolverine: With 3 convention-based middleware.
	/// </summary>
	[Benchmark(Description = "Wolverine: 3 middleware")]
	public async Task Wolverine_WithMiddleware()
	{
		var command = new WolverinePipelineCommand { Value = 42 };
		await _wolverineBus.InvokeAsync(command, CancellationToken.None).ConfigureAwait(false);
	}

	/// <summary>
	/// MassTransit Mediator: With 3 consume filters.
	/// </summary>
	[Benchmark(Description = "MassTransit: 3 consume filters")]
	public async Task MassTransit_WithFilters()
	{
		var command = new MassTransitPipelineCommand { Value = 42 };
		await _massTransitMediator.Publish(command, CancellationToken.None).ConfigureAwait(false);
	}

	// ============================================================================
	// Concurrent Commands with Pipeline
	// ============================================================================

	/// <summary>
	/// Dispatch: 10 concurrent commands with 3 behaviors.
	/// </summary>
	[Benchmark(Description = "Dispatch: 10 concurrent + 3 behaviors")]
	public async Task Dispatch_ConcurrentWithBehaviors()
	{
		var tasks = new List<Task<IMessageResult>>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new PipelineTestCommand { Value = i };
			tasks.Add(DispatchWithFreshContextAsync(_dispatchPipelineDispatcher, _dispatchContextFactory, command));
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// MediatR: 10 concurrent commands with 3 behaviors.
	/// </summary>
	[Benchmark(Description = "MediatR: 10 concurrent + 3 behaviors")]
	public async Task MediatR_ConcurrentWithBehaviors()
	{
		var tasks = new List<Task<Unit>>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new MediatRPipelineTestCommand { Value = i };
			tasks.Add(_mediatrPipelineMediator.Send(command, CancellationToken.None));
		}

		_ = await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// Wolverine: 10 concurrent commands with 3 middleware.
	/// </summary>
	[Benchmark(Description = "Wolverine: 10 concurrent + 3 middleware")]
	public async Task Wolverine_ConcurrentWithMiddleware()
	{
		var tasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new WolverinePipelineCommand { Value = i };
			tasks.Add(_wolverineBus.InvokeAsync(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	/// <summary>
	/// MassTransit Mediator: 10 concurrent commands with 3 filters.
	/// </summary>
	[Benchmark(Description = "MassTransit: 10 concurrent + 3 filters")]
	public async Task MassTransit_ConcurrentWithFilters()
	{
		var tasks = new List<Task>(10);
		for (int i = 0; i < 10; i++)
		{
			var command = new MassTransitPipelineCommand { Value = i };
			tasks.Add(_massTransitMediator.Publish(command, CancellationToken.None));
		}

		await Task.WhenAll(tasks).ConfigureAwait(false);
	}

	// ============================================================================
	// Helper Methods
	// ============================================================================

	private void WarmupAndFreezeDispatchCaches()
	{
		_ = DispatchWithFreshContextAsync(
				_dispatchPipelineDispatcher,
				_dispatchContextFactory,
				new PipelineTestCommand { Value = 1 })
			.GetAwaiter().GetResult();

		HandlerInvoker.FreezeCache();
		HandlerInvokerRegistry.FreezeCache();
		HandlerActivator.FreezeCache();
		FinalDispatchHandler.FreezeResultFactoryCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();
	}

	private static async Task<IMessageResult> DispatchWithFreshContextAsync<TMessage>(
		IDispatcher? dispatcher,
		IMessageContextFactory? contextFactory,
		TMessage message)
		where TMessage : IDispatchMessage
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		ArgumentNullException.ThrowIfNull(contextFactory);

		var context = contextFactory.CreateContext();
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

// ============================================================================
// Pipeline Test Messages and Handlers (Dispatch)
// ============================================================================

/// <summary>
/// Test command for pipeline benchmarks.
/// </summary>
public record PipelineTestCommand : IDispatchAction
{
	/// <summary>Gets the test value.</summary>
	public int Value { get; init; }
}

/// <summary>
/// Handler for PipelineTestCommand.
/// </summary>
public sealed class PipelineTestCommandHandler : IActionHandler<PipelineTestCommand>
{
	/// <inheritdoc />
	public Task HandleAsync(PipelineTestCommand message, CancellationToken cancellationToken)
	{
		_ = message.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Logging middleware (passthrough) for pipeline benchmarks.
/// </summary>
public sealed class PipelineLoggingMiddleware : IDispatchMiddleware
{
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Minimal overhead - just call next
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Validation middleware (passthrough) for pipeline benchmarks.
/// </summary>
public sealed class PipelineValidationMiddleware : IDispatchMiddleware
{
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Minimal overhead - just call next
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}
}

/// <summary>
/// Timing middleware (passthrough) for pipeline benchmarks.
/// </summary>
public sealed class PipelineTimingMiddleware : IDispatchMiddleware
{
	/// <inheritdoc />
	public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

	/// <inheritdoc />
	public async ValueTask<IMessageResult> InvokeAsync(
		IDispatchMessage message,
		IMessageContext context,
		DispatchRequestDelegate nextDelegate,
		CancellationToken cancellationToken)
	{
		// Minimal overhead - just call next
		return await nextDelegate(message, context, cancellationToken).ConfigureAwait(false);
	}
}

// ============================================================================
// Pipeline Test Messages and Handlers (MediatR)
// ============================================================================

/// <summary>
/// Test command for MediatR pipeline benchmarks.
/// </summary>
public record MediatRPipelineTestCommand : IRequest<Unit>
{
	/// <summary>Gets the test value.</summary>
	public int Value { get; init; }
}

/// <summary>
/// Handler for MediatRPipelineTestCommand.
/// </summary>
public sealed class MediatRPipelineTestCommandHandler : IRequestHandler<MediatRPipelineTestCommand, Unit>
{
	/// <inheritdoc />
	public Task<Unit> Handle(MediatRPipelineTestCommand request, CancellationToken cancellationToken)
	{
		_ = request.Value * 2;
		return Task.FromResult(Unit.Value);
	}
}

/// <summary>
/// MediatR logging behavior (passthrough).
/// </summary>
public sealed class MediatRLoggingBehavior : IPipelineBehavior<MediatRPipelineTestCommand, Unit>
{
	/// <inheritdoc />
	public async Task<Unit> Handle(
		MediatRPipelineTestCommand request,
		RequestHandlerDelegate<Unit> next,
		CancellationToken cancellationToken)
	{
		return await next().ConfigureAwait(false);
	}
}

/// <summary>
/// MediatR validation behavior (passthrough).
/// </summary>
public sealed class MediatRValidationBehavior : IPipelineBehavior<MediatRPipelineTestCommand, Unit>
{
	/// <inheritdoc />
	public async Task<Unit> Handle(
		MediatRPipelineTestCommand request,
		RequestHandlerDelegate<Unit> next,
		CancellationToken cancellationToken)
	{
		return await next().ConfigureAwait(false);
	}
}

/// <summary>
/// MediatR timing behavior (passthrough).
/// </summary>
public sealed class MediatRTimingBehavior : IPipelineBehavior<MediatRPipelineTestCommand, Unit>
{
	/// <inheritdoc />
	public async Task<Unit> Handle(
		MediatRPipelineTestCommand request,
		RequestHandlerDelegate<Unit> next,
		CancellationToken cancellationToken)
	{
		return await next().ConfigureAwait(false);
	}
}

// ============================================================================
// Pipeline Test Messages and Handlers (Wolverine)
// ============================================================================

/// <summary>
/// Test command for Wolverine pipeline benchmarks.
/// </summary>
public record WolverinePipelineCommand
{
	/// <summary>Gets or sets the test value.</summary>
	public int Value { get; set; }
}

/// <summary>
/// Wolverine handler for WolverinePipelineCommand.
/// </summary>
public static class WolverinePipelineCommandHandler
{
	/// <summary>Handle the pipeline test command.</summary>
	public static Task Handle(WolverinePipelineCommand command, CancellationToken cancellationToken)
	{
		_ = command.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Wolverine passthrough middleware 1 (logging stage equivalent).
/// Uses Wolverine convention: BeforeAsync/AfterAsync methods.
/// </summary>
public class WolverinePipelineMiddleware1
{
	/// <summary>No-op before handler execution.</summary>
	public static Task BeforeAsync() => Task.CompletedTask;
}

/// <summary>
/// Wolverine passthrough middleware 2 (validation stage equivalent).
/// </summary>
public class WolverinePipelineMiddleware2
{
	/// <summary>No-op before handler execution.</summary>
	public static Task BeforeAsync() => Task.CompletedTask;
}

/// <summary>
/// Wolverine passthrough middleware 3 (timing stage equivalent).
/// </summary>
public class WolverinePipelineMiddleware3
{
	/// <summary>No-op before handler execution.</summary>
	public static Task BeforeAsync() => Task.CompletedTask;
}

// ============================================================================
// Pipeline Test Messages and Consumers (MassTransit Mediator)
// ============================================================================

/// <summary>
/// Test command for MassTransit Mediator pipeline benchmarks.
/// </summary>
public record MassTransitPipelineCommand
{
	/// <summary>Gets or sets the test value.</summary>
	public int Value { get; set; }
}

/// <summary>
/// MassTransit consumer for MassTransitPipelineCommand.
/// </summary>
public sealed class MassTransitPipelineCommandConsumer : IConsumer<MassTransitPipelineCommand>
{
	/// <inheritdoc />
	public Task Consume(ConsumeContext<MassTransitPipelineCommand> context)
	{
		_ = context.Message.Value * 2;
		return Task.CompletedTask;
	}
}

/// <summary>
/// MassTransit passthrough consume filter 1 (logging stage equivalent).
/// </summary>
public sealed class MassTransitPipelineFilter1<T> : IFilter<ConsumeContext<T>>
	where T : class
{
	/// <inheritdoc />
	public void Probe(ProbeContext context)
	{
		context.CreateFilterScope("pipelineFilter1");
	}

	/// <inheritdoc />
	public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
	{
		// Minimal overhead - just call next
		await next.Send(context).ConfigureAwait(false);
	}
}

/// <summary>
/// MassTransit passthrough consume filter 2 (validation stage equivalent).
/// </summary>
public sealed class MassTransitPipelineFilter2<T> : IFilter<ConsumeContext<T>>
	where T : class
{
	/// <inheritdoc />
	public void Probe(ProbeContext context)
	{
		context.CreateFilterScope("pipelineFilter2");
	}

	/// <inheritdoc />
	public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
	{
		// Minimal overhead - just call next
		await next.Send(context).ConfigureAwait(false);
	}
}

/// <summary>
/// MassTransit passthrough consume filter 3 (timing stage equivalent).
/// </summary>
public sealed class MassTransitPipelineFilter3<T> : IFilter<ConsumeContext<T>>
	where T : class
{
	/// <inheritdoc />
	public void Probe(ProbeContext context)
	{
		context.CreateFilterScope("pipelineFilter3");
	}

	/// <inheritdoc />
	public async Task Send(ConsumeContext<T> context, IPipe<ConsumeContext<T>> next)
	{
		// Minimal overhead - just call next
		await next.Send(context).ConfigureAwait(false);
	}
}

#pragma warning restore SA1402
#pragma warning restore CA1707
