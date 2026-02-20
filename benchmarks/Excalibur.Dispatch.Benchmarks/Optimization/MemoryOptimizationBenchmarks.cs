// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks comparing standard vs pooled (zero-allocation) dispatch modes.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks validate memory optimization achievements:
/// </para>
/// <list type="bullet">
/// <item>Closure allocation elimination per dispatch</item>
/// <item>Context pooling effectiveness</item>
/// <item>JIT devirtualization through sealed classes</item>
/// <item>Static lambda optimizations</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MemoryOptimizationBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IServiceProvider _pooledServiceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IDispatcher _pooledDispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;
	private IMessageContextFactory _pooledContextFactory = null!;
	private TestCommand _command = null!;

	[GlobalSetup]
	public void Setup()
	{
		// Standard dispatcher (no pooling)
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = services.AddTransient<TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		_ = services.AddDispatch();

		// Add some middleware to validate chain builder
		_ = services.AddMiddleware<LoggingMiddleware>();
		_ = services.AddMiddleware<ValidationMiddleware>();
		_ = services.AddMiddleware<MetricsMiddleware>();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Pooled dispatcher (zero-allocation mode)
		var pooledServices = new ServiceCollection();
		_ = pooledServices.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = pooledServices.AddTransient<TestCommandHandler>();
		_ = pooledServices.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		_ = pooledServices.AddDispatch(builder =>
		{
			_ = builder.UseZeroAllocation();
		});

		_ = pooledServices.AddMiddleware<LoggingMiddleware>();
		_ = pooledServices.AddMiddleware<ValidationMiddleware>();
		_ = pooledServices.AddMiddleware<MetricsMiddleware>();

		_pooledServiceProvider = pooledServices.BuildServiceProvider();
		_pooledDispatcher = _pooledServiceProvider.GetRequiredService<IDispatcher>();
		_pooledContextFactory = _pooledServiceProvider.GetRequiredService<IMessageContextFactory>();

		_command = new TestCommand { OrderId = Guid.NewGuid(), CustomerId = "customer-123" };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
		(_pooledServiceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Baseline: Standard dispatch with middleware.
	/// </summary>
	[Benchmark(Baseline = true)]
	public Task<IMessageResult> StandardDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Pooled dispatch with zero-allocation mode enabled.
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> PooledDispatch()
	{
		var context = _pooledContextFactory.CreateContext();
		return _pooledDispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Measures context creation overhead (standard factory).
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext_Standard()
	{
		return _contextFactory.CreateContext();
	}

	/// <summary>
	/// Measures context creation overhead (pooled factory).
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext_Pooled()
	{
		return _pooledContextFactory.CreateContext();
	}

	/// <summary>
	/// Batch of 100 standard dispatches.
	/// </summary>
	[Benchmark]
	public async Task<int> StandardDispatch_Batch100()
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
	/// Batch of 100 pooled dispatches.
	/// </summary>
	[Benchmark]
	public async Task<int> PooledDispatch_Batch100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var context = _pooledContextFactory.CreateContext();
			var result = await _pooledDispatcher.DispatchAsync(_command, context, CancellationToken.None);
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Concurrent dispatch (10 parallel operations) - standard.
	/// </summary>
	[Benchmark]
	public async Task<int> ConcurrentDispatch_Standard()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks);
		return results.Count(r => r.Succeeded);
	}

	/// <summary>
	/// Concurrent pooled dispatch (10 parallel operations).
	/// </summary>
	[Benchmark]
	public async Task<int> ConcurrentDispatch_Pooled()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _pooledContextFactory.CreateContext();
			tasks[i] = _pooledDispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks);
		return results.Count(r => r.Succeeded);
	}

	// Test types
	private sealed record TestCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
		public string CustomerId { get; init; } = string.Empty;
	}

	private sealed class TestCommandHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
		{
			// Minimal work
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
