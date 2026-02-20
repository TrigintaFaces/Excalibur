// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// End-to-end throughput benchmarks for Dispatch message processing.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks measure dispatch throughput and latency with a representative middleware stack.
/// </para>
/// <list type="bullet">
/// <item>Full dispatch with middleware pipeline</item>
/// <item>Sequential and parallel throughput scenarios</item>
/// <item>Mixed workload patterns</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class DispatchThroughputBenchmarks
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

		// Add representative middleware stack (3 layers)
		_ = services.AddMiddleware<LoggingMiddleware>();
		_ = services.AddMiddleware<ValidationMiddleware>();
		_ = services.AddMiddleware<MetricsMiddleware>();

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
	/// Full dispatch with middleware - baseline measurement.
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> FullDispatch_WithMiddleware()
	{
		var context = _contextFactory.CreateContext();
		return await _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Dispatch without awaiting - returns Task directly.
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> Dispatch_NoAwait()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Context creation - isolated allocation measurement.
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext()
	{
		return _contextFactory.CreateContext();
	}

	/// <summary>
	/// Throughput test - 100 sequential dispatches.
	/// </summary>
	[Benchmark]
	public async Task<int> Throughput_100Sequential()
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
	/// Concurrent throughput - 10 parallel dispatches.
	/// </summary>
	[Benchmark]
	public async Task<int> Throughput_10Parallel()
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

	/// <summary>
	/// High concurrency - 100 parallel dispatches.
	/// </summary>
	[Benchmark]
	public async Task<int> Throughput_100Parallel()
	{
		var tasks = new Task<IMessageResult>[100];
		for (var i = 0; i < 100; i++)
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

	/// <summary>
	/// Mixed workload - alternating sync and async patterns.
	/// </summary>
	[Benchmark]
	public async Task<int> MixedWorkload()
	{
		var count = 0;

		// First batch (fast handler path)
		for (var i = 0; i < 50; i++)
		{
			var context = _contextFactory.CreateContext();
			var task = _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
			if (task.IsCompletedSuccessfully)
			{
				if (task.Result.Succeeded)
				{
					count++;
				}
			}
			else
			{
				var result = await task;
				if (result.Succeeded)
				{
					count++;
				}
			}
		}

		// Second batch (awaited path)
		for (var i = 0; i < 50; i++)
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
			// Fast synchronous completion
			_ = command.OrderId;
			return Task.CompletedTask;
		}
	}

	private sealed class LoggingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal logging simulation
			_ = message.GetType().Name;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class ValidationMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal validation simulation
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class MetricsMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal metrics simulation
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
