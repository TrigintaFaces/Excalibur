// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

using DispatchMessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks validating allocation reduction optimizations.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks measure allocation reduction from:
/// </para>
/// <list type="bullet">
/// <item>Cached reflection for handler resolution - Zero reflection in hot path</item>
/// <item>LINQ elimination in dispatch hot path - No iterator allocations</item>
/// <item>Collection capacity hints - Fewer list resize allocations</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AllocationOptimizationBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;
	private TestCommand _command = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = services.AddTransient<TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		_ = services.AddDispatch();

		// Add middleware to exercise the optimized filtering
		_ = services.AddMiddleware<PassthroughMiddleware>();
		_ = services.AddMiddleware<SecondPassthroughMiddleware>();
		_ = services.AddMiddleware<ThirdPassthroughMiddleware>();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		_command = new TestCommand { OrderId = Guid.NewGuid(), CustomerId = "customer-123" };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Single dispatch measuring end-to-end allocation.
	/// </summary>
	[Benchmark(Baseline = true)]
	public Task<IMessageResult> SingleDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Batch of 100 dispatches to measure sustained allocation behavior.
	/// </summary>
	[Benchmark]
	public async Task<int> Batch100Dispatches()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var context = _contextFactory.CreateContext();
			var result = await _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Context creation only - isolates factory allocation.
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext()
	{
		return _contextFactory.CreateContext();
	}

	/// <summary>
	/// Collection capacity hint effectiveness - pre-sized list.
	/// </summary>
	[Benchmark]
	public List<int> CollectionWithCapacity()
	{
		var list = new List<int>(10);
		for (var i = 0; i < 10; i++)
		{
			list.Add(i);
		}

		return list;
	}

	/// <summary>
	/// Collection without capacity hint - may resize.
	/// </summary>
	[Benchmark]
	public List<int> CollectionWithoutCapacity()
	{
		var list = new List<int>();
		for (var i = 0; i < 10; i++)
		{
			list.Add(i);
		}

		return list;
	}

	/// <summary>
	/// TryGetNonEnumeratedCount pattern for capacity hints.
	/// </summary>
	[Benchmark]
	public List<int> CollectionWithTryGetNonEnumeratedCount()
	{
		IEnumerable<int> source = Enumerable.Range(0, 10);
		var capacity = source.TryGetNonEnumeratedCount(out var count) ? count : 8;
		var list = new List<int>(capacity);
		foreach (var item in source)
		{
			list.Add(item);
		}

		return list;
	}

	/// <summary>
	/// Concurrent dispatches (10 parallel) to validate thread-safety of cached delegates.
	/// </summary>
	[Benchmark]
	public async Task<int> ConcurrentDispatches()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks);
		var count = 0;
		foreach (var result in results)
		{
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	// Test types
	private sealed record TestCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerId { get; init; } = string.Empty;
	}

	private sealed class TestCommandHandler : IActionHandler<TestCommand>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
		{
			// Minimal work
			_ = command.OrderId;
			return Task.CompletedTask;
		}
	}

	private sealed class PassthroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class SecondPassthroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ThirdPassthroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
