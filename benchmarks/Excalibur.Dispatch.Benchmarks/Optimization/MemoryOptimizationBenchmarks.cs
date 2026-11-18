// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks exercising the default and builder-overload registrations with three middleware stages.
/// </summary>
/// <remarks>
/// Both registrations use the default pooled context factory. The original timed methods
/// create contexts without returning them, so these cases measure rents without reuse,
/// not pooled-versus-unpooled behavior or a zero-allocation dispatch mode. Method names
/// retain their historical identifiers. Batch results use whole-invocation units.
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
		// Default registration overload.
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = services.AddTransient<TestCommandHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		_ = services.AddDispatch();

		// Add some middleware to validate chain builder
		_ = services.AddScoped<IDispatchMiddleware, LoggingMiddleware>();
		_ = services.AddScoped<IDispatchMiddleware, ValidationMiddleware>();
		_ = services.AddScoped<IDispatchMiddleware, MetricsMiddleware>();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Builder registration overload; the context factory is the same default implementation.
		var pooledServices = new ServiceCollection();
		_ = pooledServices.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = pooledServices.AddTransient<TestCommandHandler>();
		_ = pooledServices.AddTransient<IActionHandler<TestCommand>, TestCommandHandler>();

		// UseZeroAllocation() was removed: every registration it performed was already the
		// unconditional default. The builder-lambda overload is kept so this arm still calls Build(),
		// matching what it measured before.
		_ = pooledServices.AddDispatch(builder => { });

		_ = pooledServices.AddScoped<IDispatchMiddleware, LoggingMiddleware>();
		_ = pooledServices.AddScoped<IDispatchMiddleware, ValidationMiddleware>();
		_ = pooledServices.AddScoped<IDispatchMiddleware, MetricsMiddleware>();

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
	/// Dispatch through the builder-overload registration with the default context factory.
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> PooledDispatch()
	{
		var context = _pooledContextFactory.CreateContext();
		return _pooledDispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Measures context creation through the default registration; contexts are not returned.
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext_Standard()
	{
		return _contextFactory.CreateContext();
	}

	/// <summary>
	/// Measures context creation through the builder-overload registration; contexts are not returned.
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
	/// Batch of 100 dispatches through the builder-overload registration.
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
	/// Creates and joins ten dispatch tasks through the default registration; overlap is not guaranteed.
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
	/// Creates and joins ten dispatch tasks through the builder registration; overlap is not guaranteed.
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