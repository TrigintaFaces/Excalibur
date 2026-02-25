// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Linq.Expressions;
using System.Reflection;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Core;

/// <summary>
/// Benchmarks for Handler Resolution with Cached Reflection.
/// </summary>
/// <remarks>
/// <para>
/// These benchmarks validate that <see cref="HandlerActivator"/> uses cached compiled
/// expression-tree delegates instead of reflection on every dispatch.
/// </para>
/// <para>
/// Target: Zero reflection in handler resolution hot path.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.HostProcess)]
public class HandlerActivatorBenchmarks
{
	private IServiceProvider _serviceProvider = null!;
	private IHandlerActivator _activator = null!;
	private IMessageContext _context = null!;
	private Type _handlerType = null!;

	// Cached delegate for comparison
	private Action<object, IMessageContext>? _cachedSetter;

	// Pre-compiled setter for baseline
	private Action<object, IMessageContext>? _precompiledSetter;

	[GlobalSetup]
	public void Setup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<TestHandlerWithContext>();
		_ = services.AddDispatch();

		_serviceProvider = services.BuildServiceProvider();
		_activator = _serviceProvider.GetRequiredService<IHandlerActivator>();
		_handlerType = typeof(TestHandlerWithContext);

		// Create a context for testing
		var contextFactory = _serviceProvider.GetRequiredService<IMessageContextFactory>();
		_context = contextFactory.CreateContext();

		// Pre-warm the cache
		HandlerActivator.PreWarmCache([_handlerType]);

		// Freeze for optimal performance
		HandlerActivator.FreezeCache();

		// Create a manually compiled setter for baseline comparison
		var contextProperty = _handlerType.GetProperty(nameof(TestHandlerWithContext.Context));
		_precompiledSetter = CompilePropertySetter(_handlerType, contextProperty);

		// Also cache the reflection-based approach
		_cachedSetter = _precompiledSetter;
	}

	[GlobalCleanup]
	public void Cleanup()
	{
		(_serviceProvider as IDisposable)?.Dispose();
	}

	/// <summary>
	/// Baseline: Direct property setter invocation (no reflection, no lookup).
	/// </summary>
	[Benchmark(Baseline = true)]
	public void DirectPropertySetter()
	{
		var handler = new TestHandlerWithContext();
		handler.Context = _context;
	}

	/// <summary>
	/// Pre-compiled delegate invocation (no lookup cost).
	/// </summary>
	[Benchmark]
	public void PrecompiledDelegateSetter()
	{
		var handler = new TestHandlerWithContext();
		_precompiledSetter.Invoke(handler, _context);
	}

	/// <summary>
	/// Full HandlerActivator.ActivateHandler with cached delegates.
	/// This is the hot path we're optimizing.
	/// </summary>
	[Benchmark]
	public object ActivateHandler_Cached()
	{
		return _activator.ActivateHandler(_handlerType, _context, _serviceProvider);
	}

	/// <summary>
	/// Reflection-based property setter (what we're avoiding).
	/// </summary>
	[Benchmark]
	public void ReflectionPropertySetter()
	{
		var handler = new TestHandlerWithContext();
		var property = _handlerType.GetProperty(nameof(TestHandlerWithContext.Context));
		property.SetValue(handler, _context);
	}

	/// <summary>
	/// Batch of 100 handler activations to measure sustained performance.
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
	/// Batch of 100 reflection-based setters for comparison.
	/// </summary>
	[Benchmark]
	public int ReflectionSetter_Batch100()
	{
		var count = 0;
		var property = _handlerType.GetProperty(nameof(TestHandlerWithContext.Context));

		for (var i = 0; i < 100; i++)
		{
			var handler = new TestHandlerWithContext();
			property.SetValue(handler, _context);
			count++;
		}

		return count;
	}

	/// <summary>
	/// Compiles an expression tree to a delegate for setting a property.
	/// </summary>
	private static Action<object, IMessageContext> CompilePropertySetter(Type handlerType, PropertyInfo property)
	{
		var handlerParam = Expression.Parameter(typeof(object), "handler");
		var contextParam = Expression.Parameter(typeof(IMessageContext), "context");

		var castHandler = Expression.Convert(handlerParam, handlerType);
		var propertyAccess = Expression.Property(castHandler, property);
		var assign = Expression.Assign(propertyAccess, contextParam);

		var lambda = Expression.Lambda<Action<object, IMessageContext>>(assign, handlerParam, contextParam);
		return lambda.Compile();
	}

	// Test types
	private sealed class TestHandlerWithContext : IActionHandler<TestCommand>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(TestCommand command, CancellationToken cancellationToken)
		{
			_ = command.Id;
			return Task.CompletedTask;
		}
	}

	private sealed record TestCommand : IDispatchAction
	{
		public Guid Id { get; init; }
	}
}
