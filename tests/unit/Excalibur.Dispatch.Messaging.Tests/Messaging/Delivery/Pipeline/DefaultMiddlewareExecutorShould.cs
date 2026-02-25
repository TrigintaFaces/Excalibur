// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;
using Excalibur.Dispatch.Tests.TestFakes;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery.Pipeline;

/// <summary>
/// Unit tests for <see cref="DefaultMiddlewareExecutor"/>.
/// Sprint 451 - S451.5: Tests for PERF-4 CastResultAsync optimization.
/// </summary>
/// <remarks>
/// These tests verify the async/await pattern used in CastResultAsync
/// instead of the previous ContinueWith approach, which eliminates
/// closure allocations in the typed middleware execution path.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Performance")]
public sealed class DefaultMiddlewareExecutorShould
{
	private static readonly IDispatchMiddleware[] EmptyMiddlewares = [];
	private readonly DefaultMiddlewareExecutor _executor;

	public DefaultMiddlewareExecutorShould()
	{
		_executor = new DefaultMiddlewareExecutor();
	}

	#region ExecuteAsync (Untyped) Tests

	[Fact]
	public async Task ExecuteAsync_WithNoMiddleware_CallsFinalDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedResult = MessageResult.Success();
		var finalDelegateCalled = false;

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
		{
			finalDelegateCalled = true;
			return new ValueTask<IMessageResult>(expectedResult);
		};

		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		var result = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			CancellationToken.None);

		// Assert
		finalDelegateCalled.ShouldBeTrue();
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task ExecuteAsync_WithSingleMiddleware_ExecutesMiddlewareThenFinalDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedResult = MessageResult.Success();
		var executionOrder = new List<string>();

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.Invokes(call =>
			{
				executionOrder.Add("middleware");
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				var msg = call.GetArgument<IDispatchMessage>(0);
				var ctx = call.GetArgument<IMessageContext>(1);
				var ct = call.GetArgument<CancellationToken>(3);
				next(msg, ctx, ct).AsTask().Wait();
			})
			.ReturnsLazily(_ => new ValueTask<IMessageResult>(expectedResult));

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
		{
			executionOrder.Add("final");
			return new ValueTask<IMessageResult>(expectedResult);
		};

		var middlewareContext = new MiddlewareContext([middleware]);

		// Act
		var result = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			CancellationToken.None);

		// Assert
		executionOrder.ShouldBe(["middleware", "final"]);
	}

	[Fact]
	public async Task ExecuteAsync_WithMultipleMiddleware_ExecutesInOrder()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedResult = MessageResult.Success();
		var executionOrder = new List<string>();

		IDispatchMiddleware CreateOrderTrackingMiddleware(string name)
		{
			var middleware = A.Fake<IDispatchMiddleware>();
			_ = A.CallTo(() => middleware.InvokeAsync(
					A<IDispatchMessage>._,
					A<IMessageContext>._,
					A<DispatchRequestDelegate>._,
					A<CancellationToken>._))
				.Invokes(async call =>
				{
					executionOrder.Add($"{name}-before");
					var next = call.GetArgument<DispatchRequestDelegate>(2);
					var msg = call.GetArgument<IDispatchMessage>(0);
					var ctx = call.GetArgument<IMessageContext>(1);
					var ct = call.GetArgument<CancellationToken>(3);
					_ = await next(msg, ctx, ct);
					executionOrder.Add($"{name}-after");
				})
				.ReturnsLazily(_ => new ValueTask<IMessageResult>(expectedResult));
			return middleware;
		}

		var middleware1 = CreateOrderTrackingMiddleware("m1");
		var middleware2 = CreateOrderTrackingMiddleware("m2");

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
		{
			executionOrder.Add("final");
			return new ValueTask<IMessageResult>(expectedResult);
		};

		var middlewareContext = new MiddlewareContext([middleware1, middleware2]);

		// Act
		_ = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			CancellationToken.None);

		// Assert
		executionOrder.ShouldContain("m1-before");
		executionOrder.ShouldContain("m2-before");
		executionOrder.ShouldContain("final");
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesExceptionFromMiddleware()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedException = new InvalidOperationException("Middleware error");

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ThrowsAsync(expectedException);

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(MessageResult.Success());

		var middlewareContext = new MiddlewareContext([middleware]);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			_executor.ExecuteAsync(
				ref middlewareContext,
				message,
				context,
				finalDelegate,
				CancellationToken.None).AsTask());

		ex.Message.ShouldBe("Middleware error");
	}

	[Fact]
	public async Task ExecuteAsync_PropagatesExceptionFromFinalDelegate()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedException = new InvalidOperationException("Final delegate error");

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
			throw expectedException;

		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _executor.ExecuteAsync(
				ref middlewareContext,
				message,
				context,
				finalDelegate,
				CancellationToken.None));

		ex.Message.ShouldBe("Final delegate error");
	}

	[Fact]
	public async Task ExecuteAsync_PassesCancellationTokenThroughPipeline()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		using var cts = new CancellationTokenSource();
		var passedToken = CancellationToken.None;

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
		{
			passedToken = ct;
			return new ValueTask<IMessageResult>(MessageResult.Success());
		};

		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		_ = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			cts.Token);

		// Assert
		passedToken.ShouldBe(cts.Token);
	}

	#endregion

	#region ExecuteAsync<TMessage, TResponse> (Typed) Tests - CastResultAsync

	[Fact]
	public async Task ExecuteAsync_Typed_ReturnsTypedResult()
	{
		// Arrange
		var message = new TestDispatchAction { Data = "test-data" };
		var context = new FakeMessageContext();
		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		var result = await _executor.ExecuteAsync<TestDispatchAction, string>(
			ref middlewareContext,
			message,
			context,
			CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeAssignableTo<IMessageResult<string>>();
	}

	[Fact]
	public async Task ExecuteAsync_Typed_HandlesNullResult()
	{
		// Arrange
		var message = new TestDispatchActionNullable();
		var context = new FakeMessageContext();
		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		var result = await _executor.ExecuteAsync<TestDispatchActionNullable, string?>(
			ref middlewareContext,
			message,
			context,
			CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_Typed_ExecutesMiddlewareBeforeFinalDelegate()
	{
		// Arrange
		var message = new TestDispatchAction();
		var context = new FakeMessageContext();
		var middlewareExecuted = false;

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.Invokes(async call =>
			{
				middlewareExecuted = true;
				var next = call.GetArgument<DispatchRequestDelegate>(2);
				var msg = call.GetArgument<IDispatchMessage>(0);
				var ctx = call.GetArgument<IMessageContext>(1);
				var ct = call.GetArgument<CancellationToken>(3);
				_ = await next(msg, ctx, ct);
			})
			.ReturnsLazily(_ => new ValueTask<IMessageResult>(MessageResult.Success("result")));

		var middlewareContext = new MiddlewareContext([middleware]);

		// Act
		_ = await _executor.ExecuteAsync<TestDispatchAction, string>(
			ref middlewareContext,
			message,
			context,
			CancellationToken.None);

		// Assert
		middlewareExecuted.ShouldBeTrue();
	}

	[Fact]
	public async Task ExecuteAsync_Typed_PropagatesExceptionFromMiddleware()
	{
		// Arrange
		var message = new TestDispatchAction();
		var context = new FakeMessageContext();
		var expectedException = new InvalidOperationException("Typed middleware error");

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.ThrowsAsync(expectedException);

		var middlewareContext = new MiddlewareContext([middleware]);

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			_executor.ExecuteAsync<TestDispatchAction, string>(
				ref middlewareContext,
				message,
				context,
				CancellationToken.None).AsTask());

		ex.Message.ShouldBe("Typed middleware error");
	}

	[Fact]
	public async Task ExecuteAsync_Typed_HandlesValueTypeResult()
	{
		// Arrange
		var message = new TestDispatchActionWithInt();
		var context = new FakeMessageContext();
		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		var result = await _executor.ExecuteAsync<TestDispatchActionWithInt, int>(
			ref middlewareContext,
			message,
			context,
			CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		_ = result.ShouldBeAssignableTo<IMessageResult<int>>();
	}

	[Fact]
	public async Task ExecuteAsync_Typed_PassesCancellationToken()
	{
		// Arrange
		var message = new TestDispatchAction();
		var context = new FakeMessageContext();
		using var cts = new CancellationTokenSource();
		var receivedToken = CancellationToken.None;

		var middleware = A.Fake<IDispatchMiddleware>();
		_ = A.CallTo(() => middleware.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<DispatchRequestDelegate>._,
				A<CancellationToken>._))
			.Invokes(call =>
			{
				receivedToken = call.GetArgument<CancellationToken>(3);
			})
			.ReturnsLazily(_ => new ValueTask<IMessageResult>(MessageResult.Success("result")));

		var middlewareContext = new MiddlewareContext([middleware]);

		// Act
		_ = await _executor.ExecuteAsync<TestDispatchAction, string>(
			ref middlewareContext,
			message,
			context,
			cts.Token);

		// Assert
		receivedToken.ShouldBe(cts.Token);
	}

	#endregion

	#region MiddlewareStateMachine Tests

	[Fact]
	public async Task StateMachine_HandlesEmptyMiddlewareArray()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedResult = MessageResult.Success();

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		var middlewareContext = new MiddlewareContext(EmptyMiddlewares);

		// Act
		var result = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			CancellationToken.None);

		// Assert
		result.ShouldBe(expectedResult);
	}

	[Fact]
	public async Task StateMachine_ProgressesThroughAllMiddleware()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = new FakeMessageContext();
		var expectedResult = MessageResult.Success();
		var middlewareCallCount = 0;

		IDispatchMiddleware CreateCountingMiddleware()
		{
			var middleware = A.Fake<IDispatchMiddleware>();
			_ = A.CallTo(() => middleware.InvokeAsync(
					A<IDispatchMessage>._,
					A<IMessageContext>._,
					A<DispatchRequestDelegate>._,
					A<CancellationToken>._))
				.Invokes(async call =>
				{
					_ = Interlocked.Increment(ref middlewareCallCount);
					var next = call.GetArgument<DispatchRequestDelegate>(2);
					var msg = call.GetArgument<IDispatchMessage>(0);
					var ctx = call.GetArgument<IMessageContext>(1);
					var ct = call.GetArgument<CancellationToken>(3);
					_ = await next(msg, ctx, ct);
				})
				.ReturnsLazily(_ => new ValueTask<IMessageResult>(expectedResult));
			return middleware;
		}

		var middlewares = Enumerable.Range(0, 5).Select(_ => CreateCountingMiddleware()).ToArray();

		DispatchRequestDelegate finalDelegate = (msg, ctx, ct) =>
			new ValueTask<IMessageResult>(expectedResult);

		var middlewareContext = new MiddlewareContext(middlewares);

		// Act
		_ = await _executor.ExecuteAsync(
			ref middlewareContext,
			message,
			context,
			finalDelegate,
			CancellationToken.None);

		// Assert
		middlewareCallCount.ShouldBe(5);
	}

	#endregion

	#region Test Fixtures

	private sealed class TestDispatchAction : IDispatchAction<string>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDispatchAction";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
		public string? Data { get; set; }
	}

	private sealed class TestDispatchActionNullable : IDispatchAction<string?>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDispatchActionNullable";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestDispatchActionWithInt : IDispatchAction<int>
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? "TestDispatchActionWithInt";
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	#endregion
}
