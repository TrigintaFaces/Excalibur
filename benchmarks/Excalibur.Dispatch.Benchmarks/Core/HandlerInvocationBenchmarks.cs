// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks for handler invocation performance.
/// Measures the overhead of dispatching messages to handlers through the pipeline.
/// </summary>
/// <remarks>
/// Sprint 185 - Performance Benchmarks Enhancement.
/// bd-mwnbc: Handler Invocation Benchmarks (10 scenarios).
///
/// Performance Targets (from testing-and-benchmarking-strategy-spec.md):
/// - Single handler invocation: &lt; 1ÃƒÅ½Ã‚Â¼s (P50), &lt; 2ÃƒÅ½Ã‚Â¼s (P95)
/// - Multiple handlers (10): &lt; 10ÃƒÅ½Ã‚Â¼s (P50), &lt; 20ÃƒÅ½Ã‚Â¼s (P95)
/// - Handler with DI: &lt; 5ÃƒÅ½Ã‚Â¼s (P50), &lt; 10ÃƒÅ½Ã‚Â¼s (P95)
/// - Handler activation: &lt; 5ÃƒÅ½Ã‚Â¼s
/// - Batch handlers (50): &lt; 100ÃƒÅ½Ã‚Â¼s (P50)
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class HandlerInvocationBenchmarks
{
	private IServiceProvider? _serviceProvider;
	private IServiceProvider? _singleEventProvider;
	private IDispatcher? _singleEventDispatcher;
	private IMessageContext? _singleEventContext;
	private IServiceProvider? _scopedServiceProvider;
	private IServiceProvider? _singletonServiceProvider;
	private IDispatcher? _dispatcher;
	private IDispatcher? _scopedDispatcher;
	private IDispatcher? _singletonDispatcher;
	private IMessageContext? _context;
	private IMessageContext? _scopedContext;
	private IMessageContext? _singletonContext;

	// Test Service and Handler with DI
	private interface ITestService
	{
		int Process(int value);
	}

	/// <summary>
	/// Initialize Dispatch pipeline before benchmarks.
	/// </summary>
	[GlobalSetup]
	public void GlobalSetup()
	{
		try
		{
			// Setup 1: Default (transient handlers) - baseline
			var services = new ServiceCollection();
			_ = services.AddBenchmarkDispatch();
			_ = services.AddTransient<IActionHandler<TestAction>, TestActionHandler>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler1>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler2>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler3>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler4>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler5>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler6>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler7>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler8>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler9>();
			_ = services.AddTransient<IEventHandler<TestEvent>, TestEventHandler10>();
			_ = services.AddTransient<IActionHandler<TestQuery, string>, TestQueryHandler>();
			_ = services.AddSingleton<ITestService, TestService>();
			_ = services.AddTransient<IActionHandler<TestActionWithDI>, TestActionWithDIHandler>();
	
			// Distinct handler types preserve the intended 50 executable subscriptions.
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch1>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch2>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch3>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch4>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch5>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch6>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch7>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch8>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch9>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch10>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch11>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch12>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch13>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch14>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch15>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch16>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch17>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch18>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch19>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch20>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch21>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch22>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch23>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch24>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch25>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch26>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch27>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch28>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch29>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch30>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch31>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch32>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch33>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch34>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch35>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch36>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch37>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch38>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch39>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch40>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch41>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch42>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch43>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch44>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch45>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch46>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch47>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch48>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch49>>();
			_ = services.AddTransient<IEventHandler<BatchTestEvent>, BatchEventHandler<Batch50>>();
	
			_serviceProvider = services.BuildServiceProvider();
			_dispatcher = _serviceProvider.GetRequiredService<IDispatcher>();
			// Create context via factory (IMessageContext is per-request, not DI-registered)
			var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
			_context = contextFactory.CreateContext();
	
			var singleEventServices = new ServiceCollection();
			_ = singleEventServices.AddBenchmarkDispatch();
			_ = singleEventServices.AddTransient<IEventHandler<TestEvent>, TestEventHandler1>();
			_singleEventProvider = singleEventServices.BuildServiceProvider();
			_singleEventDispatcher = _singleEventProvider.GetRequiredService<IDispatcher>();
			_singleEventContext = _singleEventProvider.GetRequiredService<IMessageContextFactory>().CreateContext();
	
			// Setup 2: Scoped handlers - for resolution benchmarks
			var scopedServices = new ServiceCollection();
			_ = scopedServices.AddBenchmarkDispatch();
			_ = scopedServices.AddScoped<IActionHandler<TestAction>, TestActionHandler>();
			_ = scopedServices.AddSingleton<ITestService, TestService>();
	
			_scopedServiceProvider = scopedServices.BuildServiceProvider();
			_scopedDispatcher = _scopedServiceProvider.GetRequiredService<IDispatcher>();
			var scopedContextFactory = _scopedServiceProvider.GetRequiredService<IMessageContextFactory>();
			_scopedContext = scopedContextFactory.CreateContext();
	
			// Setup 3: Singleton handlers - for resolution benchmarks
			var singletonServices = new ServiceCollection();
			_ = singletonServices.AddBenchmarkDispatch();
			_ = singletonServices.AddSingleton<IActionHandler<TestAction>, TestActionHandler>();
			_ = singletonServices.AddSingleton<ITestService, TestService>();
	
			_singletonServiceProvider = singletonServices.BuildServiceProvider();
			_singletonDispatcher = _singletonServiceProvider.GetRequiredService<IDispatcher>();
			var singletonContextFactory = _singletonServiceProvider.GetRequiredService<IMessageContextFactory>();
			_singletonContext = singletonContextFactory.CreateContext();
			ValidateEventPlans(_singleEventDispatcher, typeof(TestEvent), [typeof(TestEventHandler1)]);
			ValidateEventPlans(_dispatcher, typeof(TestEvent), [typeof(TestEventHandler1), typeof(TestEventHandler2), typeof(TestEventHandler3), typeof(TestEventHandler4), typeof(TestEventHandler5), typeof(TestEventHandler6), typeof(TestEventHandler7), typeof(TestEventHandler8), typeof(TestEventHandler9), typeof(TestEventHandler10)]);
			ValidateEventPlans(_dispatcher, typeof(BatchTestEvent), [typeof(BatchEventHandler<Batch1>), typeof(BatchEventHandler<Batch2>), typeof(BatchEventHandler<Batch3>), typeof(BatchEventHandler<Batch4>), typeof(BatchEventHandler<Batch5>), typeof(BatchEventHandler<Batch6>), typeof(BatchEventHandler<Batch7>), typeof(BatchEventHandler<Batch8>), typeof(BatchEventHandler<Batch9>), typeof(BatchEventHandler<Batch10>), typeof(BatchEventHandler<Batch11>), typeof(BatchEventHandler<Batch12>), typeof(BatchEventHandler<Batch13>), typeof(BatchEventHandler<Batch14>), typeof(BatchEventHandler<Batch15>), typeof(BatchEventHandler<Batch16>), typeof(BatchEventHandler<Batch17>), typeof(BatchEventHandler<Batch18>), typeof(BatchEventHandler<Batch19>), typeof(BatchEventHandler<Batch20>), typeof(BatchEventHandler<Batch21>), typeof(BatchEventHandler<Batch22>), typeof(BatchEventHandler<Batch23>), typeof(BatchEventHandler<Batch24>), typeof(BatchEventHandler<Batch25>), typeof(BatchEventHandler<Batch26>), typeof(BatchEventHandler<Batch27>), typeof(BatchEventHandler<Batch28>), typeof(BatchEventHandler<Batch29>), typeof(BatchEventHandler<Batch30>), typeof(BatchEventHandler<Batch31>), typeof(BatchEventHandler<Batch32>), typeof(BatchEventHandler<Batch33>), typeof(BatchEventHandler<Batch34>), typeof(BatchEventHandler<Batch35>), typeof(BatchEventHandler<Batch36>), typeof(BatchEventHandler<Batch37>), typeof(BatchEventHandler<Batch38>), typeof(BatchEventHandler<Batch39>), typeof(BatchEventHandler<Batch40>), typeof(BatchEventHandler<Batch41>), typeof(BatchEventHandler<Batch42>), typeof(BatchEventHandler<Batch43>), typeof(BatchEventHandler<Batch44>), typeof(BatchEventHandler<Batch45>), typeof(BatchEventHandler<Batch46>), typeof(BatchEventHandler<Batch47>), typeof(BatchEventHandler<Batch48>), typeof(BatchEventHandler<Batch49>), typeof(BatchEventHandler<Batch50>)]);
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
		DisposeProvider(ref _serviceProvider, ref failures);
		DisposeProvider(ref _singleEventProvider, ref failures);
		DisposeProvider(ref _scopedServiceProvider, ref failures);
		DisposeProvider(ref _singletonServiceProvider, ref failures);
		_dispatcher = _singleEventDispatcher = _scopedDispatcher = _singletonDispatcher = null;
		_context = _singleEventContext = _scopedContext = _singletonContext = null;
		if (failures is not null)
		{
			throw new AggregateException("Benchmark provider cleanup failed.", failures);
		}

	}

	/// <summary>
	/// Benchmark: Single action handler (baseline).
	/// </summary>
	[Benchmark(Baseline = true)]
	public async Task<IMessageResult> InvokeSingleActionHandler()
	{
		var action = new TestAction { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Single event handler.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeSingleEventHandler()
	{
		var @event = new TestEvent { Data = "test" };
		return await _singleEventDispatcher.DispatchAsync(@event, _singleEventContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Single query handler (with return value).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult<string>> InvokeSingleQueryHandler()
	{
		var query = new TestQuery { Id = 123 };
		return await _dispatcher.DispatchAsync<TestQuery, string>(query, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Multiple event handlers (10 handlers for same event).
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeMultipleEventHandlers()
	{
		var @event = new TestEvent { Data = "test" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Handler with dependency injection.
	/// </summary>
	[Benchmark]
	public async Task<IMessageResult> InvokeHandlerWithDI()
	{
		var action = new TestActionWithDI { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	// ========================================================================
	// Sprint 185 - New Benchmark Scenarios (bd-mwnbc)
	// ========================================================================

	/// <summary>
	/// Benchmark: Handler activation from DI container.
	/// Measures pure DI resolution overhead without dispatch pipeline.
	/// Target: &lt; 5ÃƒÅ½Ã‚Â¼s
	/// </summary>
	[Benchmark(Description = "Handler Activation (DI resolution only)")]
	public object HandlerActivation()
	{
		using var scope = _serviceProvider.CreateScope();
		return scope.ServiceProvider.GetRequiredService<IActionHandler<TestAction>>();
	}

	/// <summary>
	/// Benchmark: Transient handler resolution with dispatch.
	/// Baseline for handler lifetime comparison.
	/// </summary>
	[Benchmark(Description = "Transient Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionTransient()
	{
		var action = new TestAction { Value = 42 };
		return await _dispatcher.DispatchAsync(action, _context, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Scoped handler resolution with dispatch.
	/// Handlers resolved once per scope.
	/// </summary>
	[Benchmark(Description = "Scoped Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionScoped()
	{
		var action = new TestAction { Value = 42 };
		return await _scopedDispatcher.DispatchAsync(action, _scopedContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Singleton handler resolution with dispatch.
	/// Handlers resolved once for entire application lifetime.
	/// Expected to be fastest due to no allocation overhead.
	/// </summary>
	[Benchmark(Description = "Singleton Handler Resolution")]
	public async Task<IMessageResult> InvokeHandlerResolutionSingleton()
	{
		var action = new TestAction { Value = 42 };
		return await _singletonDispatcher.DispatchAsync(action, _singletonContext, CancellationToken.None);
	}

	/// <summary>
	/// Benchmark: Batch event handlers (50 handlers for same event).
	/// Tests scaling behavior with large handler counts.
	/// Target: &lt; 100ÃƒÅ½Ã‚Â¼s (P50)
	/// </summary>
	[Benchmark(Description = "Batch Handlers (50 handlers)")]
	public async Task<IMessageResult> InvokeBatchHandlers()
	{
		var @event = new BatchTestEvent { Data = "batch-test" };
		return await _dispatcher.DispatchAsync(@event, _context, CancellationToken.None);
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

	private static void ValidateEventPlans(IDispatcher dispatcher, Type messageType, Type[] expected)
	{
		const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
		var busType = dispatcher.GetType().Assembly.GetType("Excalibur.Dispatch.Messaging.LocalMessageBus", throwOnError: true)!;
		var field = dispatcher.GetType().GetFields(flags).Single(item => item.FieldType == busType);
		var bus = field.GetValue(dispatcher) ?? throw new InvalidOperationException("Missing dispatcher-owned local bus.");
		var handlers = busType.GetMethod("GetEventHandlers", flags)?.Invoke(bus, [messageType])
			?? throw new InvalidOperationException("Cannot inspect event handlers.");
		var plans = busType.GetMethod("GetEventDispatchPlans", flags)?.Invoke(bus, [messageType, handlers]) as Array
			?? throw new InvalidOperationException("Cannot inspect executable event plans.");
		var actual = new List<Type>();
		foreach (var plan in plans)
		{
			var type = plan.GetType();
			actual.Add((Type)(type.GetProperty("HandlerType")?.GetValue(plan)
				?? throw new InvalidOperationException("Missing event handler type.")));
			if (type.GetProperty("Invoke")?.GetValue(plan) is not Delegate)
			{
				throw new InvalidOperationException("Missing event invocation delegate.");
			}
		}

		if (!actual.SequenceEqual(expected))
		{
			throw new InvalidOperationException("Executable handler population differs from the benchmark workload.");
		}
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

	private record TestActionWithDI : IDispatchAction
	{
		public int Value { get; init; }
	}

	/// <summary>
	/// Batch test event for 50-handler benchmark.
	/// </summary>
	private record BatchTestEvent : IDispatchEvent
	{
		public string Data { get; init; } = string.Empty;
	}

	// Test Handlers
	private class TestActionHandler : IActionHandler<TestAction>
	{
		public Task HandleAsync(TestAction action, CancellationToken cancellationToken = default)
		{
			// Minimal work for performance baseline
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
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler3 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler4 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler5 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler6 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler7 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler8 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler9 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}

	private class TestEventHandler10 : IEventHandler<TestEvent>
	{
		public Task HandleAsync(TestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
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

	private class TestService : ITestService
	{
		public int Process(int value) => value * 2;
	}

	private class TestActionWithDIHandler(ITestService service) : IActionHandler<TestActionWithDI>
	{
		public Task HandleAsync(TestActionWithDI action, CancellationToken cancellationToken = default)
		{
			_ = service.Process(action.Value);
			return Task.CompletedTask;
		}
	}

	/// <summary>
	/// Batch event handler for 50-handler scaling benchmark.
	/// </summary>
	private sealed class Batch1
	{
	}

	private sealed class Batch2
	{
	}

	private sealed class Batch3
	{
	}

	private sealed class Batch4
	{
	}

	private sealed class Batch5
	{
	}

	private sealed class Batch6
	{
	}

	private sealed class Batch7
	{
	}

	private sealed class Batch8
	{
	}

	private sealed class Batch9
	{
	}

	private sealed class Batch10
	{
	}

	private sealed class Batch11
	{
	}

	private sealed class Batch12
	{
	}

	private sealed class Batch13
	{
	}

	private sealed class Batch14
	{
	}

	private sealed class Batch15
	{
	}

	private sealed class Batch16
	{
	}

	private sealed class Batch17
	{
	}

	private sealed class Batch18
	{
	}

	private sealed class Batch19
	{
	}

	private sealed class Batch20
	{
	}

	private sealed class Batch21
	{
	}

	private sealed class Batch22
	{
	}

	private sealed class Batch23
	{
	}

	private sealed class Batch24
	{
	}

	private sealed class Batch25
	{
	}

	private sealed class Batch26
	{
	}

	private sealed class Batch27
	{
	}

	private sealed class Batch28
	{
	}

	private sealed class Batch29
	{
	}

	private sealed class Batch30
	{
	}

	private sealed class Batch31
	{
	}

	private sealed class Batch32
	{
	}

	private sealed class Batch33
	{
	}

	private sealed class Batch34
	{
	}

	private sealed class Batch35
	{
	}

	private sealed class Batch36
	{
	}

	private sealed class Batch37
	{
	}

	private sealed class Batch38
	{
	}

	private sealed class Batch39
	{
	}

	private sealed class Batch40
	{
	}

	private sealed class Batch41
	{
	}

	private sealed class Batch42
	{
	}

	private sealed class Batch43
	{
	}

	private sealed class Batch44
	{
	}

	private sealed class Batch45
	{
	}

	private sealed class Batch46
	{
	}

	private sealed class Batch47
	{
	}

	private sealed class Batch48
	{
	}

	private sealed class Batch49
	{
	}

	private sealed class Batch50
	{
	}

	private class BatchEventHandler<TMarker> : IEventHandler<BatchTestEvent>
	{
		public Task HandleAsync(BatchTestEvent eventMessage, CancellationToken cancellationToken)
		{
			_ = eventMessage.Data.Length;
			return Task.CompletedTask;
		}
	}
}
