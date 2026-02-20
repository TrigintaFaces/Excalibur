// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Pipeline;

using Microsoft.Extensions.DependencyInjection;

using DispatchMessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks for Middleware Chain Builder closure elimination.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks validate that the <see cref="MiddlewareChainBuilder"/> and
/// <see cref="ChainExecutor"/> eliminate per-dispatch closure allocations.
/// </para>
/// <para>
/// Target: Eliminate 200-300B per-dispatch closure allocations in middleware invocation.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MiddlewareChainBuilderBenchmarks
{
	private MiddlewareChainBuilder _builder = null!;
	private MiddlewareChainBuilder _builderFrozen = null!;
	private ChainExecutor _executor = null!;
	private ChainExecutor _executorEmpty = null!;
	private TestMessage _message = null!;
	private IMessageContext _context = null!;
	private IServiceProvider _serviceProvider = null!;

	/// <summary>
	/// Gets or sets the number of middleware in the chain.
	/// </summary>
	[Params(0, 1, 3, 5, 10)]
	public int MiddlewareCount { get; set; }

	[GlobalSetup]
	public void Setup()
	{
		// Create service provider for MessageContext
		var services = new ServiceCollection();
		_serviceProvider = services.BuildServiceProvider();

		// Create middleware array based on param
		var middlewares = CreateMiddlewareArray(MiddlewareCount);

		_builder = new MiddlewareChainBuilder(middlewares);
		_builderFrozen = new MiddlewareChainBuilder(middlewares);
		_builderFrozen.Freeze([typeof(TestMessage)]);

		_executor = _builder.GetChain(typeof(TestMessage));
		_executorEmpty = ChainExecutor.Empty;

		_message = new TestMessage { Value = 42 };
		_context = new DispatchMessageContext(_message, _serviceProvider);
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	private static IDispatchMiddleware[] CreateMiddlewareArray(int count)
	{
		var middlewares = new IDispatchMiddleware[count];
		for (var i = 0; i < count; i++)
		{
			middlewares[i] = new PassthroughMiddleware();
		}

		return middlewares;
	}

	private static readonly DispatchRequestDelegate _finalHandler = static (_, _, _) =>
		new ValueTask<IMessageResult>(new TestMessageResult { Succeeded = true });

	/// <summary>
	/// Baseline: Direct handler invocation without middleware chain.
	/// </summary>
	[Benchmark(Baseline = true)]
	public ValueTask<IMessageResult> DirectHandlerInvocation()
	{
		return new ValueTask<IMessageResult>(new TestMessageResult { Succeeded = true });
	}

	/// <summary>
	/// Chain execution using pre-compiled chain from unfrozen builder.
	/// </summary>
	[Benchmark]
	public ValueTask<IMessageResult> ChainExecution_Unfrozen()
	{
		return _executor.InvokeAsync(_message, _context, _finalHandler, CancellationToken.None);
	}

	/// <summary>
	/// Chain execution using pre-compiled chain from frozen builder.
	/// </summary>
	[Benchmark]
	public ValueTask<IMessageResult> ChainExecution_Frozen()
	{
		var executor = _builderFrozen.GetChain(typeof(TestMessage));
		return executor.InvokeAsync(_message, _context, _finalHandler, CancellationToken.None);
	}

	/// <summary>
	/// Chain execution including chain lookup (typical dispatch path).
	/// </summary>
	[Benchmark]
	public ValueTask<IMessageResult> ChainLookupAndExecution()
	{
		var executor = _builder.GetChain(typeof(TestMessage));
		return executor.InvokeAsync(_message, _context, _finalHandler, CancellationToken.None);
	}

	/// <summary>
	/// Empty chain (fast path for no middleware).
	/// </summary>
	[Benchmark]
	public ValueTask<IMessageResult> EmptyChain()
	{
		return _executorEmpty.InvokeAsync(_message, _context, _finalHandler, CancellationToken.None);
	}

	/// <summary>
	/// Batch of 100 chain executions to measure sustained performance.
	/// </summary>
	[Benchmark]
	public async Task<int> ChainExecution_Batch100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var result = await _executor.InvokeAsync(_message, _context, _finalHandler, CancellationToken.None);
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	// Test types
	private sealed record TestMessage : IDispatchAction
	{
		public int Value { get; init; }
	}

	private sealed class TestMessageResult : IMessageResult
	{
		public bool Succeeded { get; init; }
		public string? ErrorMessage { get; init; }
		public bool CacheHit { get; init; }
		public object? ValidationResult { get; init; }
		public object? AuthorizationResult { get; init; }
		public IMessageProblemDetails? ProblemDetails { get; init; }
	}

	private sealed class PassthroughMiddleware : IDispatchMiddleware
	{
		public DispatchMiddlewareStage? Stage => DispatchMiddlewareStage.PreProcessing;
		public MessageKinds ApplicableMessageKinds => MessageKinds.All;

		public ValueTask<IMessageResult> InvokeAsync(
			IDispatchMessage message,
			IMessageContext context,
			DispatchRequestDelegate nextDelegate,
			CancellationToken cancellationToken)
		{
			// Minimal work, just pass through
			return nextDelegate(message, context, cancellationToken);
		}
	}
}
