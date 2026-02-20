// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures handler invoker hot-path cost between precompiled cache hits and runtime fallback.
/// </summary>
[MemoryDiagnoser]
[Config(typeof(DiagnosticsBenchmarkConfig))]
public class HandlerInvokerPathBenchmarks
{
	private HandlerInvoker _invoker = null!;
	private PrecompiledPathHandler _precompiledHandler = null!;
	private PrecompiledPathMessage _precompiledMessage = null!;
	private RuntimePathHandler _runtimeHandler = null!;
	private RuntimePathMessage _runtimeMessage = null!;

	[GlobalSetup]
	public async Task GlobalSetup()
	{
		ResetHandlerInvokerCaches();
		ConfigureSyntheticPrecompiledEntry();

		_invoker = new HandlerInvoker();
		_precompiledHandler = new PrecompiledPathHandler();
		_precompiledMessage = new PrecompiledPathMessage(7);
		_runtimeHandler = new RuntimePathHandler();
		_runtimeMessage = new RuntimePathMessage(11);

		// Warm runtime fallback delegate compilation so benchmark reflects steady-state path cost.
		_ = await _invoker.InvokeAsync(_runtimeHandler, _runtimeMessage, CancellationToken.None).ConfigureAwait(false);
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		ResetHandlerInvokerCaches();
	}

	[Benchmark(Baseline = true, Description = "HandlerInvoker: precompiled cache-hit")]
	public Task<object?> Invoke_PrecompiledCacheHit()
	{
		return _invoker.InvokeAsync(_precompiledHandler, _precompiledMessage, CancellationToken.None);
	}

	[Benchmark(Description = "HandlerInvoker: runtime fallback (cached)")]
	public Task<object?> Invoke_RuntimeFallbackCached()
	{
		return _invoker.InvokeAsync(_runtimeHandler, _runtimeMessage, CancellationToken.None);
	}

	private static void ConfigureSyntheticPrecompiledEntry()
	{
		var invokerType = typeof(HandlerInvoker);
		var invokeDelegateType = invokerType.GetNestedType("PrecompiledInvokerDelegate", BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Unable to resolve PrecompiledInvokerDelegate.");
		var cachedInvokerType = invokerType.GetNestedType("CachedPrecompiledInvoker", BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Unable to resolve CachedPrecompiledInvoker.");

		var invokeMethod = typeof(HandlerInvokerPathBenchmarks).GetMethod(
			nameof(SyntheticInvoke),
			BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Unable to resolve synthetic invoke method.");

		var invokeDelegate = Delegate.CreateDelegate(invokeDelegateType, invokeMethod);
		var cachedInvoker = Activator.CreateInstance(
			cachedInvokerType,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null,
			args: [invokeDelegate],
			culture: null)
			?? throw new InvalidOperationException("Unable to create cached precompiled invoker entry.");

		var cacheField = invokerType.GetField("_precompiledInvokerCache", BindingFlags.Static | BindingFlags.NonPublic)
			?? throw new InvalidOperationException("Unable to resolve precompiled invoker cache field.");
		var precompiledCache = cacheField.GetValue(null)
			?? throw new InvalidOperationException("Unable to access precompiled invoker cache.");

		_ = precompiledCache.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public)
			?.Invoke(precompiledCache, null);

		var tryAddMethod = precompiledCache.GetType().GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.Public)
			?? throw new InvalidOperationException("Unable to resolve precompiled invoker cache TryAdd.");
		var cacheKey = (typeof(PrecompiledPathHandler), typeof(PrecompiledPathMessage));
		_ = tryAddMethod.Invoke(precompiledCache, [cacheKey, cachedInvoker]);
	}

	private static void ResetHandlerInvokerCaches()
	{
		_ = typeof(HandlerInvoker).GetMethod(
			"ClearCache",
			BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)
			?.Invoke(null, null);
	}

	private static Task<object?> SyntheticInvoke(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		_ = handler;
		_ = message;
		_ = cancellationToken;
		return Task.FromResult<object?>(1);
	}

	private sealed class PrecompiledPathMessage(int value) : IDispatchMessage
	{
		public int Value { get; } = value;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "PrecompiledPathMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class RuntimePathMessage(int value) : IDispatchMessage
	{
		public int Value { get; } = value;
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "RuntimePathMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class PrecompiledPathHandler
	{
		public Task<int> HandleAsync(PrecompiledPathMessage message, CancellationToken cancellationToken)
		{
			_ = message.Value;
			_ = cancellationToken;
			return Task.FromResult(1);
		}
	}

	private sealed class RuntimePathHandler
	{
		public Task<int> HandleAsync(RuntimePathMessage message, CancellationToken cancellationToken)
		{
			_ = message.Value;
			_ = cancellationToken;
			return Task.FromResult(2);
		}
	}
}
