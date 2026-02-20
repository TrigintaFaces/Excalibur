// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for Sprint 451 optimizations:
/// - PERF-4: ContinueWith elimination (async/await pattern)
/// - PERF-6: Reflection caching (ResultFactoryCache)
/// </summary>
/// <remarks>
/// These benchmarks measure the performance improvements from:
/// 1. Replacing ContinueWith with async/await in handler invocation
/// 2. Caching factory delegates for typed MessageResult creation
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[GroupBenchmarksBy(BenchmarkDotNet.Configs.BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class Sprint451OptimizationBenchmarks
{
	#region PERF-4: ContinueWith vs Async/Await

	private MethodInfo _handlerMethod = null!;
	private VoidHandler _voidHandler = null!;
	private ResultHandler _resultHandler = null!;
	private BenchmarkMessage _message = null!;
	private CancellationToken _ct;

	[GlobalSetup]
	public void Setup()
	{
		_voidHandler = new VoidHandler();
		_resultHandler = new ResultHandler { ResultToReturn = "benchmark-result" };
		_message = new BenchmarkMessage { Id = Guid.NewGuid(), Data = "BenchmarkData" };
		_ct = CancellationToken.None;

		_handlerMethod = typeof(ResultHandler).GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public)!;
	}

	/// <summary>
	/// Baseline: Direct handler invocation without reflection.
	/// </summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("HandlerInvocation")]
	public async Task<string> DirectHandler_AsyncAwait()
	{
		return await _resultHandler.HandleAsync(_message, _ct).ConfigureAwait(false);
	}

	/// <summary>
	/// PERF-4 Pattern: Async/await with reflection-based invocation.
	/// This is what the optimized ManualHandlerInvokerRegistry uses.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HandlerInvocation")]
	public async Task<object?> AsyncAwait_ReflectionInvoke()
	{
		var result = _handlerMethod.Invoke(_resultHandler, [_message, _ct]);

		if (result is Task<string> typedTask)
		{
			return await typedTask.ConfigureAwait(false);
		}

		return null;
	}

	/// <summary>
	/// Legacy Pattern: ContinueWith with reflection (closure allocation).
	/// This is what the old implementation used.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HandlerInvocation")]
	public Task<object?> ContinueWith_ReflectionInvoke()
	{
		var result = _handlerMethod.Invoke(_resultHandler, [_message, _ct]);

		if (result is Task<string> typedTask)
		{
			// Closure allocation here - the lambda captures typedTask
			return typedTask.ContinueWith(t => (object?)t.Result, TaskContinuationOptions.ExecuteSynchronously);
		}

		return Task.FromResult<object?>(null);
	}

	/// <summary>
	/// PERF-4 Pattern: Cached invoker delegate with async/await.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HandlerInvocation")]
	public async Task<object?> CachedInvoker_AsyncAwait()
	{
		var invoker = GetCachedInvokerAsyncAwait(typeof(ResultHandler));
		return await invoker(_resultHandler, _message, _ct).ConfigureAwait(false);
	}

	/// <summary>
	/// Legacy Pattern: Cached invoker delegate with ContinueWith.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("HandlerInvocation")]
	public Task<object?> CachedInvoker_ContinueWith()
	{
		var invoker = GetCachedInvokerContinueWith(typeof(ResultHandler));
		return invoker(_resultHandler, _message, _ct);
	}

	#endregion

	#region PERF-6: Reflection Caching for Result Factory

	private static readonly ConcurrentDictionary<Type, Func<object?, object>> CachedFactories = new();
	private static readonly MethodInfo GenericSuccessMethod;

	static Sprint451OptimizationBenchmarks()
	{
		GenericSuccessMethod = typeof(MessageResult)
			.GetMethods(BindingFlags.Public | BindingFlags.Static)
			.First(m => m is { Name: nameof(MessageResult.Success), IsGenericMethodDefinition: true } &&
						m.GetGenericArguments().Length == 1 &&
						m.GetParameters().Length == 1);
	}

	/// <summary>
	/// PERF-6 Baseline: Direct call to typed MessageResult.Success.
	/// </summary>
	[Benchmark(Baseline = true)]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult DirectSuccess_String()
	{
		return MessageResult.Success("result-value");
	}

	/// <summary>
	/// PERF-6 Pattern: Cached factory delegate for typed result creation.
	/// This is what ResultFactoryCache uses.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult CachedFactory_String()
	{
		var factory = CachedFactories.GetOrAdd(typeof(string), CreateFactory);
		return (IMessageResult)factory("result-value");
	}

	/// <summary>
	/// Legacy Pattern: MakeGenericMethod per-invocation (uncached).
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult UncachedReflection_String()
	{
		var typedMethod = GenericSuccessMethod.MakeGenericMethod(typeof(string));
		return (IMessageResult)typedMethod.Invoke(null, ["result-value"])!;
	}

	/// <summary>
	/// PERF-6 Pattern: Cached factory for complex type.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult CachedFactory_ComplexType()
	{
		var factory = CachedFactories.GetOrAdd(typeof(BenchmarkPayload), CreateFactory);
		return (IMessageResult)factory(new BenchmarkPayload { Id = 123, Name = "Test" });
	}

	/// <summary>
	/// Legacy Pattern: Uncached reflection for complex type.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult UncachedReflection_ComplexType()
	{
		var typedMethod = GenericSuccessMethod.MakeGenericMethod(typeof(BenchmarkPayload));
		return (IMessageResult)typedMethod.Invoke(null, [new BenchmarkPayload { Id = 123, Name = "Test" }])!;
	}

	/// <summary>
	/// Measures cache lookup performance under concurrent access.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("ResultFactory")]
	public IMessageResult CachedFactory_ConcurrentLookup()
	{
		// Simulate concurrent cache access pattern
		var types = new[] { typeof(string), typeof(int), typeof(BenchmarkPayload), typeof(Guid) };
		var type = types[Environment.TickCount % types.Length];
		var factory = CachedFactories.GetOrAdd(type, CreateFactory);

		if (type == typeof(string))
		{
			return (IMessageResult)factory("value");
		}

		if (type == typeof(int))
		{
			return (IMessageResult)factory(42);
		}

		if (type == typeof(Guid))
		{
			return (IMessageResult)factory(Guid.NewGuid());
		}

		return (IMessageResult)factory(new BenchmarkPayload { Id = 1, Name = "X" });
	}

	#endregion

	#region Combined Optimization Impact

	/// <summary>
	/// Simulates full dispatch path with all Sprint 451 optimizations.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPath")]
	public async Task<IMessageResult> OptimizedDispatchPath()
	{
		// PERF-4: Async/await cached invoker
		var invoker = GetCachedInvokerAsyncAwait(typeof(ResultHandler));
		var result = await invoker(_resultHandler, _message, _ct).ConfigureAwait(false);

		// PERF-6: Cached result factory
		var factory = CachedFactories.GetOrAdd(typeof(string), CreateFactory);
		return (IMessageResult)factory(result);
	}

	/// <summary>
	/// Simulates full dispatch path with legacy patterns.
	/// </summary>
	[Benchmark]
	[BenchmarkCategory("FullPath")]
	public async Task<IMessageResult> LegacyDispatchPath()
	{
		// Legacy: ContinueWith invoker
		var invoker = GetCachedInvokerContinueWith(typeof(ResultHandler));
		var result = await invoker(_resultHandler, _message, _ct).ConfigureAwait(false);

		// Legacy: Uncached reflection
		var typedMethod = GenericSuccessMethod.MakeGenericMethod(typeof(string));
		return (IMessageResult)typedMethod.Invoke(null, [result])!;
	}

	#endregion

	#region Helper Methods

	private static readonly ConcurrentDictionary<Type, Func<object, IDispatchMessage, CancellationToken, Task<object?>>> AsyncAwaitInvokers = new();
	private static readonly ConcurrentDictionary<Type, Func<object, IDispatchMessage, CancellationToken, Task<object?>>> ContinueWithInvokers = new();

	private static Func<object, IDispatchMessage, CancellationToken, Task<object?>> GetCachedInvokerAsyncAwait(Type handlerType)
	{
		return AsyncAwaitInvokers.GetOrAdd(handlerType, static type =>
		{
			var method = type.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public);
			var returnsVoidTask = method.ReturnType == typeof(Task);

			// PERF-4: Async/await pattern - no closure per-call
			return async (handler, message, ct) =>
			{
				var result = method.Invoke(handler, [message, ct]);

				if (result is Task task)
				{
					await task.ConfigureAwait(false);

					if (returnsVoidTask)
					{
						return null;
					}

					var resultProperty = task.GetType().GetProperty("Result");
					return resultProperty?.GetValue(task);
				}

				return null;
			};
		});
	}

	private static Func<object, IDispatchMessage, CancellationToken, Task<object?>> GetCachedInvokerContinueWith(Type handlerType)
	{
		return ContinueWithInvokers.GetOrAdd(handlerType, static type =>
		{
			var method = type.GetMethod("HandleAsync", BindingFlags.Instance | BindingFlags.Public);
			var returnsVoidTask = method.ReturnType == typeof(Task);

			// Legacy: ContinueWith pattern - allocates closure per-call
			return (handler, message, ct) =>
			{
				var result = method.Invoke(handler, [message, ct]);

				if (result is Task task)
				{
					if (returnsVoidTask)
					{
						return task.ContinueWith(_ => (object?)null, TaskContinuationOptions.ExecuteSynchronously);
					}

					// Closure allocation: captures task
					return task.ContinueWith(t =>
					{
						var resultProperty = t.GetType().GetProperty("Result");
						return resultProperty?.GetValue(t);
					}, TaskContinuationOptions.ExecuteSynchronously);
				}

				return Task.FromResult<object?>(null);
			};
		});
	}

	private static Func<object?, object> CreateFactory(Type resultType)
	{
		var typedMethod = GenericSuccessMethod.MakeGenericMethod(resultType);
		return value => typedMethod.Invoke(null, [value])!;
	}

	#endregion

	#region Test Fixtures

	private sealed record BenchmarkMessage : IDispatchMessage
	{
		public Guid Id { get; init; }
		public string Data { get; init; } = string.Empty;
	}

	private sealed class BenchmarkPayload
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}

	private sealed class VoidHandler
	{
		public Task HandleAsync(BenchmarkMessage message, CancellationToken ct) => Task.CompletedTask;
	}

	private sealed class ResultHandler
	{
		public string ResultToReturn { get; set; } = string.Empty;

		public Task<string> HandleAsync(BenchmarkMessage message, CancellationToken ct) =>
			Task.FromResult(ResultToReturn);
	}

	#endregion
}
