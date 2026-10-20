// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using BenchmarkDotNet.Attributes;

using Excalibur.Dispatch;
using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Benchmarks.Diagnostics;

/// <summary>
/// Measures synthetic registered-invoker hits and warmed runtime fallback in an isolated managed process.
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
		EnsureMutableCaches();
		HandlerInvokerRegistry.RegisterInvoker<PrecompiledPathHandler, PrecompiledPathMessage, object?>(SyntheticInvoke);

		_invoker = new HandlerInvoker();
		_precompiledHandler = new PrecompiledPathHandler();
		_precompiledMessage = new PrecompiledPathMessage(7);
		_runtimeHandler = new RuntimePathHandler();
		_runtimeMessage = new RuntimePathMessage(11);

		// Warm runtime fallback delegate compilation so benchmark reflects steady-state path cost.
		_ = await _invoker.InvokeAsync(_runtimeHandler, _runtimeMessage, CancellationToken.None).ConfigureAwait(false);
		EnsureMutableCaches();
	}

	[GlobalCleanup]
	public void GlobalCleanup()
	{
		try
		{
			EnsureMutableCaches();
		}
		finally
		{
			_invoker = null!;
			_precompiledHandler = null!;
			_precompiledMessage = null!;
			_runtimeHandler = null!;
			_runtimeMessage = null!;
		}
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

	internal static (Type Handler, Type Message) RegisteredPair => (typeof(PrecompiledPathHandler), typeof(PrecompiledPathMessage));
	internal static (Type Handler, Type Message) RuntimePair => (typeof(RuntimePathHandler), typeof(RuntimePathMessage));
	internal bool HoldsFixtureState => _invoker is not null || _precompiledHandler is not null ||
		_precompiledMessage is not null || _runtimeHandler is not null || _runtimeMessage is not null;

	internal static void VerifyFallbackGuard() =>
		_ = new PrecompiledPathHandler().HandleAsync(new PrecompiledPathMessage(7), CancellationToken.None);

	private static void EnsureMutableCaches()
	{
		if (HandlerInvokerRegistry.IsCacheFrozen || HandlerInvoker.IsCacheFrozen)
		{
			throw new InvalidOperationException("Run this fixture in a fresh process with mutable invoker caches and no concurrent cache mutation.");
		}
	}

	private static Task<object?> SyntheticInvoke(PrecompiledPathHandler handler, PrecompiledPathMessage message, CancellationToken cancellationToken)
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
			throw new InvalidOperationException("The synthetic registered-invoker benchmark unexpectedly used runtime fallback.");
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
