// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
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
	private IMessageContext? _oneMiddlewareContext;
	private IMessageContext? _fiveMiddlewareContext;
	private IMessageContext? _tenMiddlewareContext;
	private IMessageContext? _shortCircuitContext;


	/// <summary>
	/// Initialize Dispatch pipelines with different middleware configurations.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		try
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
			_oneMiddlewareContext = _oneMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_fiveMiddlewareContext = _fiveMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_tenMiddlewareContext = _tenMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_shortCircuitContext = _shortCircuitProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
	
			ValidateComposition(_noMiddlewareProvider, []);
			ValidateComposition(_oneMiddlewareProvider, [typeof(PassthroughTestMiddleware<Stage1>)]);
			ValidateComposition(_fiveMiddlewareProvider, [typeof(PassthroughTestMiddleware<Stage1>), typeof(PassthroughTestMiddleware<Stage2>), typeof(PassthroughTestMiddleware<Stage3>), typeof(PassthroughTestMiddleware<Stage4>), typeof(PassthroughTestMiddleware<Stage5>)]);
			ValidateComposition(_tenMiddlewareProvider, [typeof(PassthroughTestMiddleware<Stage1>), typeof(PassthroughTestMiddleware<Stage2>), typeof(PassthroughTestMiddleware<Stage3>), typeof(PassthroughTestMiddleware<Stage4>), typeof(PassthroughTestMiddleware<Stage5>), typeof(PassthroughTestMiddleware<Stage6>), typeof(PassthroughTestMiddleware<Stage7>), typeof(PassthroughTestMiddleware<Stage8>), typeof(PassthroughTestMiddleware<Stage9>), typeof(PassthroughTestMiddleware<Stage10>)]);
			ValidateComposition(_shortCircuitProvider, [typeof(ShortCircuitTestMiddleware)]);
			if (!NoMiddleware().GetAwaiter().GetResult().Succeeded
				|| !OneMiddleware().GetAwaiter().GetResult().Succeeded
				|| !FiveMiddleware().GetAwaiter().GetResult().Succeeded
				|| !TenMiddleware().GetAwaiter().GetResult().Succeeded
				|| MiddlewareShortCircuit().GetAwaiter().GetResult() is not MessageResult)
			{
				throw new InvalidOperationException("Middleware benchmark did not execute its intended pipeline.");
			}
	
			_context = _contextFactory.CreateContext();
			_oneMiddlewareContext = _oneMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_fiveMiddlewareContext = _fiveMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_tenMiddlewareContext = _tenMiddlewareProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
			_shortCircuitContext = _shortCircuitProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
		}
		catch (Exception setupError)
		{
			try
			{
				GlobalCleanup();
			}
			catch (Exception cleanupError)
			{
				throw new AggregateException("Benchmark setup and cleanup failed.", setupError, cleanupError);
			}

			throw;
		}
	}

	/// <summary>
	/// Cleanup service providers after benchmarks.
	/// </summary>
	[GlobalCleanup]
	public void GlobalCleanup()
	{
		List<Exception>? failures = null;
		DisposeProvider(ref _noMiddlewareProvider, ref failures);
		DisposeProvider(ref _oneMiddlewareProvider, ref failures);
		DisposeProvider(ref _fiveMiddlewareProvider, ref failures);
		DisposeProvider(ref _tenMiddlewareProvider, ref failures);
		DisposeProvider(ref _shortCircuitProvider, ref failures);
		_noMiddlewareDispatcher = _oneMiddlewareDispatcher = _fiveMiddlewareDispatcher = _tenMiddlewareDispatcher = _shortCircuitDispatcher = null;
		_context = _oneMiddlewareContext = _fiveMiddlewareContext = _tenMiddlewareContext = _shortCircuitContext = null;
		_contextFactory = null;
		if (failures is not null)
		{
			throw new AggregateException("Benchmark provider cleanup failed.", failures);
		}

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
		return await _oneMiddlewareDispatcher.DispatchAsync(action, _oneMiddlewareContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: 5 middleware in chain.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> FiveMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _fiveMiddlewareDispatcher.DispatchAsync(action, _fiveMiddlewareContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: 10 middleware in chain.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> TenMiddleware()
	{
		var action = new TestAction { Value = 42 };
		return await _tenMiddlewareDispatcher.DispatchAsync(action, _tenMiddlewareContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Middleware short-circuit (first middleware stops pipeline).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> MiddlewareShortCircuit()
	{
		var action = new TestAction { Value = 42 };
		return await _shortCircuitDispatcher.DispatchAsync(action, _shortCircuitContext, CancellationToken.None);
	}

	private static IServiceProvider CreateServiceProvider(int middlewareCount, bool shortCircuit)
	{
		var services = new ServiceCollection();

		// Concrete DI registration alone does not compose the dispatch pipeline.
		_ = services.AddBenchmarkDispatch(builder =>
		{
			if (shortCircuit)
			{
				_ = services.AddMiddleware<ShortCircuitTestMiddleware>();
				_ = builder.UseMiddleware<ShortCircuitTestMiddleware>();
			}
			else
			{
				if (middlewareCount >= 1)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage1>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage1>>();
				}
				if (middlewareCount >= 2)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage2>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage2>>();
				}
				if (middlewareCount >= 3)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage3>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage3>>();
				}
				if (middlewareCount >= 4)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage4>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage4>>();
				}
				if (middlewareCount >= 5)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage5>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage5>>();
				}
				if (middlewareCount >= 6)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage6>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage6>>();
				}
				if (middlewareCount >= 7)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage7>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage7>>();
				}
				if (middlewareCount >= 8)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage8>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage8>>();
				}
				if (middlewareCount >= 9)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage9>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage9>>();
				}
				if (middlewareCount >= 10)
				{
					_ = services.AddMiddleware<PassthroughTestMiddleware<Stage10>>();
					_ = builder.UseMiddleware<PassthroughTestMiddleware<Stage10>>();
				}
			}
		});
		_ = services.AddTransient<IActionHandler<TestAction>, TestActionHandler>();

		return services.BuildServiceProvider();
	}

	private static void ValidateComposition(IServiceProvider provider, Type[] expected)
	{
		// Managed-only fixture guard: fail closed if the actual invoker layout changes.
		const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var dispatcher = provider.GetRequiredService<IDispatcher>();
		var invoker = dispatcher.GetType().GetField("_concreteMiddlewareInvoker", flags)?.GetValue(dispatcher)
			?? throw new InvalidOperationException("Cannot inspect dispatcher-owned middleware invoker.");
		var builder = invoker.GetType().GetField("_chainBuilder", flags)?.GetValue(invoker)
			?? throw new InvalidOperationException("Cannot inspect benchmark middleware invoker.");
		var chain = builder.GetType().GetMethod("GetChain", [typeof(Type)])?.Invoke(builder, [typeof(TestAction)])
			?? throw new InvalidOperationException("Cannot inspect applicable benchmark chain.");
		var middleware = chain.GetType().GetField("_middlewares", flags)?.GetValue(chain) as IDispatchMiddleware[]
			?? throw new InvalidOperationException("Cannot inspect benchmark middleware chain.");
		var identity = invoker.GetType().Assembly.GetType("Excalibur.Dispatch.Delivery.Pipeline.IMiddlewareTypeIdentity", throwOnError: true)!;
		var identityProperty = identity.GetProperty("MiddlewareType")
			?? throw new InvalidOperationException("Cannot inspect scoped middleware identity.");
		var actual = middleware.Select(item => identity.IsInstanceOfType(item)
			? (Type)(identityProperty.GetValue(item) ?? throw new InvalidOperationException("Missing middleware identity."))
			: item.GetType()).ToArray();
		if (!actual.SequenceEqual(expected))
		{
			throw new InvalidOperationException($"Benchmark middleware chain differs from its declared workload: {string.Join(", ", middleware.Select(static item => item.GetType().FullName))}");
		}
	}

	private static void DisposeProvider(ref IServiceProvider? provider, ref List<Exception>? failures)
	{
		var owned = provider;
		provider = null;
		try
		{
			(owned as IDisposable)?.Dispose();
		}
		catch (Exception error)
		{
			(failures ??= []).Add(error);
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

	private sealed class Stage1
	{
	}
	private sealed class Stage2
	{
	}
	private sealed class Stage3
	{
	}
	private sealed class Stage4
	{
	}
	private sealed class Stage5
	{
	}
	private sealed class Stage6
	{
	}
	private sealed class Stage7
	{
	}
	private sealed class Stage8
	{
	}
	private sealed class Stage9
	{
	}
	private sealed class Stage10
	{
	}

	// Passthrough Middleware (does minimal work and continues chain)
	private class PassthroughTestMiddleware<TStage> : IDispatchMiddleware
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
		public object? ValidationResult { get; init; }
		public object? AuthorizationResult { get; init; }
		public IMessageProblemDetails? ProblemDetails { get; init; }
	}
}
