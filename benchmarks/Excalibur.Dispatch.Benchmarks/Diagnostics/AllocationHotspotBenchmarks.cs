// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Tracks allocation hotspots per dispatch stage with memory diagnostics and GC counter deltas.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class AllocationHotspotBenchmarks
{
	private DiagnosticBenchmarkFixture? _fixture;
	private readonly DiagnosticCommand _command = new(42);
	private readonly DiagnosticEvent _event = new(42, "alloc-hotspot");

	[Params(10_000)]
	public int BatchOperations { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		_fixture = new DiagnosticBenchmarkFixture(eventHandlerCount: 10);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_fixture?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Stage alloc: dispatcher")]
	public Task<IMessageResult> DispatcherStage()
	{
		return WithContextAsync(context => _fixture!.Dispatcher.DispatchAsync(_command, context, CancellationToken.None));
	}

	[Benchmark(Description = "Stage alloc: final handler")]
	public async Task<IMessageResult> FinalHandlerStage()
	{
		return await WithContextValueTaskAsync(
			context => _fixture!.FinalDispatchHandler.HandleAsync(_command, context, CancellationToken.None)).
			ConfigureAwait(false);
	}

	[Benchmark(Description = "Stage alloc: local bus send")]
	public async Task LocalBusStage()
	{
		await WithContextAsync(async context =>
		{
			await _fixture!.LocalMessageBus.SendAsync(_command, context, CancellationToken.None).ConfigureAwait(false);
			return 0;
		}).ConfigureAwait(false);
	}

	[Benchmark(Description = "Stage alloc: handler activator")]
	public object HandlerActivatorStage()
	{
		_ = _fixture!.HandlerRegistry.TryGetHandler(typeof(DiagnosticCommand), out var entry);
		return WithContext(context => _fixture.HandlerActivator.ActivateHandler(entry.HandlerType, context, _fixture.Services));
	}

	[Benchmark(Description = "Stage alloc: handler invoker")]
	public async Task<object?> HandlerInvokerStage()
	{
		_ = _fixture!.HandlerRegistry.TryGetHandler(typeof(DiagnosticCommand), out var entry);
		return await WithContextAsync(async context =>
		{
			var handler = _fixture.HandlerActivator.ActivateHandler(entry.HandlerType, context, _fixture.Services);
			return await _fixture.HandlerInvoker.InvokeAsync(handler, _command, CancellationToken.None).ConfigureAwait(false);
		}).ConfigureAwait(false);
	}

	[Benchmark(Description = "GC counter delta: gen0 per dispatch batch")]
	public async Task<int> Gen0CollectionsPerBatch()
	{
		var start = GC.CollectionCount(0);
		for (var i = 0; i < BatchOperations; i++)
		{
			_ = await WithContextAsync(
				context => _fixture!.Dispatcher.DispatchAsync(_event, context, CancellationToken.None)).ConfigureAwait(false);
		}

		return GC.CollectionCount(0) - start;
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
