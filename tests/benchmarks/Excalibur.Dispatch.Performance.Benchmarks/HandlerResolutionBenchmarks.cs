// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Reflection;

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Performance.Benchmarks;

/// <summary>
/// Benchmarks for handler resolution and registration performance.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
public class HandlerResolutionBenchmarks
{
	private Dictionary<Type, Type> _dictionaryRegistry = null!;
	private ConcurrentDictionary<Type, Type> _concurrentRegistry = null!;
	private Dictionary<Type, Func<object>> _factoryRegistry = null!;
	private Type[] _messageTypes = null!;

	[GlobalSetup]
	public void Setup()
	{
		_dictionaryRegistry = new Dictionary<Type, Type>
		{
			[typeof(TestMessage1)] = typeof(TestHandler1),
			[typeof(TestMessage2)] = typeof(TestHandler2),
			[typeof(TestMessage3)] = typeof(TestHandler3),
			[typeof(TestMessage4)] = typeof(TestHandler4),
			[typeof(TestMessage5)] = typeof(TestHandler5),
		};

		_concurrentRegistry = new ConcurrentDictionary<Type, Type>(_dictionaryRegistry);

		_factoryRegistry = new Dictionary<Type, Func<object>>
		{
			[typeof(TestMessage1)] = () => new TestHandler1(),
			[typeof(TestMessage2)] = () => new TestHandler2(),
			[typeof(TestMessage3)] = () => new TestHandler3(),
			[typeof(TestMessage4)] = () => new TestHandler4(),
			[typeof(TestMessage5)] = () => new TestHandler5(),
		};

		_messageTypes =
		[
			typeof(TestMessage1),
			typeof(TestMessage2),
			typeof(TestMessage3),
			typeof(TestMessage4),
			typeof(TestMessage5),
		];
	}

	[Benchmark(Baseline = true)]
	public Type? Dictionary_ResolveHandler()
	{
		return _dictionaryRegistry.TryGetValue(typeof(TestMessage1), out var handler) ? handler : null;
	}

	[Benchmark]
	public Type? ConcurrentDictionary_ResolveHandler()
	{
		return _concurrentRegistry.TryGetValue(typeof(TestMessage1), out var handler) ? handler : null;
	}

	[Benchmark]
	public object? Factory_CreateHandler()
	{
		return _factoryRegistry.TryGetValue(typeof(TestMessage1), out var factory) ? factory() : null;
	}

	[Benchmark]
	public object? Activator_CreateHandler()
	{
		if (_dictionaryRegistry.TryGetValue(typeof(TestMessage1), out var handlerType))
		{
			return Activator.CreateInstance(handlerType);
		}

		return null;
	}

	[Benchmark]
	public void Dictionary_RegisterHandler()
	{
		var registry = new Dictionary<Type, Type>();
		registry[typeof(TestMessage1)] = typeof(TestHandler1);
	}

	[Benchmark]
	public void ConcurrentDictionary_RegisterHandler()
	{
		var registry = new ConcurrentDictionary<Type, Type>();
		registry[typeof(TestMessage1)] = typeof(TestHandler1);
	}

	[Benchmark]
	public void Dictionary_RegisterMultipleHandlers()
	{
		var registry = new Dictionary<Type, Type>();
		for (var i = 0; i < 5; i++)
		{
			registry[_messageTypes[i]] = _dictionaryRegistry[_messageTypes[i]];
		}
	}

	[Benchmark]
	public Type? GetHandlerType_FromMessage()
	{
		var message = new TestMessage1();
		var messageType = message.GetType();
		return _dictionaryRegistry.TryGetValue(messageType, out var handler) ? handler : null;
	}

	[Benchmark]
	public bool Dictionary_ContainsHandler()
	{
		return _dictionaryRegistry.ContainsKey(typeof(TestMessage1));
	}

	[Benchmark]
	public Type[] GetInterfacesForHandler()
	{
		return typeof(TestHandler1).GetInterfaces();
	}

	[Benchmark]
	public bool CheckHandlerImplementsInterface()
	{
		return typeof(ITestHandler<TestMessage1>).IsAssignableFrom(typeof(TestHandler1));
	}

	[Benchmark]
	public IEnumerable<Type> FindHandlersInAssembly()
	{
		var handlerInterface = typeof(ITestHandler<>);
		return Assembly.GetExecutingAssembly().GetTypes()
			.Where(t => t.IsClass && !t.IsAbstract)
			.Where(t => t.GetInterfaces()
				.Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface))
			.Take(5);
	}

	private interface ITestHandler<in TMessage>
	{
		Task HandleAsync(TMessage message, CancellationToken cancellationToken);
	}

	private sealed class TestMessage1 : IDispatchMessage;
	private sealed class TestMessage2 : IDispatchMessage;
	private sealed class TestMessage3 : IDispatchMessage;
	private sealed class TestMessage4 : IDispatchMessage;
	private sealed class TestMessage5 : IDispatchMessage;

	private sealed class TestHandler1 : ITestHandler<TestMessage1>
	{
		public Task HandleAsync(TestMessage1 message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestHandler2 : ITestHandler<TestMessage2>
	{
		public Task HandleAsync(TestMessage2 message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestHandler3 : ITestHandler<TestMessage3>
	{
		public Task HandleAsync(TestMessage3 message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestHandler4 : ITestHandler<TestMessage4>
	{
		public Task HandleAsync(TestMessage4 message, CancellationToken cancellationToken) => Task.CompletedTask;
	}

	private sealed class TestHandler5 : ITestHandler<TestMessage5>
	{
		public Task HandleAsync(TestMessage5 message, CancellationToken cancellationToken) => Task.CompletedTask;
	}
}
