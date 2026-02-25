// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Optimization;

/// <summary>
/// Benchmarks for Native AOT Compatibility.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks measure the performance of AOT-compatible code paths
/// versus reflection-based fallbacks.
/// </para>
/// <para>
/// Target: Zero AOT warnings, equivalent performance to JIT.
/// </para>
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
	/// Baseline: Handler activation using cached compiled delegates (AOT-friendly).
	/// </summary>
	[Benchmark(Baseline = true)]
	public object ActivateHandler_CachedDelegates()
	{
		return _activator.ActivateHandler(_handlerType, _context, _serviceProvider);
	}

	/// <summary>
	/// Handler activation batch - measures sustained AOT-friendly activation.
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
	/// Type check without reflection - AOT-safe pattern.
	/// </summary>
	[Benchmark]
	public bool TypeCheck_AotSafe()
	{
		return IsActionHandler(_handlerType);
	}

	/// <summary>
	/// Type check with reflection - what we avoid in AOT.
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
	/// Batch type checks - AOT-safe pattern at scale.
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
	/// Batch type checks - reflection pattern at scale.
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
	/// Direct service resolution - bypassing reflection.
	/// </summary>
	[Benchmark]
	public object DirectServiceResolution()
	{
		return _serviceProvider.GetRequiredService(_handlerType);
	}

	/// <summary>
	/// Concurrent handler activations - validates thread-safety of AOT paths.
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
	/// AOT-safe type check helper (simulates source-generated check).
	/// </summary>
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static bool IsActionHandler(
		[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)] Type type)
	{
		// This pattern is AOT-safe because it doesn't require runtime type discovery
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
