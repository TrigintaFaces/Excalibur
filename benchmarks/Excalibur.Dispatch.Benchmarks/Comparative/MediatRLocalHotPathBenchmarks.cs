// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;

using MediatR;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Comparative;

#pragma warning disable CA1707 // Identifiers should not contain underscores - benchmark naming convention

/// <summary>
/// Focused local hot-path parity benchmark for tight optimization loops.
/// Keeps only high-frequency command/query scenarios to reduce runtime and noise.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(ComparativeBenchmarkConfig))]
public class MediatRLocalHotPathBenchmarks
{
	private IServiceProvider? _dispatchServiceProvider;
	private IDispatcher? _dispatcher;
	private IDirectLocalDispatcher? _directLocalDispatcher;
	private IMessageContextFactory? _contextFactory;

	private IServiceProvider? _mediatrServiceProvider;
	private IMediator? _mediator;

	[GlobalSetup]
	public void GlobalSetup()
	{
		var dispatchServices = new ServiceCollection();
		_ = dispatchServices.AddLogging();
		_ = dispatchServices.AddDispatch();
		_ = dispatchServices.AddTransient<DispatchTestCommandHandler>();
		_ = dispatchServices.AddTransient<DispatchTestQueryHandler>();
		_ = dispatchServices.AddTransient<IActionHandler<TestCommand>, DispatchTestCommandHandler>();
		_ = dispatchServices.AddTransient<IActionHandler<TestQuery, int>, DispatchTestQueryHandler>();

		_dispatchServiceProvider = dispatchServices.BuildServiceProvider();
		_dispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>();
		_directLocalDispatcher = _dispatchServiceProvider.GetRequiredService<IDispatcher>() as IDirectLocalDispatcher;
		_contextFactory = _dispatchServiceProvider.GetRequiredService<IMessageContextFactory>();

		var mediatrServices = new ServiceCollection();
		_ = mediatrServices.AddLogging();
		_ = mediatrServices.AddMediatR(cfg => cfg.RegisterServicesFromAssemblyContaining<MediatRComparisonBenchmarks>());
		_mediatrServiceProvider = mediatrServices.BuildServiceProvider();
		_mediator = _mediatrServiceProvider.GetRequiredService<IMediator>();

		WarmupAndFreezeDispatchCaches();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_dispatchServiceProvider is IDisposable dispatchDisposable)
		{
			dispatchDisposable.Dispose();
		}

		if (_mediatrServiceProvider is IDisposable mediatrDisposable)
		{
			mediatrDisposable.Dispose();
		}
	}

	[Benchmark(Baseline = true, Description = "Dispatch hot-path: command")]
	public Task<IMessageResult> Dispatch_SingleCommand()
	{
		var command = new TestCommand { Value = 42 };
		return DispatchWithFreshContextAsync(_dispatcher, _contextFactory, command);
	}

	[Benchmark(Description = "Dispatch hot-path: query")]
	public Task<IMessageResult> Dispatch_Query()
	{
		var query = new TestQuery { Id = 123 };
		return DispatchWithFreshContextAsync(_dispatcher, _contextFactory, query);
	}

	[Benchmark(Description = "Dispatch hot-path: typed query API")]
	public Task<IMessageResult<int>> Dispatch_TypedQuery()
	{
		var query = new TestQuery { Id = 123 };
		return DispatchWithFreshContextTypedAsync<TestQuery, int>(_dispatcher, _contextFactory, query);
	}

	[Benchmark(Description = "Dispatch hot-path: ultra-local command")]
	public ValueTask Dispatch_UltraLocalCommand()
	{
		var command = new TestCommand { Value = 42 };
		return DispatchUltraLocalAsync(_directLocalDispatcher, command);
	}

	[Benchmark(Description = "Dispatch hot-path: ultra-local query")]
	public ValueTask<int> Dispatch_UltraLocalQuery()
	{
		var query = new TestQuery { Id = 123 };
		return DispatchUltraLocalWithResponseAsync<TestQuery, int>(_directLocalDispatcher, query);
	}

	[Benchmark(Description = "MediatR hot-path: command")]
	public Task<Unit> MediatR_SingleCommand()
	{
		var command = new MediatRTestCommand { Value = 42 };
		return _mediator!.Send(command, CancellationToken.None);
	}

	[Benchmark(Description = "MediatR hot-path: query")]
	public Task<int> MediatR_Query()
	{
		var query = new MediatRTestQuery { Id = 123 };
		return _mediator!.Send(query, CancellationToken.None);
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
		var dispatchTask = dispatcher.DispatchAsync(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				contextFactory.Return(context);
			}
		}

		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
	}

	private static async Task<IMessageResult<TResponse>> DispatchWithFreshContextTypedAsync<TMessage, TResponse>(
		IDispatcher? dispatcher,
		IMessageContextFactory? contextFactory,
		TMessage message)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		ArgumentNullException.ThrowIfNull(contextFactory);

		var context = contextFactory.CreateContext();
		var dispatchTask = dispatcher.DispatchAsync<TMessage, TResponse>(message, context, CancellationToken.None);
		if (dispatchTask.IsCompletedSuccessfully)
		{
			try
			{
				return dispatchTask.Result;
			}
			finally
			{
				contextFactory.Return(context);
			}
		}

		try
		{
			return await dispatchTask.ConfigureAwait(false);
		}
		finally
		{
			contextFactory.Return(context);
		}
	}

	private static ValueTask DispatchUltraLocalAsync<TMessage>(IDirectLocalDispatcher? dispatcher, TMessage message)
		where TMessage : IDispatchAction
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		return dispatcher.DispatchLocalAsync(message, CancellationToken.None);
	}

	private static ValueTask<TResponse?> DispatchUltraLocalWithResponseAsync<TMessage, TResponse>(
		IDirectLocalDispatcher? dispatcher,
		TMessage message)
		where TMessage : IDispatchAction<TResponse>
	{
		ArgumentNullException.ThrowIfNull(dispatcher);
		return dispatcher.DispatchLocalAsync<TMessage, TResponse>(message, CancellationToken.None);
	}

	private void WarmupAndFreezeDispatchCaches()
	{
		ArgumentNullException.ThrowIfNull(_dispatcher);
		ArgumentNullException.ThrowIfNull(_directLocalDispatcher);
		ArgumentNullException.ThrowIfNull(_contextFactory);

		for (var i = 0; i < 8; i++)
		{
			var command = new TestCommand { Value = i };
			var query = new TestQuery { Id = i };

			var context = _contextFactory.CreateContext();
			try
			{
				_ = _dispatcher.DispatchAsync(command, context, CancellationToken.None).GetAwaiter().GetResult();
				_ = _dispatcher.DispatchAsync<TestQuery, int>(query, context, CancellationToken.None).GetAwaiter().GetResult();
			}
			finally
			{
				_contextFactory.Return(context);
			}

			_directLocalDispatcher.DispatchLocalAsync(command, CancellationToken.None).GetAwaiter().GetResult();
			_ = _directLocalDispatcher.DispatchLocalAsync<TestQuery, int>(query, CancellationToken.None).GetAwaiter().GetResult();
		}

		HandlerInvokerRegistry.FreezeCache();
		HandlerActivator.FreezeCache();
		FinalDispatchHandler.FreezeResultFactoryCache();
		MiddlewareApplicabilityEvaluator.FreezeCache();
	}
}
