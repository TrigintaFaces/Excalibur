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

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for static pipeline generation performance (PERF-23).
/// Validates middleware decomposition and static pipeline invocation benefits.
/// </summary>
/// <remarks>
/// Sprint 457 - S457.5: Benchmark verification for static pipeline performance.
/// Compares:
/// 1. Static pipeline (inlined middleware) - compile-time generated
/// 2. Dynamic pipeline (middleware chain) - runtime resolution
/// 3. Before/After decomposition benefits for middleware inlining
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class StaticPipelineBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;

	// Test messages representing deterministic vs non-deterministic types
	private DeterministicCommand _deterministicCommand = null!;
	private DeterministicQuery _deterministicQuery = null!;
	private NonDeterministicCommand _nonDeterministicCommand = null!;

	// Reusable context for allocation comparison
	private IMessageContext _reusableContext = null!;

	// Handler instances for direct invocation comparison
	private DeterministicCommandHandler _commandHandler = null!;
	private DeterministicQueryHandler _queryHandler = null!;

	// Simulated middleware chains for decomposition comparison
	private Func<Task<IMessageResult>> _noMiddlewareChain = null!;
	private Func<Task<IMessageResult>> _beforeOnlyChain = null!;
	private Func<Task<IMessageResult>> _afterOnlyChain = null!;
	private Func<Task<IMessageResult>> _beforeAfterChain = null!;
	private Func<Task<IMessageResult>> _tryCatchChain = null!;

	// FrozenDictionary for decomposition info lookup
	private FrozenDictionary<Type, DecompositionInfo> _decompositionCache = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers
		_ = services.AddTransient<DeterministicCommandHandler>();
		_ = services.AddTransient<IActionHandler<DeterministicCommand>, DeterministicCommandHandler>();
		_ = services.AddTransient<DeterministicQueryHandler>();
		_ = services.AddTransient<IActionHandler<DeterministicQuery, DeterministicQueryResult>, DeterministicQueryHandler>();
		_ = services.AddTransient<IActionHandler<NonDeterministicCommand>, NonDeterministicCommandHandler>();

		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		// Pre-create test instances
		_deterministicCommand = new DeterministicCommand { Id = Guid.NewGuid(), Value = 42 };
		_deterministicQuery = new DeterministicQuery { SearchTerm = "benchmark" };
		_nonDeterministicCommand = new NonDeterministicCommand { TenantId = "tenant-001" };
		_reusableContext = _contextFactory.CreateContext();

		// Pre-instantiate handlers for direct comparison
		_commandHandler = new DeterministicCommandHandler();
		_queryHandler = new DeterministicQueryHandler();

		// Build simulated middleware chains for decomposition comparison
		_noMiddlewareChain = BuildNoMiddlewareChain();
		_beforeOnlyChain = BuildBeforeOnlyChain();
		_afterOnlyChain = BuildAfterOnlyChain();
		_beforeAfterChain = BuildBeforeAfterChain();
		_tryCatchChain = BuildTryCatchChain();

		// Build decomposition cache
		var decompositions = new Dictionary<Type, DecompositionInfo>
		{
			[typeof(DeterministicCommand)] = new DecompositionInfo(true, true, true, false, false),
			[typeof(DeterministicQuery)] = new DecompositionInfo(true, true, false, false, false),
			[typeof(NonDeterministicCommand)] = new DecompositionInfo(false, false, false, true, false)
		};
		_decompositionCache = decompositions.ToFrozenDictionary();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	#region Direct Handler Invocation (Simulated Static Pipeline)

	/// <summary>
	/// Baseline: Direct handler invocation (what static pipelines achieve).
	/// This represents the optimal path with middleware inlined.
	/// </summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("StaticPipeline")]
	public Task DirectHandlerInvocation_Command()
	{
		return _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
	}

	/// <summary>
	/// Direct query handler invocation with typed result.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("StaticPipeline")]
	public Task<DeterministicQueryResult> DirectHandlerInvocation_Query()
	{
		return _queryHandler.HandleAsync(_deterministicQuery, CancellationToken.None);
	}

	#endregion

	#region Middleware Decomposition Comparison

	/// <summary>
	/// No middleware - pure handler invocation.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Decomposition")]
	public Task<IMessageResult> NoMiddleware_Pipeline()
	{
		return _noMiddlewareChain();
	}

	/// <summary>
	/// Before-only middleware (e.g., validation, logging).
	/// Static pipeline can inline this as sequential code.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Decomposition")]
	public Task<IMessageResult> BeforeOnly_Middleware()
	{
		return _beforeOnlyChain();
	}

	/// <summary>
	/// After-only middleware (e.g., metrics, cleanup).
	/// Static pipeline can inline this as sequential code.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Decomposition")]
	public Task<IMessageResult> AfterOnly_Middleware()
	{
		return _afterOnlyChain();
	}

	/// <summary>
	/// Before + After middleware (typical decomposable pattern).
	/// Static pipeline inlines both phases around handler.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Decomposition")]
	public Task<IMessageResult> BeforeAfter_Middleware()
	{
		return _beforeAfterChain();
	}

	/// <summary>
	/// Try-catch middleware (requires state capture).
	/// Static pipeline must capture state variables across phases.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Decomposition")]
	public Task<IMessageResult> TryCatch_Middleware()
	{
		return _tryCatchChain();
	}

	#endregion

	#region Decomposition Cache Lookup

	/// <summary>
	/// FrozenDictionary lookup for decomposition info.
	/// This simulates the generated code path for checking decomposability.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("DecompositionCache")]
	public bool DecompositionCache_LookupDeterministic()
	{
		if (_decompositionCache.TryGetValue(typeof(DeterministicCommand), out var info))
		{
			return info.IsDecomposable;
		}
		return false;
	}

	/// <summary>
	/// FrozenDictionary lookup for non-deterministic type.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("DecompositionCache")]
	public bool DecompositionCache_LookupNonDeterministic()
	{
		if (_decompositionCache.TryGetValue(typeof(NonDeterministicCommand), out var info))
		{
			return info.IsDecomposable;
		}
		return false;
	}

	#endregion

	#region Full Pipeline Comparison

	/// <summary>
	/// Full dispatcher pipeline for deterministic message.
	/// When static pipeline is available, this should be faster.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult> FullPipeline_DeterministicCommand()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_deterministicCommand, context, CancellationToken.None);
	}

	/// <summary>
	/// Full dispatcher pipeline for non-deterministic message.
	/// Falls back to dynamic pipeline resolution.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult> FullPipeline_NonDeterministicCommand()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_nonDeterministicCommand, context, CancellationToken.None);
	}

	/// <summary>
	/// Full pipeline with reused context (minimal allocation).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPipeline")]
	public Task<IMessageResult> FullPipeline_WithReusedContext()
	{
		_reusableContext.Items.Clear();
		return _dispatcher.DispatchAsync(_deterministicCommand, _reusableContext, CancellationToken.None);
	}

	#endregion

	#region Throughput Comparison

	/// <summary>
	/// Batch of 100 direct handler invocations (simulated static pipeline).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task StaticPipeline_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
		}
	}

	/// <summary>
	/// Batch of 100 before/after middleware invocations.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Throughput")]
	public async Task BeforeAfterMiddleware_Batch100()
	{
		for (int i = 0; i < 100; i++)
		{
			_ = await _beforeAfterChain();
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
			_ = await _dispatcher.DispatchAsync(_deterministicCommand, context, CancellationToken.None);
		}
	}

	#endregion

	#region Memory Allocation Comparison

	/// <summary>
	/// Memory allocation - static pipeline path (10 calls).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_StaticPipeline_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
		}
	}

	/// <summary>
	/// Memory allocation - middleware chain (10 calls).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_MiddlewareChain_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			_ = await _beforeAfterChain();
		}
	}

	/// <summary>
	/// Memory allocation - full pipeline (10 calls).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("Memory")]
	public async Task Memory_FullPipeline_10Calls()
	{
		for (int i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			_ = await _dispatcher.DispatchAsync(_deterministicCommand, context, CancellationToken.None);
		}
	}

	#endregion

	#region Middleware Chain Builders

	private Func<Task<IMessageResult>> BuildNoMiddlewareChain()
	{
		return async () =>
		{
			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
			return MessageResult.Success();
		};
	}

	private Func<Task<IMessageResult>> BuildBeforeOnlyChain()
	{
		return async () =>
		{
			// Before phase - validation/logging
			_ = _deterministicCommand.Id;

			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
			return MessageResult.Success();
		};
	}

	private Func<Task<IMessageResult>> BuildAfterOnlyChain()
	{
		return async () =>
		{
			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);

			// After phase - metrics/cleanup
			_ = _deterministicCommand.Value;
			return MessageResult.Success();
		};
	}

	private Func<Task<IMessageResult>> BuildBeforeAfterChain()
	{
		return async () =>
		{
			// Before phase
			_ = _deterministicCommand.Id;

			await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);

			// After phase
			_ = _deterministicCommand.Value;
			return MessageResult.Success();
		};
	}

	private Func<Task<IMessageResult>> BuildTryCatchChain()
	{
		return async () =>
		{
			// State variable captured across phases
			var startTime = DateTime.UtcNow;

			try
			{
				await _commandHandler.HandleAsync(_deterministicCommand, CancellationToken.None);
				return MessageResult.Success();
			}
			catch
			{
				// After phase uses captured state
				var elapsed = DateTime.UtcNow - startTime;
				return MessageResult.Failed($"Failed after {elapsed.TotalMilliseconds}ms");
			}
		};
	}

	#endregion

	#region Test Types

	/// <summary>
	/// Command that can be statically determined at compile time.
	/// </summary>
	public sealed record DeterministicCommand : IDispatchAction
	{
		public Guid Id { get; init; }
		public int Value { get; init; }
	}

	/// <summary>
	/// Query with typed result that can be statically determined.
	/// </summary>
	public sealed record DeterministicQuery : IDispatchAction<DeterministicQueryResult>
	{
		public string SearchTerm { get; init; } = string.Empty;
	}

	/// <summary>
	/// Query result type.
	/// </summary>
	public sealed record DeterministicQueryResult
	{
		public int Count { get; init; }
		public string[] Items { get; init; } = [];
	}

	/// <summary>
	/// Command that requires runtime resolution (tenant-specific routing).
	/// </summary>
	public sealed record NonDeterministicCommand : IDispatchAction
	{
		public string TenantId { get; init; } = string.Empty;
	}

	/// <summary>
	/// Handler for deterministic commands.
	/// </summary>
	public sealed class DeterministicCommandHandler : IActionHandler<DeterministicCommand>
	{
		public Task HandleAsync(DeterministicCommand action, CancellationToken cancellationToken)
		{
			// Minimal work to prevent optimization
			_ = action.Id;
			_ = action.Value;
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Handler for deterministic queries.
	/// </summary>
	public sealed class DeterministicQueryHandler : IActionHandler<DeterministicQuery, DeterministicQueryResult>
	{
		public Task<DeterministicQueryResult> HandleAsync(DeterministicQuery action, CancellationToken cancellationToken)
		{
			return Task.FromResult(new DeterministicQueryResult
			{
				Count = 1,
				Items = [action.SearchTerm]
			});
		}
	}

	/// <summary>
	/// Handler for non-deterministic commands.
	/// </summary>
	public sealed class NonDeterministicCommandHandler : IActionHandler<NonDeterministicCommand>
	{
		public Task HandleAsync(NonDeterministicCommand action, CancellationToken cancellationToken)
		{
			_ = action.TenantId;
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Decomposition info for middleware analysis.
	/// </summary>
	public sealed record DecompositionInfo(
		bool IsDecomposable,
		bool HasBeforePhase,
		bool HasAfterPhase,
		bool RequiresRuntimeResolution,
		bool HasStateVariables);

	#endregion
}
