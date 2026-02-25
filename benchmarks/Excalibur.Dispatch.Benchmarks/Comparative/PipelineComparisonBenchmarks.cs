// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention
#pragma warning disable SA1402 // File may only contain a single type - benchmarks with supporting types

/// <summary>
/// Comparative benchmarks for pipeline behavior overhead.
/// Measures Dispatch middleware vs MediatR IPipelineBehavior performance.
/// </summary>
/// <remarks>
/// Sprint 204 - Competitor Comparison Benchmarks Epic.
/// Fills gaps identified in existing MediatRComparisonBenchmarks:
/// - Pipeline behavior overhead (3 behaviors)
/// - Concurrent execution with behaviors
///
/// Performance Targets:
/// - Pipeline vs MediatR: 5x faster
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

	/// <summary>
	/// Initialize both frameworks with 3 pipeline behaviors.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
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

		// Warm and freeze Dispatch caches so benchmark reflects optimized production mode.
		WarmupAndFreezeDispatchCaches();
	}

	/// <summary>
	/// Cleanup service providers.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_dispatchPipelineProvider is IDisposable dispatchDisposable)
		{
			dispatchDisposable.Dispose();
		}

		if (_mediatrPipelineProvider is IDisposable mediatrDisposable)
		{
			mediatrDisposable.Dispose();
		}
	}

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

#pragma warning restore SA1402
#pragma warning restore CA1707
