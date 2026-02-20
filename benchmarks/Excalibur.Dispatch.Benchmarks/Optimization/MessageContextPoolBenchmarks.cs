// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.ZeroAlloc;

using Microsoft.Extensions.DependencyInjection;

using DispatchMessageContext = Excalibur.Dispatch.Messaging.MessageContext;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks for MessageContext Object Pooling.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks validate that the <see cref="MessageContextPool"/> eliminates
/// per-dispatch MessageContext allocations (~400-500B).
/// </para>
/// <para>
/// Target: Reduce per-dispatch allocations by 400-500B through context pooling.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class MessageContextPoolBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private MessageContextPool _pool = null!;
	private TestMessage _message = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_serviceProvider = services.BuildServiceProvider();
		_pool = new MessageContextPool(_serviceProvider);
		_message = new TestMessage { Value = 42 };
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		if (_serviceProvider is IDisposable disposable)
		{
			disposable.Dispose();
		}
	}

	/// <summary>
	/// Baseline: Allocate new MessageContext each time (no pooling).
	/// </summary>
	[Benchmark(Baseline = true)]
	public IMessageContext CreateContext_NoPooling()
	{
		return new DispatchMessageContext(_message, _serviceProvider);
	}

	/// <summary>
	/// Pooled: Rent from pool, use, return to pool.
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext_Pooled_RentAndReturn()
	{
		var context = _pool.Rent(_message);
		// Simulate minimal usage
		context.SetItem("key", "value");
		_pool.ReturnToPool(context);
		return context;
	}

	/// <summary>
	/// Pooled: Rent only (without return - measures allocation on first use).
	/// </summary>
	[Benchmark]
	public IMessageContext CreateContext_Pooled_RentOnly()
	{
		return _pool.Rent(_message);
	}

	/// <summary>
	/// Pooled: Return only (measures return overhead).
	/// </summary>
	[Benchmark]
	public void ReturnContext_Pooled()
	{
		var context = _pool.Rent(_message);
		_pool.ReturnToPool(context);
	}

	/// <summary>
	/// Batch of 100 context creations without pooling.
	/// </summary>
	[Benchmark]
	public int CreateContext_Batch100_NoPooling()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var context = new DispatchMessageContext(_message, _serviceProvider);
			if (context != null)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Batch of 100 context rent/return cycles with pooling.
	/// </summary>
	[Benchmark]
	public int CreateContext_Batch100_Pooled()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var context = _pool.Rent(_message);
			context.SetItem("iteration", i);
			_pool.ReturnToPool(context);
			count++;
		}

		return count;
	}

	/// <summary>
	/// Simulates realistic dispatch pattern: create, use in pipeline, return.
	/// </summary>
	[Benchmark]
	public async Task<int> RealisticDispatch_Pooled()
	{
		var context = _pool.Rent(_message);
		try
		{
			// Simulate middleware pipeline access patterns
			context.SetItem("CorrelationId", Guid.NewGuid().ToString());
			context.SetItem("Dispatch:StartTime", DateTimeOffset.UtcNow);

			// Simulate async work
			await Task.Yield();

			context.SetItem("Dispatch:Result", "Success");
			return context.ContainsItem("Dispatch:Result") ? 1 : 0;
		}
		finally
		{
			_pool.ReturnToPool(context);
		}
	}

	/// <summary>
	/// Simulates realistic dispatch pattern without pooling.
	/// </summary>
	[Benchmark]
	public async Task<int> RealisticDispatch_NoPooling()
	{
		var context = new DispatchMessageContext(_message, _serviceProvider);

		// Simulate middleware pipeline access patterns
		context.SetItem("CorrelationId", Guid.NewGuid().ToString());
		context.SetItem("Dispatch:StartTime", DateTimeOffset.UtcNow);

		// Simulate async work
		await Task.Yield();

		context.SetItem("Dispatch:Result", "Success");
		return context.ContainsItem("Dispatch:Result") ? 1 : 0;
	}

	/// <summary>
	/// Measures concurrent pooling performance.
	/// </summary>
	[Benchmark]
	public async Task<int> ConcurrentPoolAccess()
	{
		const int TaskCount = 10;
		var tasks = new Task<int>[TaskCount];

		for (var i = 0; i < TaskCount; i++)
		{
			tasks[i] = Task.Run(() =>
			{
				var context = _pool.Rent(_message);
				try
				{
					context.SetItem("ThreadId", Environment.CurrentManagedThreadId);
					return 1;
				}
				finally
				{
					_pool.ReturnToPool(context);
				}
			});
		}

		var results = await Task.WhenAll(tasks);
		return results.Sum();
	}

	// Test message
	private sealed record TestMessage : IDispatchAction
	{
		public int Value { get; init; }
	}
}
