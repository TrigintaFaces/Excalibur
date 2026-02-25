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
/// Benchmarks for middleware chain execution performance.
/// Measures the overhead of middleware chains with varying lengths and short-circuit scenarios.
/// </summary>
/// <remarks>
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - No middleware (baseline): &lt; 1us (P50), &lt; 2us (P95)
/// - Single middleware: &lt; 2us (P50), &lt; 4us (P95)
/// - 5 middleware chain: &lt; 5us (P50), &lt; 10us (P95)
/// - 10 middleware chain: &lt; 10us (P50), &lt; 20us (P95)
/// - Short-circuit: &lt; 2us (P50), &lt; 4us (P95)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MiddlewareChainBenchmarks
{
	private IServiceProvider? _noMiddlewareProvider;
	private IServiceProvider? _oneMiddlewareProvider;
	private IServiceProvider? _fiveMiddlewareProvider;
	private IServiceProvider? _tenMiddlewareProvider;
	private IServiceProvider? _shortCircuitProvider;

	private IDispatcher? _noMiddlewareDispatcher;
	private IDispatcher? _oneMiddlewareDispatcher;
	private IDispatcher? _fiveMiddlewareDispatcher;
	private IDispatcher? _tenMiddlewareDispatcher;
	private IDispatcher? _shortCircuitDispatcher;

	private IMessageContextFactory? _contextFactory;
	private IMessageContext? _context;

	/// <summary>
	/// Initialize Dispatch pipelines with different middleware configurations.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		// Setup: No middleware (baseline)
		_noMiddlewareProvider = CreateServiceProvider(0, shortCircuit: false);
		_noMiddlewareDispatcher = _noMiddlewareProvider.GetRequiredService<IDispatcher>();

		// Setup: 1 middleware
		_oneMiddlewareProvider = CreateServiceProvider(1, shortCircuit: false);
		_oneMiddlewareDispatcher = _oneMiddlewareProvider.GetRequiredService<IDispatcher>();

		// Setup: 5 middleware
		_fiveMiddlewareProvider = CreateServiceProvider(5, shortCircuit: false);
		_fiveMiddlewareDispatcher = _fiveMiddlewareProvider.GetRequiredService<IDispatcher>();

		// Setup: 10 middleware
		_tenMiddlewareProvider = CreateServiceProvider(10, shortCircuit: false);
		_tenMiddlewareDispatcher = _tenMiddlewareProvider.GetRequiredService<IDispatcher>();

		// Setup: Short-circuit middleware
		_shortCircuitProvider = CreateServiceProvider(5, shortCircuit: true);
		_shortCircuitDispatcher = _shortCircuitProvider.GetRequiredService<IDispatcher>();

		// Create context via factory (IMessageContext is per-request, not DI-registered)
		_contextFactory = _noMiddlewareProvider.GetRequiredService<IMessageContextFactory>();
		_context = _contextFactory.CreateContext();
	}

	/// <summary>
	/// Cleanup service providers after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		DisposeProvider(_noMiddlewareProvider);
		DisposeProvider(_oneMiddlewareProvider);
		DisposeProvider(_fiveMiddlewareProvider);
		DisposeProvider(_tenMiddlewareProvider);
		DisposeProvider(_shortCircuitProvider);
	}

	/// <summary>
	/// Benchmark: No middleware (baseline).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> NoMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _noMiddlewareDispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Single middleware.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> OneMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _oneMiddlewareDispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: 5 middleware in chain.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> FiveMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _fiveMiddlewareDispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: 10 middleware in chain.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> TenMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _tenMiddlewareDispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Middleware short-circuit (first middleware stops pipeline).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> MiddlewareShortCircuit()
	{
		var action = new TestAction { Value = 42 };
		return await _shortCircuitDispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	private static IServiceProvider CreateServiceProvider(int middlewareCount, bool shortCircuit)
	{
		var services = new ServiceCollection();

		// Add core Dispatch services
		_ = services.AddBenchmarkDispatch();

		// Register test handler
		_ = services.AddTransient<IActionHandler<TestAction>, TestActionHandler>();

		// Register middleware
		if (shortCircuit)
		{
			// First middleware short-circuits
			_ = services.AddMiddleware<ShortCircuitTestMiddleware>();
		}
		else
		{
			// Add specified number of passthrough middleware
			for (int i = 0; i < middlewareCount; i++)
			{
				_ = services.AddMiddleware<PassthroughTestMiddleware>();
			}
		}

		return services.BuildServiceProvider();
	}

	private static void DisposeProvider(IServiceProvider? provider)
	{
		if (provider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	// Test Message
	private record TestAction : IDispatchAction
	{
		public int Value { get; init; }
	}

	// Test Handler
	private class TestActionHandler : IActionHandler<TestAction>
	{
		public Task HandleAsync(TestAction action, CancellationToken cancellationToken = default)
		{
			// Minimal work for performance baseline
			_ = action.Value * 2;
			return Task.CompletedTask;
		}
	}

	// Passthrough Middleware (does minimal work and continues chain)
	private class PassthroughTestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal work to simulate middleware overhead
			_ = message.GetType().Name.Length;

			// Continue to next middleware/handler
			return nextDelegate(message, context, cancellationToken);
		}
	}

	// Short-Circuit Middleware (stops pipeline early)
	private class ShortCircuitTestMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Short-circuit: don't call nextDelegate
			// Return a success result without invoking handler
			return new ValueTask<IMessageResult>(
				new MessageResult { Succeeded = true });
		}
	}

	// Simple MessageResult implementation for short-circuit
	private class MessageResult : IMessageResult
	{
		public bool Succeeded { get; init; } = true;
		public string? ErrorMessage { get; init; }
		public bool CacheHit { get; init; }
		public object? ValidationResult { get; init; }
		public object? AuthorizationResult { get; init; }
		public IMessageProblemDetails? ProblemDetails { get; init; }
	}
}
