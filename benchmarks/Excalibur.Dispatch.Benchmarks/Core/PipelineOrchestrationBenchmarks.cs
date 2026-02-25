// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under MIT. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks for full pipeline orchestration scenarios.
/// Measures end-to-end performance of complete dispatch workflows including middleware, handlers, and context.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Full action pipeline: &lt; 2us (P50), &lt; 4us (P95)
/// - Full event pipeline (3 handlers): &lt; 5us (P50), &lt; 10us (P95)
/// - Pipeline with context: &lt; 3us (P50), &lt; 6us (P95)
/// - Complex pipeline (5 middleware + 3 handlers): &lt; 10us (P50), &lt; 20us (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class PipelineOrchestrationBenchmarks
{
	private IServiceProvider? _serviceProvider;
	private IDispatcher? _dispatcher;
	private IMessageContextFactory? _contextFactory;
	private IMessageContext? _context;

	/// <summary>
	/// Initialize Dispatch pipeline for orchestration benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();

		// Add core Dispatch services
		_ = services.AddBenchmarkDispatch();

		// Register handlers for different message types
		_ = services.AddTransient<IActionHandler<TestAction>, TestActionHandler>();
		_ = services.AddTransient<IActionHandler<TestQuery, string>, TestQueryHandler>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler1>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler2>();
		_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler3>();

		// Register context-aware handler
		_ = services.AddTransient<IActionHandler<ContextAwareAction>, ContextAwareActionHandler>();

		// Register middleware for complex scenario
		_ = services.AddMiddleware<ContextMiddleware>();
		_ = services.AddMiddleware<PassthroughMiddleware1>();
		_ = services.AddMiddleware<PassthroughMiddleware2>();
		_ = services.AddMiddleware<PassthroughMiddleware3>();
		_ = services.AddMiddleware<PassthroughMiddleware4>();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();

		// Create context via factory (IMessageContext is per-request, not DI-registered)
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_context = _contextFactory.CreateContext();
	}

	/// <summary>
	/// Cleanup service provider after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Benchmark: Full action pipeline (baseline - simple action through complete pipeline).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> FullActionPipeline()
	{
		var action = new TestAction { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Full event pipeline with 3 handlers.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> FullEventPipelineMultipleHandlers()
	{
		var @event = new TestEvent { Data = "test-data" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Full query pipeline with return value.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult<string>> FullQueryPipeline()
	{
		var query = new TestQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<TestQuery, string>(query, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Pipeline with context propagation (middleware sets context, handler reads it).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> PipelineWithContextPropagation()
	{
		var action = new ContextAwareAction { RequestId = "req-123" };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Complex pipeline with 5 middleware + 3 event handlers.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> ComplexPipelineWorkflow()
	{
		var @event = new TestEvent { Data = "complex-workflow" };
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

	private record ContextAwareAction : IDispatchAction
	{
		public string RequestId { get; init; } = string.Empty;
	}

	// Test Handlers
	private class TestActionHandler : IActionHandler<TestAction>
	{
		public Task HandleAsync(TestAction action, CancellationToken cancellationToken = default)
		{
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
			_ = eventMessage.Data.GetHashCode(StringComparison.Ordinal);
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler3 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.ToUpperInvariant();
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

	private class ContextAwareActionHandler : IActionHandler<ContextAwareAction>
	{
		public Task HandleAsync(ContextAwareAction action, CancellationToken cancellationToken = default)
		{
			// Handler would normally read context data here
			_ = action.RequestId.Length;
			return Task.CompletedTask;
		}
	}

	// Middleware Components
	private class ContextMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public async ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Simulate setting context data
			context.SetItem("TraceId", Guid.NewGuid().ToString());
			context.SetItem("Timestamp", DateTimeOffset.UtcNow);

			return await nextDelegate(message, context, cancellationToken);
		}
	}

	private class PassthroughMiddleware1 : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			_ = message.GetType().Name.Length;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private class PassthroughMiddleware2 : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			_ = context.GetItem<object>("TraceId");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private class PassthroughMiddleware3 : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Processing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			_ = context.GetItem<object>("Timestamp");
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private class PassthroughMiddleware4 : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PostProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			_ = message.GetType().FullName;
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
