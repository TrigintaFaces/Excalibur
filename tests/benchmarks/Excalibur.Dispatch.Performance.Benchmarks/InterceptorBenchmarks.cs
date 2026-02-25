// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Frozen;
using System.Collections.Concurrent;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for C# 12 interceptor performance vs runtime resolution.
/// Validates the 50% latency reduction target for PERF-9.
/// </summary>
/// <remarks>
/// Sprint 454 - S454.6: Benchmark verification for interceptor performance.
/// Compares three resolution tiers:
/// 1. Intercepted (compile-time static dispatch)
/// 2. FrozenDictionary lookup (runtime, no locking)
/// 3. ConcurrentDictionary lookup (runtime, with locking)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class InterceptorBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;
	private HandlerInvoker _handlerInvoker = null!;

	// Test messages
	private InterceptorTestCommand _command = null!;
	private InterceptorTestQuery _query = null!;
	private IMessageContext _reusableContext = null!;

	// Handler instances for direct invocation comparison
	private InterceptorTestCommandHandler _commandHandler = null!;
	private InterceptorTestQueryHandler _queryHandler = null!;

	// Dictionary comparison - simulating runtime resolution paths
	private FrozenDictionary<Type, Func<object, IMessageContext, CancellationToken, Task<object?>>> _frozenHandlerMap = null!;
	private ConcurrentDictionary<Type, Func<object, IMessageContext, CancellationToken, Task<object?>>> _concurrentHandlerMap = null!;
	private Dictionary<Type, Func<object, IMessageContext, CancellationToken, Task<object?>>> _dictionaryHandlerMap = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers
		_ = services.AddTransient<InterceptorTestCommandHandler>();
		_ = services.AddTransient<IActionHandler<InterceptorTestCommand>, InterceptorTestCommandHandler>();
		_ = services.AddTransient<InterceptorTestQueryHandler>();
		_ = services.AddTransient<IActionHandler<InterceptorTestQuery, InterceptorTestQueryResult>, InterceptorTestQueryHandler>();

		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_handlerInvoker = new HandlerInvoker();

		// Pre-create test instances
		_command = new InterceptorTestCommand { Id = Guid.NewGuid(), Value = 42 };
		_query = new InterceptorTestQuery { SearchTerm = "benchmark" };
		_reusableContext = _contextFactory.CreateContext();

		// Pre-instantiate handlers for direct comparison
		_commandHandler = new InterceptorTestCommandHandler();
		_queryHandler = new InterceptorTestQueryHandler();

		// Build handler maps for resolution comparison
		var handlerMap = new Dictionary<Type, Func<object, IMessageContext, CancellationToken, Task<object?>>>
		{
			[typeof(InterceptorTestCommand)] = async (msg, ctx, ct) =>
			{
				await _commandHandler.HandleAsync((InterceptorTestCommand)msg, ct);
				return null;
			},
			[typeof(InterceptorTestQuery)] = async (msg, ctx, ct) =>
			{
				return await _queryHandler.HandleAsync((InterceptorTestQuery)msg, ct);
			}
		};

		_dictionaryHandlerMap = handlerMap;
		_concurrentHandlerMap = new ConcurrentDictionary<Type, Func<object, IMessageContext, CancellationToken, Task<object?>>>(handlerMap);
		_frozenHandlerMap = handlerMap.ToFrozenDictionary();

		// Warm up HandlerInvoker cache
		_ = _handlerInvoker.InvokeAsync(_commandHandler, _command, CancellationToken.None).GetAwaiter().GetResult();
		HandlerInvoker.FreezeCache();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	#region Direct Handler Invocation (Simulated Interceptor Path)

	/// <summary>
	/// Baseline: Direct handler invocation (what interceptors achieve).
	/// This represents the optimal path with zero dictionary lookups.
	/// </summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("Interceptor")]
	public Task DirectHandlerInvocation_Command()
	{
		return _commandHandler.HandleAsync(_command, CancellationToken.None);
	}

	/// <summary>
	/// Direct query handler invocation with result.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Interceptor")]
	public Task<InterceptorTestQueryResult> DirectHandlerInvocation_Query()
	{
		return _queryHandler.HandleAsync(_query, CancellationToken.None);
	}

	#endregion

	#region FrozenDictionary Resolution (PERF-13/14 Optimized Path)

	/// <summary>
	/// FrozenDictionary lookup + handler invocation.
	/// This is the fallback path for non-intercepted calls (PERF-13/14).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FrozenDictionary")]
	public Task FrozenDictionary_ResolveAndInvoke_Command()
	{
		if (_frozenHandlerMap.TryGetValue(typeof(InterceptorTestCommand), out var handler))
		{
			return handler(_command, _reusableContext, CancellationToken.None);
		}
		return Task.CompletedTask;
	}

	/// <summary>
	/// FrozenDictionary lookup for query with result.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FrozenDictionary")]
	public async Task<object?> FrozenDictionary_ResolveAndInvoke_Query()
	{
		if (_frozenHandlerMap.TryGetValue(typeof(InterceptorTestQuery), out var handler))
		{
			return await handler(_query, _reusableContext, CancellationToken.None);
		}
		return null;
	}

	#endregion

	#region ConcurrentDictionary Resolution (Pre-optimization Path)

	/// <summary>
	/// ConcurrentDictionary lookup + handler invocation.
	/// This represents the pre-PERF-13/14 path.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ConcurrentDictionary")]
	public Task ConcurrentDictionary_ResolveAndInvoke_Command()
	{
		if (_concurrentHandlerMap.TryGetValue(typeof(InterceptorTestCommand), out var handler))
		{
			return handler(_command, _reusableContext, CancellationToken.None);
		}
		return Task.CompletedTask;
	}

	/// <summary>
	/// ConcurrentDictionary lookup for query with result.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ConcurrentDictionary")]
	public async Task<object?> ConcurrentDictionary_ResolveAndInvoke_Query()
	{
		if (_concurrentHandlerMap.TryGetValue(typeof(InterceptorTestQuery), out var handler))
		{
			return await handler(_query, _reusableContext, CancellationToken.None);
		}
		return null;
	}

	#endregion

	#region Full Pipeline Dispatch (Real-world Comparison)

	/// <summary>
	/// Full dispatcher pipeline dispatch (includes middleware).
	/// This measures actual framework overhead.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult> FullPipeline_DispatchCommand()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Full dispatcher pipeline dispatch for query with result.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult<InterceptorTestQueryResult>> FullPipeline_DispatchQuery()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync<InterceptorTestQuery, InterceptorTestQueryResult>(_query, context, CancellationToken.None);
	}

	/// <summary>
	/// Full pipeline with reused context (minimal allocation).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult> FullPipeline_WithReusedContext()
	{
		_reusableContext.Items.Clear();
		return _dispatcher.DispatchAsync(_command, _reusableContext, CancellationToken.None);
	}

	#endregion

	#region HandlerInvoker Cache (Frozen vs Warmup)

	/// <summary>
	/// HandlerInvoker with frozen cache (post-optimization).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HandlerInvoker")]
	public Task<object?> HandlerInvoker_FrozenCache()
	{
		return _handlerInvoker.InvokeAsync(_commandHandler, _command, CancellationToken.None);
	}

	#endregion

	#region Throughput Comparisons

	/// <summary>
	/// Batch of 100 direct handler invocations.
	/// Represents best-case interceptor throughput.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task DirectInvocation_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			await _commandHandler.HandleAsync(_command, CancellationToken.None);
		}
	}

	/// <summary>
	/// Batch of 100 FrozenDictionary resolutions.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task FrozenDictionary_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			if (_frozenHandlerMap.TryGetValue(typeof(InterceptorTestCommand), out var handler))
			{
				_ = await handler(_command, _reusableContext, CancellationToken.None);
			}
		}
	}

	/// <summary>
	/// Batch of 100 full pipeline dispatches.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task FullPipeline_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			var context = _contextFactory.CreateContext();
			_ = await _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}
	}

	#endregion

	#region Memory Allocation Comparison

	/// <summary>
	/// Single allocation measurement - direct invocation.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_DirectInvocation_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			await _commandHandler.HandleAsync(_command, CancellationToken.None);
		}
	}

	/// <summary>
	/// Single allocation measurement - frozen dictionary.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_FrozenDictionary_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			if (_frozenHandlerMap.TryGetValue(typeof(InterceptorTestCommand), out var handler))
			{
				_ = await handler(_command, _reusableContext, CancellationToken.None);
			}
		}
	}

	/// <summary>
	/// Single allocation measurement - full pipeline.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_FullPipeline_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			_ = await _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}
	}

	#endregion

	#region Test Types

	public sealed record InterceptorTestCommand : IDispatchAction
	{
		public Guid Id { get; init; }
		public int Value { get; init; }
	}

	public sealed record InterceptorTestQuery : IDispatchAction<InterceptorTestQueryResult>
	{
		public string SearchTerm { get; init; } = string.Empty;
	}

	public sealed record InterceptorTestQueryResult
	{
		public int Count { get; init; }
		public string[] Items { get; init; } = [];
	}

	public sealed class InterceptorTestCommandHandler : IActionHandler<InterceptorTestCommand>
	{
		public Task HandleAsync(InterceptorTestCommand action, CancellationToken cancellationToken)
		{
			// Minimal work to prevent optimization
			_ = action.Id;
			_ = action.Value;
			return Task.CompletedTask;
		}
	}

	public sealed class InterceptorTestQueryHandler : IActionHandler<InterceptorTestQuery, InterceptorTestQueryResult>
	{
		public Task<InterceptorTestQueryResult> HandleAsync(InterceptorTestQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult(new InterceptorTestQueryResult
			{
				Count = 1,
				Items = [action.SearchTerm]
			});
		}
	}

	#endregion
}
