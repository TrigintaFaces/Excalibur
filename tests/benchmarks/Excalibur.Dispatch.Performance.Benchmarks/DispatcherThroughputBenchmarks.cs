// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// End-to-end benchmarks measuring actual messages-per-second throughput
/// through the complete Excalibur framework pipeline.
/// </summary>
/// <remarks>
/// These benchmarks use the real IDispatcher with actual handler resolution
/// to measure true framework performance, not isolated component speeds.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class DispatcherThroughputBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;

	private SimpleCommand _simpleCommand = null!;
	private SimpleQuery _simpleQuery = null!;
	private IMessageContext _reusableContext = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register test handlers BEFORE AddDispatch - the handler registry is built during AddDispatch
		// Must register BOTH the concrete type (for activator) AND the interface (for registry discovery)
		_ = services.AddTransient<SimpleCommandHandler>();
		_ = services.AddTransient<IActionHandler<SimpleCommand>, SimpleCommandHandler>();

		_ = services.AddTransient<SimpleQueryHandler>();
		_ = services.AddTransient<IActionHandler<SimpleQuery, SimpleQueryResult>, SimpleQueryHandler>();

		_ = services.AddTransient<NoOpCommandHandler>();
		_ = services.AddTransient<IActionHandler<NoOpCommand>, NoOpCommandHandler>();

		// Wire up the full Excalibur framework (without assembly scanning to avoid issues)
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		_simpleCommand = new SimpleCommand { Id = Guid.NewGuid(), Value = 42 };
		_simpleQuery = new SimpleQuery { SearchTerm = "test" };
		_reusableContext = _contextFactory.CreateContext();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Baseline: Single command dispatch through full pipeline.
	/// This is the primary throughput metric.
	/// </summary>
	[Benchmark(Baseline = true)]
	public Task<IMessageResult> SingleCommandDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_simpleCommand, context, CancellationToken.None);
	}

	/// <summary>
	/// Query with result return through full pipeline.
	/// </summary>
	[Benchmark]
	public Task<IMessageResult<SimpleQueryResult>> SingleQueryDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync<SimpleQuery, SimpleQueryResult>(_simpleQuery, context, CancellationToken.None);
	}

	/// <summary>
	/// Measures minimal handler overhead (no-op handler).
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> NoOpCommandDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(new NoOpCommand(), context, CancellationToken.None);
	}

	/// <summary>
	/// Measures context creation overhead separately.
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext()
	{
		return _contextFactory.CreateContext();
	}

	/// <summary>
	/// Dispatch with reused context (minimal allocation path).
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> DispatchWithReusedContext()
	{
		// Reset context for next use
		_reusableContext.Items.Clear();
		return _dispatcher.DispatchAsync(_simpleCommand, _reusableContext, CancellationToken.None);
	}

	/// <summary>
	/// Batch of 10 commands dispatched sequentially.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Batch")]
	public async Task<IMessageResult[]> BatchOf10Sequential()
	{
		var results = new IMessageResult[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			results[i] = await _dispatcher.DispatchAsync(_simpleCommand, context, CancellationToken.None);
		}

		return results;
	}

	/// <summary>
	/// Batch of 10 commands dispatched concurrently.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Batch")]
	public Task<IMessageResult[]> BatchOf10Concurrent()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_simpleCommand, context, CancellationToken.None);
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// Batch of 100 commands dispatched concurrently.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Batch")]
	public Task<IMessageResult[]> BatchOf100Concurrent()
	{
		var tasks = new Task<IMessageResult>[100];
		for (var i = 0; i < 100; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_simpleCommand, context, CancellationToken.None);
		}

		return Task.WhenAll(tasks);
	}

	/// <summary>
	/// High-load scenario: 1000 concurrent dispatches.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HighLoad")]
	public Task<IMessageResult[]> HighLoad1000Concurrent()
	{
		var tasks = new Task<IMessageResult>[1000];
		for (var i = 0; i < 1000; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_simpleCommand, context, CancellationToken.None);
		}

		return Task.WhenAll(tasks);
	}

	// Test Messages
	public sealed record SimpleCommand : IDispatchAction
	{
		public Guid Id { get; init; }
		public int Value { get; init; }
	}

	public sealed record SimpleQuery : IDispatchAction<SimpleQueryResult>
	{
		public string SearchTerm { get; init; } = string.Empty;
	}

	public sealed record SimpleQueryResult
	{
		public int ResultCount { get; init; }
		public string[] Results { get; init; } = [];
	}

	public sealed record NoOpCommand : IDispatchAction;

	// Test Handlers - Minimal work to measure framework overhead
	public sealed class SimpleCommandHandler : IActionHandler<SimpleCommand>
	{
		public Task HandleAsync(SimpleCommand action, CancellationToken cancellationToken)
		{
			// Minimal work - just access the data to prevent optimization
			_ = action.Id;
			_ = action.Value;
			return Task.CompletedTask;
		}
	}

	public sealed class SimpleQueryHandler : IActionHandler<SimpleQuery, SimpleQueryResult>
	{
		public Task<SimpleQueryResult> HandleAsync(SimpleQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult(new SimpleQueryResult
			{
				ResultCount = 1,
				Results = [action.SearchTerm],
			});
		}
	}

	public sealed class NoOpCommandHandler : IActionHandler<NoOpCommand>
	{
		public Task HandleAsync(NoOpCommand action, CancellationToken cancellationToken)
		{
			return Task.CompletedTask;
		}
	}
}
