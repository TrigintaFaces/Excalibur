// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Benchmarks.Diagnostics.Support;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Compares handler activation strategies under warm-path conditions.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class ActivationStrategyBenchmarks
{
	private IServiceProvider? _services;
	private IMessageContext? _context;
	private IHandlerActivator? _handlerActivator;
	private readonly IHandlerActivator _reflectionActivator = new ReflectionHandlerActivator();
	private readonly IHandlerActivator _cachedReflectionActivator = new CachedReflectionHandlerActivator();
	private static readonly Type BenchmarkHandlerType = typeof(ActivationBenchmarkHandler);

	[GlobalSetup]
	public void GlobalSetup()
	{
		var services = new ServiceCollection();
		_ = services.AddLogging();
		_ = services.AddTransient<ActivationBenchmarkHandler>();
		_ = services.AddDispatch();

		_services = services.BuildServiceProvider();
		_handlerActivator = _services.GetRequiredService<IHandlerActivator>();

		var contextFactory = _services.GetRequiredService<IMessageContextFactory>();
		_context = contextFactory.CreateContext();

		// Prime + freeze the cache so this benchmark represents the hot path users care about.
		HandlerActivator.PreWarmCache([BenchmarkHandlerType]);
		HandlerActivator.FreezeCache();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		(_services as IDisposable)?.Dispose();
	}

	[Benchmark(Baseline = true, Description = "HandlerActivator (warm/frozen)")]
	public object HandlerActivator_Warm()
	{
		return _handlerActivator!.ActivateHandler(BenchmarkHandlerType, _context!, _services!);
	}

	[Benchmark(Description = "Reflection activator (naive per-call property scan)")]
	public object ReflectionActivator_Naive()
	{
		return _reflectionActivator.ActivateHandler(BenchmarkHandlerType, _context!, _services!);
	}

	[Benchmark(Description = "Reflection activator (cached property lookup)")]
	public object ReflectionActivator_CachedProperty()
	{
		return _cachedReflectionActivator.ActivateHandler(BenchmarkHandlerType, _context!, _services!);
	}

	[Benchmark(Description = "HandlerActivator batch x100")]
	public int HandlerActivator_Batch100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			if (_handlerActivator!.ActivateHandler(BenchmarkHandlerType, _context!, _services!) is not null)
			{
				count++;
			}
		}

		return count;
	}

	[Benchmark(Description = "Reflection naive batch x100")]
	public int ReflectionActivatorNaive_Batch100()
	{
		var count = 0;
		for (var i = 0; i < 100; i++)
		{
			if (_reflectionActivator.ActivateHandler(BenchmarkHandlerType, _context!, _services!) is not null)
			{
				count++;
			}
		}

		return count;
	}

	private sealed class ReflectionHandlerActivator : IHandlerActivator
	{
		[RequiresUnreferencedCode("Uses reflection to resolve and set handler context properties.")]
		public object ActivateHandler(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
			IMessageContext context,
			IServiceProvider provider)
		{
			var handler = ActivatorUtilities.GetServiceOrCreateInstance(provider, handlerType);

			var contextProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
				.FirstOrDefault(static p => p.CanWrite && p.PropertyType == typeof(IMessageContext));

			contextProperty?.SetValue(handler, context);
			return handler;
		}
	}

	private sealed class CachedReflectionHandlerActivator : IHandlerActivator
	{
		private readonly ConcurrentDictionary<Type, PropertyInfo?> _contextPropertyCache = new();

		[RequiresUnreferencedCode("Uses reflection to resolve and set handler context properties.")]
		public object ActivateHandler(
			[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)] Type handlerType,
			IMessageContext context,
			IServiceProvider provider)
		{
			var handler = ActivatorUtilities.GetServiceOrCreateInstance(provider, handlerType);

			var contextProperty = _contextPropertyCache.GetOrAdd(handlerType, static type =>
				type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
					.FirstOrDefault(static p => p.CanWrite && p.PropertyType == typeof(IMessageContext)));

			contextProperty?.SetValue(handler, context);
			return handler;
		}
	}

	private sealed class ActivationBenchmarkHandler : IActionHandler<DiagnosticCommand>
	{
		public IMessageContext? Context { get; set; }

		public Task HandleAsync(DiagnosticCommand action, CancellationToken cancellationToken)
		{
			_ = action.Value;
			return Task.CompletedTask;
		}
	}
}
