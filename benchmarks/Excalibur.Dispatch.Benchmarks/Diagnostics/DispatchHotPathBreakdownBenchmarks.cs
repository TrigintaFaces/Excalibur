// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Breaks dispatch into component-level costs to pinpoint hot-path regressions.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class DispatchHotPathBreakdownBenchmarks
{
	private DiagnosticBenchmarkFixture? _fixture;
	private DiagnosticCommand _command = null!;
	private DiagnosticQuery _query = null!;
	private object _preActivatedHandler = null!;
	private IMessageContext _precreatedActivationContext = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(middlewareCount: 0, eventHandlerCount: 3);
		_command = new DiagnosticCommand(42);
		_query = new DiagnosticQuery(42);

		var context = _fixture.CreateContext();
		try
		{
			_preActivatedHandler = _fixture.HandlerActivator.ActivateHandler(
				typeof(DiagnosticCommandHandler),
				context,
				_fixture.Services);
		}
		finally
		{
			_fixture.ReturnContext(context);
		}

		_precreatedActivationContext = _fixture.CreateContext();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_fixture is null)
		{
			return;
		}

		if (_precreatedActivationContext is not null)
		{
			_fixture.ReturnContext(_precreatedActivationContext);
			_precreatedActivationContext = null!;
		}

		_fixture.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Dispatcher: Single command")]
	public Task<IMessageResult> Dispatcher_SingleCommand()
	{
		return WithContextAsync(context => _fixture!.Dispatcher.DispatchAsync(_command, context, CancellationToken.None));
	}

	[Benchmark(Description = "Dispatcher: Query with response")]
	public Task<IMessageResult<int>> Dispatcher_QueryWithResponse()
	{
		return WithContextAsync(context => _fixture!.Dispatcher.DispatchAsync<DiagnosticQuery, int>(_query, context, CancellationToken.None));
	}

	[Benchmark(Description = "MiddlewareInvoker: Direct invoke")]
	public async Task<IMessageResult> MiddlewareInvoker_DirectInvoke()
	{
		return await WithContextAsync(context => _fixture!.MiddlewareInvoker.InvokeAsync(
			_command,
			context,
			static (_, _, _) => ValueTask.FromResult(MessageResult.Success()),
			CancellationToken.None).AsTask()).ConfigureAwait(false);
	}

	[Benchmark(Description = "FinalDispatchHandler: Action")]
	public async Task<IMessageResult> FinalDispatchHandler_Action()
	{
		return await WithContextValueTaskAsync(
			context => _fixture!.FinalDispatchHandler.HandleAsync(_command, context, CancellationToken.None)).
			ConfigureAwait(false);
	}

	[Benchmark(Description = "LocalMessageBus: Send action")]
	public async Task<int> LocalMessageBus_SendAction()
	{
		return await WithContextAsync(async context =>
		{
			await _fixture!.LocalMessageBus.SendAsync(_command, context, CancellationToken.None).ConfigureAwait(false);
			return 1;
		}).ConfigureAwait(false);
	}

	[Benchmark(Description = "HandlerActivator: Activate")]
	public object HandlerActivator_Activate()
	{
		return WithContext(context => _fixture!.HandlerActivator.ActivateHandler(
			typeof(DiagnosticCommandHandler),
			context,
			_fixture.Services));
	}

	[Benchmark(Description = "HandlerActivator: Activate (precreated context)")]
	public object HandlerActivator_Activate_PrecreatedContext()
	{
		return _fixture!.HandlerActivator.ActivateHandler(
			typeof(DiagnosticCommandHandler),
			_precreatedActivationContext,
			_fixture.Services);
	}

	[Benchmark(Description = "HandlerInvoker: Invoke")]
	public Task<object?> HandlerInvoker_Invoke()
	{
		return _fixture!.HandlerInvoker.InvokeAsync(_preActivatedHandler, _command, CancellationToken.None);
	}

	[Benchmark(Description = "HandlerRegistry: Lookup")]
	public bool HandlerRegistry_Lookup()
	{
		return _fixture!.HandlerRegistry.TryGetHandler(typeof(DiagnosticCommand), out _);
	}

	private async Task<T> WithContextAsync<T>(Func<IMessageContext, Task<T>> operation)
	{
		var context = _fixture!.CreateContext();
		try
		{
			return await operation(context).ConfigureAwait(false);
		}
		finally
		{
			_fixture.ReturnContext(context);
		}
	}

	private async Task<T> WithContextValueTaskAsync<T>(Func<IMessageContext, ValueTask<T>> operation)
	{
		var context = _fixture!.CreateContext();
		try
		{
			return await operation(context).ConfigureAwait(false);
		}
		finally
		{
			_fixture.ReturnContext(context);
		}
	}

	private T WithContext<T>(Func<IMessageContext, T> operation)
	{
		var context = _fixture!.CreateContext();
		try
		{
			return operation(context);
		}
		finally
		{
			_fixture.ReturnContext(context);
		}
	}
}
