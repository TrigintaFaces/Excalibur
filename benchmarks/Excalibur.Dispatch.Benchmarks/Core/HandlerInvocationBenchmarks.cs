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
/// Benchmarks for handler invocation performance.
/// Measures the overhead of dispatching messages to handlers through the pipeline.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-mwnbc: Handler Invocation Benchmarks (10 scenarios).
///
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single handler invocation: &lt; 1μs (P50), &lt; 2μs (P95)
/// - Multiple handlers (10): &lt; 10μs (P50), &lt; 20μs (P95)
/// - Handler with DI: &lt; 5μs (P50), &lt; 10μs (P95)
/// - Handler activation: &lt; 5μs
/// - Batch handlers (50): &lt; 100μs (P50)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class HandlerInvocationBenchmarks
{
	private IServiceProvider? _serviceProvider;
	private IServiceProvider? _scopedServiceProvider;
	private IServiceProvider? _singletonServiceProvider;
	private IDispatcher? _dispatcher;
	private IDispatcher? _scopedDispatcher;
	private IDispatcher? _singletonDispatcher;
	private IMessageContext? _context;
	private IMessageContext? _scopedContext;
	private IMessageContext? _singletonContext;

	// Test Service and Handler with DI
	private interface ITestService
	{
		int Process(int value);
	}

	/// <summary>
	/// Initialize Dispatch pipeline before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Setup 1: Default (transient handlers) - baseline
		var services = new ServiceCollection();
		_ = services.AddBenchmarkDispatch();
		_ = services.AddTransient<IActionHandler<TestAction>, TestActionHandler>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler1>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler2>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler3>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler4>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler5>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler6>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler7>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler8>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler9>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler10>();
		_ = services.AddTransient<IActionHandler<TestQuery, string>, TestQueryHandler>();
		_ = services.AddSingleton<ITestService, TestService>();
		_ = services.AddTransient<IActionHandler<TestActionWithDI>, TestActionWithDIHandler>();

		// Register batch event handlers (50 handlers)
		for (var i = 0; i < 50; i++)
		{
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler>();
		}

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		// Create context via factory (IMessageContext is per-request, not DI-registered)
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_context = contextFactory.CreateContext();

		// Setup 2: Scoped handlers - for resolution benchmarks
		var scopedServices = new ServiceCollection();
		_ = scopedServices.AddBenchmarkDispatch();
		_ = scopedServices.AddScoped<IActionHandler<TestAction>, TestActionHandler>();
		_ = scopedServices.AddSingleton<ITestService, TestService>();

		_scopedServiceProvider = scopedServices.BuildServiceProvider();
		_scopedDispatcher = _scopedServiceProvider.GetRequiredService<IDispatcher>();
		var scopedContextFactory = _scopedServiceProvider.GetRequiredService<IMessageContextFactory>();
		_scopedContext = scopedContextFactory.CreateContext();

		// Setup 3: Singleton handlers - for resolution benchmarks
		var singletonServices = new ServiceCollection();
		_ = singletonServices.AddBenchmarkDispatch();
		_ = singletonServices.AddSingleton<IActionHandler<TestAction>, TestActionHandler>();
		_ = singletonServices.AddSingleton<ITestService, TestService>();

		_singletonServiceProvider = singletonServices.BuildServiceProvider();
		_singletonDispatcher = _singletonServiceProvider.GetRequiredService<IDispatcher>();
		var singletonContextFactory = _singletonServiceProvider.GetRequiredService<IMessageContextFactory>();
		_singletonContext = singletonContextFactory.CreateContext();
	}

	/// <summary>
	/// Cleanup service providers after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}

		if (_scopedServiceProvider is IDisposable scopedDisposable)
		{
			scopedDisposable.Dispose();
		}

		if (_singletonServiceProvider is IDisposable singletonDisposable)
		{
			singletonDisposable.Dispose();
		}
	}

	/// <summary>
	/// Benchmark: Single action handler (baseline).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> InvokeSingleActionHandler()
	{
		var action = new TestAction { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Single event handler.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeSingleEventHandler()
	{
		var @event = new TestEvent { Data = "test" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Single query handler (with return value).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult<string>> InvokeSingleQueryHandler()
	{
		var query = new TestQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<TestQuery, string>(query, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Multiple event handlers (10 handlers for same event).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeMultipleEventHandlers()
	{
		var @event = new TestEvent { Data = "test" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Handler with dependency injection.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeHandlerWithDI()
	{
		var action = new TestActionWithDI { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	// ========================================================================
	// Sprint 185 - New Benchmark Scenarios (bd-mwnbc)
	// ========================================================================

	/// <summary>
	/// Benchmark: Handler activation from DI container.
	/// Measures pure DI resolution overhead without dispatch pipeline.
	/// Target: &lt; 5μs
	/// </summary>
	[Benchmark(Description = "Handler Activation (DI resolution only)")]
	public object HandlerActivation()
	{
		using var scope = _serviceProvider.CreateScope();
		return scope.ServiceProvider.GetRequiredService<IActionHandler<TestAction>>();
	}

	/// <summary>
	/// Benchmark: Transient handler resolution with dispatch.
	/// Baseline for handler lifetime comparison.
	/// </summary>
	[Benchmark(Description = "Transient Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionTransient()
	{
		var action = new TestAction { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Scoped handler resolution with dispatch.
	/// Handlers resolved once per scope.
	/// </summary>
	[Benchmark(Description = "Scoped Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionScoped()
	{
		var action = new TestAction { Value = 42 };
		return await _scopedDispatcher.DispatchAsync(action, _scopedContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Singleton handler resolution with dispatch.
	/// Handlers resolved once for entire application lifetime.
	/// Expected to be fastest due to no allocation overhead.
	/// </summary>
	[Benchmark(Description = "Singleton Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionSingleton()
	{
		var action = new TestAction { Value = 42 };
		return await _singletonDispatcher.DispatchAsync(action, _singletonContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Batch event handlers (50 handlers for same event).
	/// Tests scaling behavior with large handler counts.
	/// Target: &lt; 100μs (P50)
	/// </summary>
	[Benchmark(Description = "Batch Handlers (50 handlers)")]
	public async Task<IMessageResult> InvokeBatchHandlers()
	{
		var @event = new BatchTestEvent { Data = "batch-test" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
	}

	// Test Messages
	private record TestAction : IDispatchAction
	{
		public int Value { get; init; }
	}

	private record TestEvent : IDispatchEvent
	{
		public string Data { get; init; } = string.Empty;
	}

	private record TestQuery : IDispatchAction<string>
	{
		public int Id { get; init; }
	}

	private record TestActionWithDI : IDispatchAction
	{
		public int Value { get; init; }
	}

	/// <summary>
	/// Batch test event for 50-handler benchmark.
	/// </summary>
	private record BatchTestEvent : IDispatchEvent
	{
		public string Data { get; init; } = string.Empty;
	}

	// Test Handlers
	private class TestActionHandler : IActionHandler<TestAction>
	{
		public Task HandleAsync(TestAction action, CancellationToken cancellationToken = default)
		{
			// Minimal work for performance baseline
			_ = action.Value * 2;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler1 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler2 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler3 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler4 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler5 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler6 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler7 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler8 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler9 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler10 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestQueryHandler : IActionHandler<TestQuery, string>
	{
		public Task<string> HandleAsync(TestQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult($"Result-{action.Id}");
		}
	}

	private class TestService : ITestService
	{
		public int Process(int value) => value * 2;
	}

	private class TestActionWithDIHandler(ITestService service) : IActionHandler<TestActionWithDI>
	{
		public Task HandleAsync(TestActionWithDI action, CancellationToken cancellationToken = default)
		{
			_ = service.Process(action.Value);
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Batch event handler for 50-handler scaling benchmark.
	/// </summary>
	private class BatchEventHandler : IEventHandler<BatchTestEvent>
	{
		public Task HandleAsync(BatchTestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}
}
