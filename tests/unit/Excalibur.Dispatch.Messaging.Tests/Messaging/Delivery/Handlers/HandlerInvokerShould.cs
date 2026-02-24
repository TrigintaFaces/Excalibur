// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Reflection;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Handlers;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Handlers;

/// <summary>
/// Tests for <see cref="HandlerInvoker"/> - the default handler invoker using expression compilation.
/// </summary>
/// <remarks>
/// Sprint 413 - Task T413.2: HandlerInvoker tests (81.3% → 95%).
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class HandlerInvokerShould : IDisposable
{
	private readonly HandlerInvoker _invoker = new();

	public void Dispose()
	{
		// Clear cache between tests to ensure isolation
		HandlerInvoker.ClearCache();
	}

	#region Null Argument Validation Tests

	[Fact]
	public async Task ThrowArgumentNullException_WhenHandlerIsNull()
	{
		var message = new TestMessage();

		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_invoker.InvokeAsync(null!, message, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowArgumentNullException_WhenMessageIsNull()
	{
		var handler = new TaskOnlyHandler();

		_ = await Should.ThrowAsync<ArgumentNullException>(() =>
			_invoker.InvokeAsync(handler, null!, CancellationToken.None));
	}

	#endregion

	#region Task Return Type Tests

	[Fact]
	public async Task InvokeHandlerReturningTaskCompletesWithNullResult()
	{
		var handler = new TaskOnlyHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task InvokeHandlerReturningTaskOfTUnwrapsResult()
	{
		var handler = new TaskResultHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<int>().ShouldBe(42);
	}

	[Fact]
	public async Task InvokeHandlerReturningTaskOfStringUnwrapsResult()
	{
		var handler = new TaskStringHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<string>().ShouldBe("hello");
	}

	[Fact]
	public async Task InvokeHandlerReturningTaskOfBoolUseCachedTrueValue()
	{
		var handler = new TaskBoolTrueHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<bool>().ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeHandlerReturningTaskOfBoolUseCachedFalseValue()
	{
		var handler = new TaskBoolFalseHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<bool>().ShouldBeFalse();
	}

	#endregion

	#region ValueTask Return Type Tests

	[Fact]
	public async Task InvokeHandlerReturningValueTaskCompletesWithNullResult()
	{
		var handler = new ValueTaskOnlyHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeNull();
	}

	[Fact]
	public async Task InvokeHandlerReturningValueTaskOfTUnwrapsResult()
	{
		var handler = new ValueTaskResultHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<int>().ShouldBe(99);
	}

	[Fact]
	public async Task InvokeHandlerReturningValueTaskOfBoolUseCachedValue()
	{
		var handler = new ValueTaskBoolHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		result.ShouldBeOfType<bool>().ShouldBeTrue();
	}

	[Fact]
	public async Task InvokeHandlerReturningValueTaskOfCollectionPropagatesBatchResults()
	{
		var handler = new BatchResultHandler();
		var message = new TestMessage();

		var result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);

		_ = result.ShouldBeAssignableTo<IReadOnlyList<IMessageResult>>();
		var batchResults = (IReadOnlyList<IMessageResult>)result!;
		batchResults.Count.ShouldBe(1);
		batchResults[0].Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Exception Handling Tests

	[Fact]
	public async Task ThrowInvalidOperationException_WhenHandlerHasNoHandleAsyncMethod()
	{
		var handler = new NoHandleAsyncHandler();
		var message = new TestMessage();

		_ = await Should.ThrowAsync<InvalidOperationException>(() =>
			_invoker.InvokeAsync(handler, message, CancellationToken.None));
	}

	[Fact]
	public async Task PropagateException_WhenHandlerThrows()
	{
		var handler = new ThrowingHandler();
		var message = new TestMessage();

		var exception = await Should.ThrowAsync<InvalidOperationException>(() =>
			_invoker.InvokeAsync(handler, message, CancellationToken.None));

		exception.Message.ShouldBe("Handler failed");
	}

	[Fact]
	public async Task PropagateOperationCanceledException_WhenCancelled()
	{
		var handler = new CancellationAwareHandler();
		var message = new TestMessage();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		_ = await Should.ThrowAsync<OperationCanceledException>(() =>
			_invoker.InvokeAsync(handler, message, cts.Token));
	}

	#endregion

	#region Caching Tests

	[Fact]
	public async Task CacheInvokerForRepeatedCalls()
	{
		var handler = new TaskResultHandler();
		var message = new TestMessage();

		// First call builds the invoker
		var result1 = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		result1.ShouldBe(42);

		// Second call should use cached invoker (we verify behavior works the same)
		var result2 = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
		result2.ShouldBe(42);
	}

	[Fact]
	public async Task UseDifferentInvokersForDifferentHandlerTypes()
	{
		var handler1 = new TaskResultHandler();
		var handler2 = new ValueTaskResultHandler();
		var message = new TestMessage();

		var result1 = await _invoker.InvokeAsync(handler1, message, CancellationToken.None);
		result1.ShouldBe(42);

		var result2 = await _invoker.InvokeAsync(handler2, message, CancellationToken.None);
		result2.ShouldBe(99);
	}

	[Fact]
	public async Task Prefer_Precompiled_Generated_Invoker_When_Available()
	{
		var handler = new PrecompiledPathHandler();
		var message = new PrecompiledPathMessage();
		object? result = null;
		var usedSyntheticInvoker = false;

		// Global precompiled caches are reset on assembly-load notifications; retry with a
		// bounded loop to avoid transient misses while still asserting the precompiled path works.
		for (var attempt = 0; attempt < 5 && !usedSyntheticInvoker; attempt++)
		{
			ConfigureSyntheticPrecompiledProvider();
			result = await _invoker.InvokeAsync(handler, message, CancellationToken.None);
			usedSyntheticInvoker = result is 777 && Volatile.Read(ref s_syntheticPrecompiledInvocations) == 1;
		}

		usedSyntheticInvoker.ShouldBeTrue();
		result.ShouldBe(777);
		Volatile.Read(ref s_syntheticPrecompiledInvocations).ShouldBe(1);
	}

	#endregion

	#region Test Fixtures

	private static int s_syntheticPrecompiledInvocations;

	private static void ConfigureSyntheticPrecompiledProvider()
	{
		HandlerInvoker.ClearCache();
		_ = Interlocked.Exchange(ref s_syntheticPrecompiledInvocations, 0);

		var invokerType = typeof(HandlerInvoker);
		var invokeDelegateType = invokerType.GetNestedType("PrecompiledInvokerDelegate", BindingFlags.NonPublic);
		var cachedInvokerType = invokerType.GetNestedType("CachedPrecompiledInvoker", BindingFlags.NonPublic);
		invokeDelegateType.ShouldNotBeNull();
		cachedInvokerType.ShouldNotBeNull();

		var invokeMethod = typeof(HandlerInvokerShould).GetMethod(nameof(SyntheticInvoke), BindingFlags.Static | BindingFlags.NonPublic);
		invokeMethod.ShouldNotBeNull();

		var invokeDelegate = Delegate.CreateDelegate(invokeDelegateType!, invokeMethod!);
		var cachedInvoker = Activator.CreateInstance(
			cachedInvokerType!,
			BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
			binder: null,
			[invokeDelegate],
			culture: null);
		cachedInvoker.ShouldNotBeNull();

		var cacheField = invokerType.GetField("_precompiledInvokerCache", BindingFlags.Static | BindingFlags.NonPublic);
		cacheField.ShouldNotBeNull();

		var precompiledCache = cacheField!.GetValue(null);
		precompiledCache.ShouldNotBeNull();
		_ = precompiledCache!.GetType().GetMethod("Clear", BindingFlags.Instance | BindingFlags.Public)?.Invoke(precompiledCache, null);
		var tryAddMethod = precompiledCache.GetType().GetMethod("TryAdd", BindingFlags.Instance | BindingFlags.Public);
		tryAddMethod.ShouldNotBeNull();
		var key = (typeof(PrecompiledPathHandler), typeof(PrecompiledPathMessage));
		_ = tryAddMethod!.Invoke(precompiledCache, [key, cachedInvoker]);
	}

	private static Task<object?> SyntheticInvoke(object handler, IDispatchMessage message, CancellationToken cancellationToken)
	{
		_ = Interlocked.Increment(ref s_syntheticPrecompiledInvocations);
		return Task.FromResult<object?>(777);
	}

	private sealed class TestMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	// Task return type handlers
	private sealed class TaskOnlyHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.CompletedTask;
	}

	private sealed class TaskResultHandler
	{
		public async Task<int> HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			await Task.Delay(10, cancellationToken).ConfigureAwait(false);
			return 42;
		}
	}

	private sealed class TaskStringHandler
	{
		public Task<string> HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.FromResult("hello");
	}

	private sealed class TaskBoolTrueHandler
	{
		public Task<bool> HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.FromResult(true);
	}

	private sealed class TaskBoolFalseHandler
	{
		public Task<bool> HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> Task.FromResult(false);
	}

	// ValueTask return type handlers
	private sealed class ValueTaskOnlyHandler
	{
		public ValueTask HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> ValueTask.CompletedTask;
	}

	private sealed class ValueTaskResultHandler
	{
		public ValueTask<int> HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> ValueTask.FromResult(99);
	}

	private sealed class ValueTaskBoolHandler
	{
		public ValueTask<bool> HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> ValueTask.FromResult(true);
	}

	private sealed class BatchResultHandler
	{
		public ValueTask<IReadOnlyList<IMessageResult>> HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			IReadOnlyList<IMessageResult> results = new[] { MessageResult.Success() };
			return ValueTask.FromResult(results);
		}
	}

	// Exception/error handlers
	private sealed class NoHandleAsyncHandler
	{
		// Intentionally no HandleAsync method
		public void SomeOtherMethod() { }
	}

	private sealed class ThrowingHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
			=> throw new InvalidOperationException("Handler failed");
	}

	private sealed class CancellationAwareHandler
	{
		public Task HandleAsync(TestMessage message, CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();
			return Task.CompletedTask;
		}
	}

	private sealed class PrecompiledPathMessage : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "PrecompiledPathMessage";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class PrecompiledPathHandler
	{
		public Task<int> HandleAsync(PrecompiledPathMessage message, CancellationToken cancellationToken)
			=> Task.FromResult(13);
	}

	#endregion
}
