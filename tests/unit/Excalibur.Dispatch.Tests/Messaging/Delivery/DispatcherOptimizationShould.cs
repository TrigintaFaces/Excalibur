// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly - FakeItEasy .Returns() stores ValueTask

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Messaging;

using Microsoft.Extensions.Logging.Abstractions;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Tests specifically targeting the Dispatcher performance optimizations:
/// 1. Non-async fast path (DispatchOptimizedAsync / DispatchOptimizedWithResponseAsync)
/// 2. Combined MessageDispatchInfo cache
/// 3. AsyncLocal context restoration across sync/async/exception paths
/// 4. Cancellation paths
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatcherOptimizationShould
{
	private readonly FinalDispatchHandler _finalHandler = new(
		A.Fake<IMessageBusProvider>(),
		NullLogger<FinalDispatchHandler>.Instance,
		retryPolicy: null,
		new Dictionary<string, IMessageBusOptions>(StringComparer.Ordinal));

	private Dispatcher CreateSut(
		IDispatchMiddlewareInvoker? invoker = null,
		FinalDispatchHandler? handler = null) =>
		new(invoker ?? A.Fake<IDispatchMiddlewareInvoker>(), handler ?? _finalHandler);

	/// <summary>
	/// Creates a genuinely async <see cref="ValueTask{T}"/> using a <see cref="TaskCompletionSource{T}"/>
	/// that completes immediately after creation. This avoids <c>Task.Delay</c> timing dependencies
	/// while still forcing the non-sync path (<c>IsCompletedSuccessfully == false</c> at check time).
	/// </summary>
	private static ValueTask<IMessageResult> CreateAsyncValueTask(IMessageResult result)
	{
		var tcs = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		tcs.SetResult(result);
		return new ValueTask<IMessageResult>(tcs.Task);
	}

	/// <summary>
	/// Creates a genuinely async <see cref="ValueTask{T}"/> that completes with an exception.
	/// </summary>
	private static ValueTask<IMessageResult> CreateAsyncFaultedValueTask(Exception exception)
	{
		var tcs = new TaskCompletionSource<IMessageResult>(TaskCreationOptions.RunContinuationsAsynchronously);
		tcs.SetException(exception);
		return new ValueTask<IMessageResult>(tcs.Task);
	}

	// ─── Optimization 1: Non-Async Fast Path ───────────────────────────

	[Fact]
	public async Task ReturnResultDirectlyWhenMiddlewareCompletesSynchronously()
	{
		// Arrange: middleware returns a pre-completed ValueTask (sync path)
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		var expectedResult = MessageResult.Success();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(expectedResult));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: result matches expected (sync fast path)
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ReuseCachedTaskForSimpleSuccessSingletonResults()
	{
		// Arrange: middleware returns the framework singleton success result.
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(SimpleMessageResult.SuccessResult));

		var sut = CreateSut(invoker);

		// Act
		var firstTask = sut.DispatchAsync(new TestCommand(), new MessageContext(), CancellationToken.None);
		var secondTask = sut.DispatchAsync(new TestCommand(), new MessageContext(), CancellationToken.None);

		// Assert: both dispatches reuse the cached Task wrapper.
		firstTask.ShouldBe(secondTask);
		(await firstTask).ShouldBe(SimpleMessageResult.SuccessResult);
	}

	[Fact]
	public async Task ReuseCachedTaskForTypedCancelledResults()
	{
		// Arrange: pre-cancelled token + ReturnCancelledResult flag should use generic cached task.
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		var sut = CreateSut(invoker);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		var context1 = new MessageContext();
		context1.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);
		var context2 = new MessageContext();
		context2.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);

		// Act
		var firstTask = sut.DispatchAsync<TestQuery, string>(new TestQuery(), context1, cts.Token);
		var secondTask = sut.DispatchAsync<TestQuery, string>(new TestQuery(), context2, cts.Token);

		// Assert
		firstTask.ShouldBe(secondTask);
		(await firstTask).Succeeded.ShouldBeFalse();
	}

	[Fact]
	public async Task ReturnResultWhenMiddlewareCompletesAsynchronously()
	{
		// Arrange: middleware returns a genuinely async ValueTask (no timing dependency)
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		var expectedResult = MessageResult.Success();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(CreateAsyncValueTask(expectedResult));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();

		// Act
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: async continuation returns correct result
		result.ShouldBe(expectedResult);
	}

	// ─── Optimization 2: MessageDispatchInfo Cache ─────────────────────

	[Fact]
	public async Task CacheDispatchInfoAcrossMultipleDispatches()
	{
		// Arrange: dispatch the same message type multiple times
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);

		// Act: dispatch 10 times with same message type
		for (var i = 0; i < 10; i++)
		{
			var message = new TestCommand();
			var context = new MessageContext();
			await sut.DispatchAsync(message, context, CancellationToken.None);
		}

		// Assert: middleware was invoked 10 times (cache doesn't skip dispatch, it caches metadata)
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.MustHaveHappened(10, Times.Exactly);
	}

	[Fact]
	public async Task CorrectlyCategorizeActionMessages()
	{
		// Arrange: use a concrete DispatchMiddlewareInvoker with no middleware (bypass)
		var invoker = new DispatchMiddlewareInvoker([]);
		var sut = CreateSut(invoker);
		var context = new MessageContext();

		// Act: dispatch an action (no response) — exercises IsAction=true in MessageDispatchInfo
		var message = new TestCommand();
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: dispatch completes without error
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task CorrectlyCategorizeEventMessages()
	{
		// Arrange: use a concrete DispatchMiddlewareInvoker with no middleware (bypass)
		var invoker = new DispatchMiddlewareInvoker([]);
		var sut = CreateSut(invoker);
		var context = new MessageContext();

		// Act: dispatch an event — exercises IsEvent=true in MessageDispatchInfo
		var message = new TestEvent();
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: dispatch completes without error
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task CorrectlyCategorizeActionWithResponseMessages()
	{
		// Arrange: use a concrete DispatchMiddlewareInvoker with no middleware (bypass)
		var invoker = new DispatchMiddlewareInvoker([]);
		var sut = CreateSut(invoker);
		var context = new MessageContext();

		// Act: dispatch an action with response — exercises ExpectsResponse=true
		var message = new TestQuery();
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: dispatch completes without error
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task DispatchDifferentMessageTypesSequentiallyWithCorrectCaching()
	{
		// Arrange: verify cache works correctly for multiple distinct types
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);

		// Act: dispatch each type twice — second dispatch of each type should use cached info
		for (var round = 0; round < 2; round++)
		{
			await sut.DispatchAsync(new TestCommand(), new MessageContext(), CancellationToken.None);
			await sut.DispatchAsync(new TestEvent(), new MessageContext(), CancellationToken.None);
			await sut.DispatchAsync(new TestQuery(), new MessageContext(), CancellationToken.None);
		}

		// Assert: all 6 dispatches completed (2 rounds x 3 types)
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.MustHaveHappened(6, Times.Exactly);
	}

	[Fact]
	public async Task HandleConcurrentFirstAccessToMessageDispatchInfoCache()
	{
		// Arrange: test thread safety of cache — many threads hitting GetOrAdd for the first time
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);

		// Use a barrier to ensure all tasks start concurrently
		using var barrier = new Barrier(20);
		var tasks = new Task[20];
		for (var i = 0; i < 20; i++)
		{
			tasks[i] = Task.Run(async () =>
			{
				barrier.SignalAndWait();
				// All 20 tasks dispatch the same type simultaneously
				await sut.DispatchAsync(new TestCommand(), new MessageContext(), CancellationToken.None);
			});
		}

		// Act
		await Task.WhenAll(tasks);

		// Assert: all 20 dispatches completed (no deadlocks, no exceptions)
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.MustHaveHappened(20, Times.Exactly);
	}

	// ─── Optimization 3: AsyncLocal Context Restoration ────────────────

	[Fact]
	public async Task RestoreAmbientContextAfterSyncCompletion()
	{
		// Arrange: set a previous ambient context
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		try
		{
			// Act
			await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

			// Assert: previous context is restored (not the dispatch context)
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task RestoreAmbientContextAfterAsyncCompletion()
	{
		// Arrange: set a previous ambient context
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		// Return a genuinely async ValueTask (forces AwaitDispatchOptimizedAsync path)
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(CreateAsyncValueTask(MessageResult.Success()));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		try
		{
			// Act
			await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

			// Assert: previous context is restored after async completion
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task RestoreAmbientContextAfterAsyncException()
	{
		// Arrange: set a previous ambient context
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		// Return an async ValueTask that faults
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(CreateAsyncFaultedValueTask(new InvalidOperationException("async failure")));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		try
		{
			// Act
			try
			{
				await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
				// expected
			}

			// Assert: previous context is restored even after async exception
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task RestoreAmbientContextAfterSyncException()
	{
		// Arrange: set a previous ambient context
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		// Throw synchronously
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Throws(new InvalidOperationException("sync failure"));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		try
		{
			// Act
			try
			{
				await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);
			}
			catch (InvalidOperationException)
			{
				// expected
			}

			// Assert: previous context is restored even after sync exception
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task SetCorrectAmbientContextDuringMiddlewareExecution()
	{
		// Arrange
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		IMessageContext? capturedDuringMiddleware = null;

		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Invokes(_ => capturedDuringMiddleware = MessageContextHolder.Current)
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		// Act
		await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

		// Assert: middleware sees the dispatch context as ambient
		capturedDuringMiddleware.ShouldBe(dispatchContext);
	}

	[Fact]
	public async Task RestoreNullAmbientContextWhenNoPreviousContextExists()
	{
		// Arrange: no previous ambient context
		MessageContextHolder.Current = null;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();

		// Act
		await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

		// Assert: ambient context restored to null (not left as dispatchContext)
		MessageContextHolder.Current.ShouldBeNull();
	}

	// ─── Optimization 4: Cancellation Paths ────────────────────────────

	[Fact]
	public async Task ReturnCancelledResultWhenTokenPreCancelled()
	{
		// Arrange: pre-cancelled token with ReturnCancelledResult flag
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		context.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act
		var result = await sut.DispatchAsync(message, context, cts.Token);

		// Assert: returns cancelled result without invoking middleware
		result.ShouldNotBeNull();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task ThrowOperationCancelledWhenTokenPreCancelledWithoutFlag()
	{
		// Arrange: pre-cancelled token WITHOUT ReturnCancelledResult flag
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		using var cts = new CancellationTokenSource();
		cts.Cancel();

		// Act & Assert: throws OCE because ReturnCancelledResult is false
		await Should.ThrowAsync<OperationCanceledException>(
			() => sut.DispatchAsync(message, context, cts.Token));
	}

	[Fact]
	public async Task HandleCancellationDuringAsyncMiddlewareWithReturnFlag()
	{
		// Arrange: middleware completes with an async OCE (deterministic, no timing)
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(CreateAsyncFaultedValueTask(new OperationCanceledException("cancelled in async path")));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		context.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);

		// Act
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: returns cancelled result (handled by AwaitDispatchOptimizedAsync catch)
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task HandleCancellationDuringSyncMiddlewareWithReturnFlag()
	{
		// Arrange: middleware throws OCE synchronously
		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Throws(new OperationCanceledException("cancelled in sync path"));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var context = new MessageContext();
		context.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);

		// Act
		var result = await sut.DispatchAsync(message, context, CancellationToken.None);

		// Assert: returns cancelled result (handled by catch in DispatchOptimizedAsync)
		result.ShouldNotBeNull();
	}

	[Fact]
	public async Task RestoreAmbientContextAfterCancellation()
	{
		// Arrange: set previous context, then cancel during dispatch
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Throws(new OperationCanceledException("cancelled"));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();
		dispatchContext.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);

		try
		{
			// Act
			await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

			// Assert: previous context restored after cancellation
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	[Fact]
	public async Task RestoreAmbientContextAfterAsyncCancellation()
	{
		// Arrange: set previous context, then cancel asynchronously during dispatch
		var previousContext = new MessageContext();
		MessageContextHolder.Current = previousContext;

		var invoker = A.Fake<IDispatchMiddlewareInvoker>();
		A.CallTo(invoker)
			.WithReturnType<ValueTask<IMessageResult>>()
			.Returns(CreateAsyncFaultedValueTask(new OperationCanceledException("async cancelled")));

		var sut = CreateSut(invoker);
		var message = A.Fake<IDispatchMessage>();
		var dispatchContext = new MessageContext();
		dispatchContext.SetItem(Dispatcher.ReturnCancelledResultContextKey, true);

		try
		{
			// Act
			await sut.DispatchAsync(message, dispatchContext, CancellationToken.None);

			// Assert: previous context restored even after async cancellation
			MessageContextHolder.Current.ShouldBe(previousContext);
		}
		finally
		{
			MessageContextHolder.Current = null;
		}
	}

	// ─── Test message types ────────────────────────────────────────────

	private sealed class TestCommand : IDispatchAction;

	private sealed class TestEvent : IDispatchEvent;

	private sealed class TestQuery : IDispatchAction<string>;
}
