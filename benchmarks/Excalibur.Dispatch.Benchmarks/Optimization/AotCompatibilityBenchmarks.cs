// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Managed HostProcess measurements of warmed handler activation and type checks.
/// </summary>
/// <remarks>
/// These jobs do not publish or execute NativeAOT and do not invoke the handler body.
/// Setup prewarms and freezes static activation caches; cleanup does not reset them.
/// The historical method names are retained. The two type-check implementations agree
/// for this fixture but test different general contracts: a specific closed action-handler
/// interface versus any closed action-handler interface. No native/JIT parity or general
/// thread-safety claim follows from these measurements.
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class AotCompatibilityBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IHandlerActivator _activator = null!;
	private IMessageContext _context = null!;
	private Type _handlerType = null!;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<TestHandler>();
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_activator = _serviceProvider.GetRequiredService<IHandlerActivator>();
		_handlerType = typeof(TestHandler);

		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_context = contextFactory.CreateContext();

		// Pre-warm caches
		HandlerActivator.PreWarmCache([_handlerType]);
		HandlerActivator.FreezeCache();
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Baseline: Managed activation through the prewarmed handler activation plan.
	/// </summary>
	[Benchmark(Baseline = true)]
	public object ActivateHandler_CachedDelegates()
	{
		return _activator.ActivateHandler(_handlerType, _context, _serviceProvider);
	}

	/// <summary>
	/// Batch of 100 managed activations; the result counts non-null handlers.
	/// </summary>
	[Benchmark]
	public int ActivateHandler_Batch100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			var handler = _activator.ActivateHandler(_handlerType, _context, _serviceProvider);
			if (handler != null)
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Tests assignability to one statically named closed action-handler interface.
	/// </summary>
	[Benchmark]
	public bool TypeCheck_AotSafe()
	{
		return IsActionHandler(_handlerType);
	}

	/// <summary>
	/// Enumerates interfaces looking for any closed action-handler interface.
	/// </summary>
	[Benchmark]
	public bool TypeCheck_Reflection()
	{
		var interfaces = _handlerType.GetInterfaces();
		foreach (var iface in interfaces)
		{
			if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IActionHandler<>))
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Batch of 1,000 closed-interface assignability checks.
	/// </summary>
	[Benchmark]
	public int TypeCheck_AotSafe_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			if (IsActionHandler(_handlerType))
			{
				count++;
			}
		}

		return count;
	}

	/// <summary>
	/// Batch of 1,000 interface-enumeration checks.
	/// </summary>
	[Benchmark]
	public int TypeCheck_Reflection_Batch1000()
	{
		var count = 0;
		for (var i = 0; i < 1000; i++)
		{
			var interfaces = _handlerType.GetInterfaces();
			foreach (var iface in interfaces)
			{
				if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IActionHandler<>))
				{
					count++;
					break;
				}
			}
		}

		return count;
	}

	/// <summary>
	/// Direct non-generic service resolution from the managed DI container.
	/// </summary>
	[Benchmark]
	public object DirectServiceResolution()
	{
		return _serviceProvider.GetRequiredService(_handlerType);
	}

	/// <summary>
	/// Ten Parallel.For activations; overlap and general thread safety are not established.
	/// </summary>
	[Benchmark]
	public int ConcurrentActivation()
	{
		var count = 0;
		_ = Parallel.For(0, 10, _ =>
		{
			var handler = _activator.ActivateHandler(_handlerType, _context, _serviceProvider);
			if (handler != null)
			{
				_ = Interlocked.Increment(ref count);
			}
		});
		return count;
	}

	/// <summary>
	/// Closed-interface type check helper.
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsActionHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
	{
		// The closed interface is statically known; native execution requires separate verification.
		return typeof(IActionHandler<TestCommand>).IsAssignableFrom(type);
	}

	// Test types
	private sealed record TestCommand : IDispatchAction
	{
		public Guid Id { get; init; }
	}

	private sealed class TestHandler : IActionHandler<TestCommand>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
		{
			_ = command.Id;
			return Task.CompletedTask;
		}
	}
}