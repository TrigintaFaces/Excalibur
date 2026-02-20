// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

using DispatchMessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks for ValueTask Migration for Hot Path Methods.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks validate that ValueTask migration enables zero-allocation
/// dispatch on synchronous completion paths.
/// </para>
/// <para>
/// Target: Zero allocation on sync completion path (cache hit, fast handler).
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class ValueTaskMigrationBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IDispatcher _dispatcher = null!;
	private IMessageContextFactory _contextFactory = null!;
	private TestCommand _command = null!;
	private IMessageContext _context = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Register handlers BEFORE AddDispatch
		_ = services.AddTransient<FastSyncHandler>();
		_ = services.AddTransient<IActionHandler<TestCommand>, FastSyncHandler>();

		_ = services.AddDispatch();

		// Add minimal middleware to test ValueTask pass-through
		_ = services.AddMiddleware<PassthroughMiddleware>();

		_serviceProvider = services.BuildServiceProvider();
		_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
		_contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();

		_command = new TestCommand { OrderId = Guid.NewGuid() };
		_context = _contextFactory.CreateContext();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Baseline: Single dispatch measuring allocation behavior.
	/// </summary>
	[Benchmark(Baseline = true)]
	public Task<IMessageResult> SingleDispatch()
	{
		var context = _contextFactory.CreateContext();
		return _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Awaited dispatch - measures async path behavior.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> SingleDispatch_Awaited()
	{
		var context = _contextFactory.CreateContext();
		return await _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
	}

	/// <summary>
	/// Reused context - measures allocation when context is pre-created.
	/// </summary>
	[Benchmark]
	public Task<IMessageResult> SingleDispatch_ReusedContext()
	{
		return _dispatcher.DispatchAsync(_command, _context, CancellationToken.None);
	}

	/// <summary>
	/// Batch of 100 dispatches - measures sustained ValueTask performance.
	/// </summary>
	[Benchmark]
	public async Task<int> Batch100Dispatches()
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
	/// Direct middleware chain invoke - isolates ValueTask path without dispatch overhead.
	/// </summary>
	[Benchmark]
	public ValueTask<IMessageResult> DirectMiddlewareInvoke()
	{
		var middleware = new PassthroughMiddleware();
		return middleware.InvokeAsync(
			_command,
			_context,
			static (_, _, _) => new ValueTask<IMessageResult>(new SuccessResult()),
			CancellationToken.None);
	}

	/// <summary>
	/// Synchronous completion path - validates zero-allocation behavior.
	/// </summary>
	[Benchmark]
	public IMessageResult SyncCompletionPath()
	{
		var result = new SuccessResult();
		var valueTask = new ValueTask<IMessageResult>(result);

		// This should not allocate since it's already completed
		return valueTask.IsCompletedSuccessfully ? valueTask.Result : valueTask.AsTask().Result;
	}

	/// <summary>
	/// Concurrent dispatches (10 parallel) - validates thread-safety.
	/// </summary>
	[Benchmark]
	public async Task<int> ConcurrentDispatches()
	{
		var tasks = new Task<IMessageResult>[10];
		for (var i = 0; i < 10; i++)
		{
			var context = _contextFactory.CreateContext();
			tasks[i] = _dispatcher.DispatchAsync(_command, context, CancellationToken.None);
		}

		var results = await Task.WhenAll(tasks);
		var count = 0;
		foreach (var result in results)
		{
			if (result.Succeeded)
			{
				count++;
			}
		}

		return count;
	}

	// Test types
	private sealed record TestCommand : IDispatchAction
	{
		public Guid OrderId { get; init; }
	}

	private sealed class FastSyncHandler : IActionHandler<TestCommand>
	{
		public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
		{
			// Fast synchronous completion
			_ = command.OrderId;
			return Task.CompletedTask;
		}
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
			// Minimal overhead - just pass through
			return nextDelegate(message, context, cancellationToken);
		}
	}

	private sealed class SuccessResult : IMessageResult
	{
		public bool Succeeded => true;
		public string? ErrorMessage => null;
		public bool CacheHit => false;
		public object? ValidationResult => null;
		public object? AuthorizationResult => null;
		public IMessageProblemDetails? ProblemDetails => null;
	}
}
