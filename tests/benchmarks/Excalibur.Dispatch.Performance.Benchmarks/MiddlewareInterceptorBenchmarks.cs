// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;
using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.DependencyInjection;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for middleware invocation interceptors (PERF-10 Phase 1).
/// Compares direct typed invocation vs interface dispatch vs FrozenDictionary registry.
/// </summary>
/// <remarks>
/// Sprint 456 - S456.5: Benchmark suite for middleware interceptor performance.
/// Validates:
/// 1. Direct typed invocation (intercepted path) - baseline
/// 2. Interface dispatch (current path) - pre-optimization
/// 3. FrozenDictionary registry lookup (fallback path)
/// 4. Pipeline execution at various depths (3, 5 stages)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class MiddlewareInterceptorBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private MessageContext _context = null!;
	private BenchmarkMessage _message = null!;

	// Middleware instances for direct invocation
	private BenchmarkLoggingMiddleware _loggingMiddleware = null!;
	private BenchmarkValidationMiddleware _validationMiddleware = null!;
	private BenchmarkAuthorizationMiddleware _authorizationMiddleware = null!;
	private BenchmarkMetricsMiddleware _metricsMiddleware = null!;
	private BenchmarkRoutingMiddleware _routingMiddleware = null!;

	// Interface references for interface dispatch comparison
	private IDispatchMiddleware _loggingMiddlewareInterface = null!;
	private IDispatchMiddleware _validationMiddlewareInterface = null!;

	// Registry for FrozenDictionary lookup
	private FrozenDictionary<Type, Func<IDispatchMiddleware, IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>>> _frozenInvokerRegistry = null!;
	private ConcurrentDictionary<Type, Func<IDispatchMiddleware, IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>>> _concurrentInvokerRegistry = null!;

	// Terminal delegate for pipelines
	private DispatchRequestDelegate _terminalDelegate = null!;

	// Pre-built pipelines
	private Func<ValueTask<IMessageResult>> _pipeline3Stages = null!;
	private Func<ValueTask<IMessageResult>> _pipeline5Stages = null!;
	private Func<ValueTask<IMessageResult>> _pipeline3StagesInterfaceDispatch = null!;
	private Func<ValueTask<IMessageResult>> _pipeline5StagesInterfaceDispatch = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_serviceProvider = services.BuildServiceProvider();

		_message = new BenchmarkMessage { Id = Guid.NewGuid(), Data = "BenchmarkData" };
		_context = new MessageContext(_message, _serviceProvider);

		// Create middleware instances
		_loggingMiddleware = new BenchmarkLoggingMiddleware();
		_validationMiddleware = new BenchmarkValidationMiddleware();
		_authorizationMiddleware = new BenchmarkAuthorizationMiddleware();
		_metricsMiddleware = new BenchmarkMetricsMiddleware();
		_routingMiddleware = new BenchmarkRoutingMiddleware();

		// Interface references
		_loggingMiddlewareInterface = _loggingMiddleware;
		_validationMiddlewareInterface = _validationMiddleware;

		// Build invoker registries
		var invokerMap = new Dictionary<Type, Func<IDispatchMiddleware, IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>>>
		{
			[typeof(BenchmarkLoggingMiddleware)] = (middleware, msg, ctx, next, ct) =>
				((BenchmarkLoggingMiddleware)middleware).InvokeAsync(msg, ctx, next, ct),
			[typeof(BenchmarkValidationMiddleware)] = (middleware, msg, ctx, next, ct) =>
				((BenchmarkValidationMiddleware)middleware).InvokeAsync(msg, ctx, next, ct),
			[typeof(BenchmarkAuthorizationMiddleware)] = (middleware, msg, ctx, next, ct) =>
				((BenchmarkAuthorizationMiddleware)middleware).InvokeAsync(msg, ctx, next, ct),
			[typeof(BenchmarkMetricsMiddleware)] = (middleware, msg, ctx, next, ct) =>
				((BenchmarkMetricsMiddleware)middleware).InvokeAsync(msg, ctx, next, ct),
			[typeof(BenchmarkRoutingMiddleware)] = (middleware, msg, ctx, next, ct) =>
				((BenchmarkRoutingMiddleware)middleware).InvokeAsync(msg, ctx, next, ct)
		};

		_frozenInvokerRegistry = invokerMap.ToFrozenDictionary();
		_concurrentInvokerRegistry = new ConcurrentDictionary<Type, Func<IDispatchMiddleware, IDispatchMessage, IMessageContext, DispatchRequestDelegate, CancellationToken, ValueTask<IMessageResult>>>(invokerMap);

		// Terminal delegate
		_terminalDelegate = (msg, ctx, ct) => new ValueTask<IMessageResult>(MessageResult.Success());

		// Build pipelines with direct typed invocation (3 stages)
		_pipeline3Stages = BuildDirectTypedPipeline(
			_loggingMiddleware,
			_validationMiddleware,
			_authorizationMiddleware);

		// Build pipelines with direct typed invocation (5 stages)
		_pipeline5Stages = BuildDirectTypedPipeline(
			_loggingMiddleware,
			_validationMiddleware,
			_authorizationMiddleware,
			_metricsMiddleware,
			_routingMiddleware);

		// Build pipelines with interface dispatch (3 stages)
		_pipeline3StagesInterfaceDispatch = BuildInterfaceDispatchPipeline(
			_loggingMiddlewareInterface,
			_validationMiddlewareInterface,
			_authorizationMiddleware);

		// Build pipelines with interface dispatch (5 stages)
		_pipeline5StagesInterfaceDispatch = BuildInterfaceDispatchPipeline(
			_loggingMiddlewareInterface,
			_validationMiddlewareInterface,
			_authorizationMiddleware,
			_metricsMiddleware,
			_routingMiddleware);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	#region Direct Typed Invocation (Intercepted Path - Baseline)

	/// <summary>
	/// Baseline: Direct typed middleware invocation (what interceptors achieve).
	/// Eliminates interface dispatch overhead via compile-time type knowledge.
	/// </summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("DirectTyped")]
	public ValueTask<IMessageResult> DirectTypedInvocation_SingleMiddleware()
	{
		return _loggingMiddleware.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
	}

	/// <summary>
	/// Direct typed invocation of two middleware in sequence.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("DirectTyped")]
	public async ValueTask<IMessageResult> DirectTypedInvocation_TwoMiddleware()
	{
		return await _loggingMiddleware.InvokeAsync(_message, _context, (msg, ctx, ct) =>
			_validationMiddleware.InvokeAsync(msg, ctx, _terminalDelegate, ct), CancellationToken.None);
	}

	#endregion

	#region Interface Dispatch (Current Path)

	/// <summary>
	/// Interface dispatch: Current middleware invocation path.
	/// Incurs virtual dispatch overhead at each call.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("InterfaceDispatch")]
	public ValueTask<IMessageResult> InterfaceDispatch_SingleMiddleware()
	{
		return _loggingMiddlewareInterface.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
	}

	/// <summary>
	/// Interface dispatch of two middleware in sequence.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("InterfaceDispatch")]
	public async ValueTask<IMessageResult> InterfaceDispatch_TwoMiddleware()
	{
		return await _loggingMiddlewareInterface.InvokeAsync(_message, _context, (msg, ctx, ct) =>
			_validationMiddlewareInterface.InvokeAsync(msg, ctx, _terminalDelegate, ct), CancellationToken.None);
	}

	#endregion

	#region FrozenDictionary Registry (Fallback Path)

	/// <summary>
	/// FrozenDictionary registry lookup + direct typed invocation.
	/// This is the fallback path for dynamically-registered middleware.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FrozenRegistry")]
	public ValueTask<IMessageResult> FrozenRegistry_LookupAndInvoke()
	{
		if (_frozenInvokerRegistry.TryGetValue(typeof(BenchmarkLoggingMiddleware), out var invoker))
		{
			return invoker(_loggingMiddleware, _message, _context, _terminalDelegate, CancellationToken.None);
		}
		return new ValueTask<IMessageResult>(MessageResult.Failed("Middleware not found"));
	}

	/// <summary>
	/// ConcurrentDictionary registry lookup (pre-optimization comparison).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FrozenRegistry")]
	public ValueTask<IMessageResult> ConcurrentRegistry_LookupAndInvoke()
	{
		if (_concurrentInvokerRegistry.TryGetValue(typeof(BenchmarkLoggingMiddleware), out var invoker))
		{
			return invoker(_loggingMiddleware, _message, _context, _terminalDelegate, CancellationToken.None);
		}
		return new ValueTask<IMessageResult>(MessageResult.Failed("Middleware not found"));
	}

	#endregion

	#region Pipeline Execution (3 Stages)

	/// <summary>
	/// 3-stage pipeline with direct typed invocation (intercepted).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Pipeline3")]
	public ValueTask<IMessageResult> Pipeline3Stages_DirectTyped()
	{
		return _pipeline3Stages();
	}

	/// <summary>
	/// 3-stage pipeline with interface dispatch (current).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Pipeline3")]
	public ValueTask<IMessageResult> Pipeline3Stages_InterfaceDispatch()
	{
		return _pipeline3StagesInterfaceDispatch();
	}

	#endregion

	#region Pipeline Execution (5 Stages)

	/// <summary>
	/// 5-stage pipeline with direct typed invocation (intercepted).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Pipeline5")]
	public ValueTask<IMessageResult> Pipeline5Stages_DirectTyped()
	{
		return _pipeline5Stages();
	}

	/// <summary>
	/// 5-stage pipeline with interface dispatch (current).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Pipeline5")]
	public ValueTask<IMessageResult> Pipeline5Stages_InterfaceDispatch()
	{
		return _pipeline5StagesInterfaceDispatch();
	}

	#endregion

	#region Throughput Benchmarks

	/// <summary>
	/// Batch of 100 direct typed invocations.
	/// Represents best-case interceptor throughput.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task Throughput_DirectTyped_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			_ = await _loggingMiddleware.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
		}
	}

	/// <summary>
	/// Batch of 100 interface dispatch invocations.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task Throughput_InterfaceDispatch_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			_ = await _loggingMiddlewareInterface.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
		}
	}

	/// <summary>
	/// Batch of 100 FrozenDictionary registry lookups.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task Throughput_FrozenRegistry_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			if (_frozenInvokerRegistry.TryGetValue(typeof(BenchmarkLoggingMiddleware), out var invoker))
			{
				_ = await invoker(_loggingMiddleware, _message, _context, _terminalDelegate, CancellationToken.None);
			}
		}
	}

	/// <summary>
	/// Batch of 100 5-stage pipeline executions (direct typed).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task Throughput_Pipeline5_DirectTyped_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			_ = await _pipeline5Stages();
		}
	}

	#endregion

	#region Memory Allocation Comparison

	/// <summary>
	/// Memory measurement: 10 direct typed invocations.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_DirectTyped_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			_ = await _loggingMiddleware.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
		}
	}

	/// <summary>
	/// Memory measurement: 10 interface dispatch invocations.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_InterfaceDispatch_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			_ = await _loggingMiddlewareInterface.InvokeAsync(_message, _context, _terminalDelegate, CancellationToken.None);
		}
	}

	/// <summary>
	/// Memory measurement: 10 FrozenDictionary registry lookups.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_FrozenRegistry_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			if (_frozenInvokerRegistry.TryGetValue(typeof(BenchmarkLoggingMiddleware), out var invoker))
			{
				_ = await invoker(_loggingMiddleware, _message, _context, _terminalDelegate, CancellationToken.None);
			}
		}
	}

	/// <summary>
	/// Memory measurement: 10 3-stage pipeline executions.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_Pipeline3_DirectTyped_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			_ = await _pipeline3Stages();
		}
	}

	#endregion

	#region Pipeline Builders

	private Func<ValueTask<IMessageResult>> BuildDirectTypedPipeline(params IDispatchMiddleware[] middlewares)
	{
		DispatchRequestDelegate current = _terminalDelegate;

		// Build pipeline in reverse order
		for (int i = middlewares.Length - 1; i >= 0; i--)
		{
			var middleware = middlewares[i];
			var next = current;
			current = (msg, ctx, ct) => middleware switch
			{
				BenchmarkLoggingMiddleware logging => logging.InvokeAsync(msg, ctx, next, ct),
				BenchmarkValidationMiddleware validation => validation.InvokeAsync(msg, ctx, next, ct),
				BenchmarkAuthorizationMiddleware auth => auth.InvokeAsync(msg, ctx, next, ct),
				BenchmarkMetricsMiddleware metrics => metrics.InvokeAsync(msg, ctx, next, ct),
				BenchmarkRoutingMiddleware routing => routing.InvokeAsync(msg, ctx, next, ct),
				_ => middleware.InvokeAsync(msg, ctx, next, ct) // Fallback to interface
			};
		}

		var finalDelegate = current;
		return () => finalDelegate(_message, _context, CancellationToken.None);
	}

	private Func<ValueTask<IMessageResult>> BuildInterfaceDispatchPipeline(params IDispatchMiddleware[] middlewares)
	{
		DispatchRequestDelegate current = _terminalDelegate;

		// Build pipeline in reverse order using interface dispatch
		for (int i = middlewares.Length - 1; i >= 0; i--)
		{
			var middleware = middlewares[i];
			var next = current;
			current = (msg, ctx, ct) => middleware.InvokeAsync(msg, ctx, next, ct);
		}

		var finalDelegate = current;
		return () => finalDelegate(_message, _context, CancellationToken.None);
	}

	#endregion

	#region Test Types

	private sealed record BenchmarkMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	/// <summary>
	/// Minimal logging middleware for benchmarking.
	/// </summary>
	private sealed class BenchmarkLoggingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Start;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal work to prevent optimization
			_ = message;
			_ = context;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Minimal validation middleware for benchmarking.
	/// </summary>
	private sealed class BenchmarkValidationMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Validation;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal validation check
			_ = message != null;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Minimal authorization middleware for benchmarking.
	/// </summary>
	private sealed class BenchmarkAuthorizationMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Authorization;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal auth check
			_ = context != null;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Minimal metrics middleware for benchmarking.
	/// </summary>
	private sealed class BenchmarkMetricsMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Instrumentation;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal metrics work
			_ = message;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	/// <summary>
	/// Minimal routing middleware for benchmarking.
	/// </summary>
	private sealed class BenchmarkRoutingMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.Routing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal routing work
			_ = context;
			return nextDelegate(message, context, cancellationToken);
		}
	}

	#endregion
}
