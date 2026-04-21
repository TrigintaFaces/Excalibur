// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.EventSourcing.Abstractions;
using Excalibur.EventSourcing.Projections;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Benchmarks.EventSourcing;

/// <summary>
/// T.14 (ynp02v): Performance benchmark comparing When&lt;T&gt; lambda vs WhenHandledBy
/// handler overhead per event. Measures the dispatch hot path only.
/// Target: &lt; 100ns overhead per event (D6).
/// </summary>
/// <remarks>
/// <para>Benchmarks:</para>
/// <list type="bullet">
/// <item><c>WhenLambdaSync</c>: Tier 1 sync lambda via <c>Apply()</c> -- the baseline.</item>
/// <item><c>HandlerDirectCall</c>: Handler.HandleAsync direct call -- measures handler overhead only.</item>
/// <item><c>HandlerViaDiSingleton</c>: DI resolution (singleton) + handler call.</item>
/// <item><c>HandlerViaDiTransient</c>: DI resolution (transient) + handler call -- includes allocation.</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class ProjectionHandlerBenchmarks
{
	private MultiStreamProjection<BenchProjection> _syncProjection = null!;
	private BenchProjection _state = null!;
	private BenchEvent _event = null!;
	private ProjectionHandlerContext _context = null!;
	private BenchEventSingletonHandler _singletonHandler = null!;
	private IServiceProvider _singletonServices = null!;
	private IServiceProvider _transientServices = null!;

	[GlobalSetup]
	public void GlobalSetup()
	{
		_state = new BenchProjection();
		_event = new BenchEvent
		{
			EventId = "evt-1",
			AggregateId = "agg-1",
			Version = 1,
			OccurredAt = DateTimeOffset.UtcNow,
			EventType = "BenchEvent",
			Amount = 42m
		};
		_context = new ProjectionHandlerContext("agg-1", "Bench", 1, DateTimeOffset.UtcNow);

		// Tier 1: Sync lambda projection (via public API)
		_syncProjection = new MultiStreamProjection<BenchProjection>();

		// Use reflection to add handler since AddHandler is internal.
		// This is acceptable for benchmark setup -- the hot path (Apply) is public.
		var addMethod = typeof(MultiStreamProjection<BenchProjection>)
			.GetMethod("AddHandler", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
			.MakeGenericMethod(typeof(BenchEvent));
		Action<BenchProjection, BenchEvent> handler = (proj, e) => proj.Total += e.Amount;
		addMethod.Invoke(_syncProjection, [handler]);

		// Singleton handler instance
		_singletonHandler = new BenchEventSingletonHandler();

		// Singleton DI container
		_singletonServices = new ServiceCollection()
			.AddSingleton<BenchEventSingletonHandler>(_singletonHandler)
			.BuildServiceProvider();

		// Transient DI container
		_transientServices = new ServiceCollection()
			.AddTransient<BenchEventTransientHandler>()
			.BuildServiceProvider();
	}

	/// <summary>
	/// Baseline: Sync lambda via When&lt;T&gt; (Tier 1) -- Apply() dispatch.
	/// </summary>
	[Benchmark(Baseline = true)]
	public bool WhenLambdaSync()
	{
		return _syncProjection.Apply(_state, _event);
	}

	/// <summary>
	/// Direct handler call (no DI resolution). Measures pure handler overhead.
	/// </summary>
	[Benchmark]
	public Task HandlerDirectCall()
	{
		return _singletonHandler.HandleAsync(_state, _event, _context, CancellationToken.None);
	}

	/// <summary>
	/// DI resolution (singleton) + handler call. Measures DI lookup overhead.
	/// </summary>
	[Benchmark]
	public Task HandlerViaDiSingleton()
	{
		var handler = (IProjectionEventHandler<BenchProjection, BenchEvent>)
			_singletonServices.GetRequiredService(typeof(BenchEventSingletonHandler));
		return handler.HandleAsync(_state, _event, _context, CancellationToken.None);
	}

	/// <summary>
	/// DI resolution (transient) + handler call. Measures DI + allocation overhead.
	/// </summary>
	[Benchmark]
	public Task HandlerViaDiTransient()
	{
		var handler = (IProjectionEventHandler<BenchProjection, BenchEvent>)
			_transientServices.GetRequiredService(typeof(BenchEventTransientHandler));
		return handler.HandleAsync(_state, _event, _context, CancellationToken.None);
	}
}

/// <summary>
/// Minimal benchmark projection state.
/// </summary>
public sealed class BenchProjection
{
	public decimal Total { get; set; }
}

/// <summary>
/// Benchmark event.
/// </summary>
public sealed class BenchEvent : IDomainEvent
{
	public required string EventId { get; init; }
	public required string AggregateId { get; init; }
	public required long Version { get; init; }
	public required DateTimeOffset OccurredAt { get; init; }
	public required string EventType { get; init; }
	public IDictionary<string, object>? Metadata { get; init; }
	public decimal Amount { get; init; }
}

/// <summary>
/// Singleton benchmark handler -- zero allocation on resolution.
/// </summary>
public sealed class BenchEventSingletonHandler : IProjectionEventHandler<BenchProjection, BenchEvent>
{
	public Task HandleAsync(
		BenchProjection projection,
		BenchEvent @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Total += @event.Amount;
		return Task.CompletedTask;
	}
}

/// <summary>
/// Transient benchmark handler -- allocated per resolution.
/// </summary>
public sealed class BenchEventTransientHandler : IProjectionEventHandler<BenchProjection, BenchEvent>
{
	public Task HandleAsync(
		BenchProjection projection,
		BenchEvent @event,
		ProjectionHandlerContext context,
		CancellationToken cancellationToken)
	{
		projection.Total += @event.Amount;
		return Task.CompletedTask;
	}
}
