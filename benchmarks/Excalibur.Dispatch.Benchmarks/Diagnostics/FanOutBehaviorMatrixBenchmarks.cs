// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Holds fan-out handler count constant while changing handler behavior to isolate scheduler vs framework overhead.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class FanOutBehaviorMatrixBenchmarks
{
	private ServiceProvider? _provider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _contextFactory;
	private DiagnosticEvent _event = null!;

	[Params(1, 10, 50)]
	public int HandlerCount { get; set; }

	[ParamsAllValues]
	public FanOutBehaviorMode BehaviorMode { get; set; }

	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddBenchmarkDispatch();

		var markerTypes = DiagnosticBenchmarkFixture.GetFanOutMarkerTypes(HandlerCount);
		for (var index = 0; index < markerTypes.Count; index++)
		{
			var markerType = markerTypes[index];
			var handlerType = ResolveHandlerType(markerType);
			_ = services.AddTransient(typeof(IEventHandler<DiagnosticEvent>), handlerType);
		}

		_provider = services.BuildServiceProvider();
		_dispatcher = _provider.GetRequiredService<IDispatcher>();
		_contextFactory = _provider.GetRequiredService<IMessageContextFactory>();
		_event = new DiagnosticEvent(42, $"behavior-{BehaviorMode}");
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		_provider?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "Fan-out dispatch by handler behavior")]
	public Task<IMessageResult> DispatchByHandlerBehavior()
	{
		return _dispatcher!.DispatchAsync(_event, _contextFactory!.CreateContext(), CancellationToken.None);
	}

	private Type ResolveHandlerType(Type markerType)
	{
		return BehaviorMode switch
		{
			FanOutBehaviorMode.CompletedTask => typeof(CompletedFanOutEventHandler<>).MakeGenericType(markerType),
			FanOutBehaviorMode.TaskYield => typeof(YieldingFanOutEventHandler<>).MakeGenericType(markerType),
			FanOutBehaviorMode.ShortIoDelay => typeof(ShortIoFanOutEventHandler<>).MakeGenericType(markerType),
			_ => throw new InvalidOperationException($"Unsupported fan-out behavior mode: {BehaviorMode}."),
		};
	}

	private sealed class CompletedFanOutEventHandler<TMarker> : IEventHandler<DiagnosticEvent>
		where TMarker : class
	{
		public Task HandleAsync(DiagnosticEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Value + eventMessage.Name.Length;
			return Task.CompletedTask;
		}
	}

	private sealed class YieldingFanOutEventHandler<TMarker> : IEventHandler<DiagnosticEvent>
		where TMarker : class
	{
		public async Task HandleAsync(DiagnosticEvent eventMessage, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			await Task.Yield();
			_ = eventMessage.Value + eventMessage.Name.Length;
		}
	}

	private sealed class ShortIoFanOutEventHandler<TMarker> : IEventHandler<DiagnosticEvent>
		where TMarker : class
	{
		public async Task HandleAsync(DiagnosticEvent eventMessage, CancellationToken cancellationToken)
		{
			await Task.Delay(1, cancellationToken).ConfigureAwait(false);
			_ = eventMessage.Value + eventMessage.Name.Length;
		}
	}
}

public enum FanOutBehaviorMode
{
	CompletedTask,
	TaskYield,
	ShortIoDelay,
}
