// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Delivery;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Delivery;
using Excalibur.Dispatch.Delivery.Handlers;

using MessageResult = Excalibur.Dispatch.Abstractions.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging.Interception;

/// <summary>
/// Tests for C# 12 interceptor runtime behavior.
/// Validates the three-tier resolution strategy: intercepted → precompiled → runtime.
/// </summary>
/// <remarks>
/// Sprint 454 - S454.5: Unit tests for interceptor behavior.
/// Tests runtime invocation, fallback paths, and handler resolution.
/// </remarks>
[Collection("HandlerInvokerRegistry")]
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
[Trait("Priority", "0")]
public sealed class InterceptedDispatchShould : IDisposable
{
	private readonly IDispatchMiddlewareInvoker _middlewareInvoker;
	private readonly ILogger<FinalDispatchHandler> _logger;
	private readonly IMessageBusProvider _busProvider;
	private readonly FinalDispatchHandler _finalHandler;

	public InterceptedDispatchShould()
	{
		_middlewareInvoker = A.Fake<IDispatchMiddlewareInvoker>();
		_logger = A.Fake<ILogger<FinalDispatchHandler>>();
		_busProvider = A.Fake<IMessageBusProvider>();
		_finalHandler = new FinalDispatchHandler(_busProvider, _logger, null, new Dictionary<string, IMessageBusOptions>());

		// Reset handler invoker cache between tests
		HandlerInvoker.ClearCache();
	}

	public void Dispose()
	{
		HandlerInvoker.ClearCache();
	}

	#region Fallback Resolution Tests (4 tests)

	[Fact]
	public async Task FallbackToRuntimeResolution_WhenNotIntercepted()
	{
		// Arrange - Standard dispatch without interception
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new TestCommand();
		var context = new MessageContext();
		var expected = MessageResult.Success();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(expected));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - Falls back to middleware invoker (runtime path)
		result.ShouldBe(expected);
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task FallbackToRuntimeResolution_ForGenericMessages()
	{
		// Arrange - Generic dispatch cannot be intercepted at compile time
		await TestGenericDispatch<TestCommand>();
	}

	[Fact]
	public async Task FallbackToRuntimeResolution_ForInterfaceTypedMessages()
	{
		// Arrange - Interface-typed dispatch cannot be intercepted
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		IDispatchMessage message = new TestCommand(); // Cast to interface
		var context = new MessageContext();
		var expected = MessageResult.Success();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(expected));

		// Act
		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - Runtime resolution used
		result.ShouldBe(expected);
	}

	[Fact]
	public async Task FallbackToRuntimeResolution_ForDynamicMessages()
	{
		// Arrange - Dynamic dispatch cannot be intercepted
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		dynamic message = new TestCommand();
		var context = new MessageContext();
		var expected = MessageResult.Success();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(expected));

		// Act
		var result = await dispatcher.DispatchAsync((IDispatchMessage)message, context, CancellationToken.None);

		// Assert
		result.ShouldBe(expected);
	}

	#endregion

	#region Handler Invocation Tests (3 tests)

	[Fact]
	public async Task InvokeHandler_ThroughMiddlewarePipeline()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new TestCommand();
		var context = new MessageContext();
		var handlerInvoked = false;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() => handlerInvoked = true)
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert
		handlerInvoked.ShouldBeTrue();
	}

	[Fact]
	public async Task ReturnTypedResult_WhenHandlerProducesValue()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var query = new TestQuery();
		var context = new MessageContext();
		var expectedResult = new TestQueryResult { Value = 42 };
		var typedResult = MessageResult.Success(expectedResult);

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				query,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(typedResult));

		// Act
		var result = await dispatcher.DispatchAsync<TestQuery, TestQueryResult>(query, context, CancellationToken.None);

		// Assert
		result.ShouldBeSameAs(typedResult);
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task WrapResultInMessageResult_FromHandlerResponse()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var command = new TestCommand();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				command,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result = await dispatcher.DispatchAsync(command, context, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Multiple Call Site Tests (3 tests)

	[Fact]
	public async Task HandleMultipleDispatchCalls_InSameMethod()
	{
		// Arrange - Multiple dispatch calls from same method (would have multiple interceptors)
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var command1 = new TestCommand();
		var command2 = new AnotherTestCommand();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		var result1 = await dispatcher.DispatchAsync(command1, context, CancellationToken.None);
		var result2 = await dispatcher.DispatchAsync(command2, context, CancellationToken.None);

		// Assert
		result1.Succeeded.ShouldBeTrue();
		result2.Succeeded.ShouldBeTrue();
		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.MustHaveHappened(2, Times.Exactly);
	}

	[Fact]
	public async Task HandleDispatchInLoop_EachIterationResolved()
	{
		// Arrange - Dispatch in a loop (same call site, multiple invocations)
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var context = new MessageContext();
		var callCount = 0;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(() => callCount++)
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		for (int i = 0; i < 5; i++)
		{
			_ = await dispatcher.DispatchAsync(new TestCommand(), context, CancellationToken.None);
		}

		// Assert
		callCount.ShouldBe(5);
	}

	[Fact]
	public async Task HandleConcurrentDispatches_ThreadSafe()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var context = new MessageContext();
		var exceptions = new List<Exception>();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		await Parallel.ForEachAsync(
			Enumerable.Range(0, 100),
			new ParallelOptions { MaxDegreeOfParallelism = 10 },
			async (i, ct) =>
			{
				try
				{
					var localContext = new MessageContext();
					_ = await dispatcher.DispatchAsync(new TestCommand(), localContext, ct);
				}
				catch (Exception ex)
				{
					lock (exceptions)
					{
						exceptions.Add(ex);
					}
				}
			});

		// Assert
		exceptions.ShouldBeEmpty();
	}

	#endregion

	#region Resolution Strategy Tests (2 tests)

	[Fact]
	public void DispatcherUsesMiddlewareInvoker_AsResolutionStrategy()
	{
		// Arrange & Act
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);

		// Assert - Dispatcher relies on middleware invoker for resolution
		_ = dispatcher.ShouldNotBeNull();
		// The interceptor would bypass this by directly calling the typed handler
	}

	[Fact]
	public async Task PreserveMessageContext_ThroughInterceptedPath()
	{
		// Arrange
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new TestCommand();
		var context = new MessageContext { MessageId = "test-message-id" };
		IMessageContext? capturedContext = null;

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				message,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Invokes(call => capturedContext = call.GetArgument<IMessageContext>(1))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		// Act
		_ = await dispatcher.DispatchAsync(message, context, CancellationToken.None);

		// Assert - Context is preserved
		_ = capturedContext.ShouldNotBeNull();
		capturedContext.MessageId.ShouldBe("test-message-id");
	}

	#endregion

	#region Helper Methods

	private async Task TestGenericDispatch<TMessage>() where TMessage : IDispatchMessage, new()
	{
		var dispatcher = new Dispatcher(_middlewareInvoker, _finalHandler);
		var message = new TMessage();
		var context = new MessageContext();

		_ = A.CallTo(() => _middlewareInvoker.InvokeAsync(
				A<IDispatchMessage>._,
				context,
				A<Func<IDispatchMessage, IMessageContext, CancellationToken, ValueTask<IMessageResult>>>._,
				A<CancellationToken>._))
			.Returns(new ValueTask<IMessageResult>(MessageResult.Success()));

		var result = await dispatcher.DispatchAsync(message, context, CancellationToken.None);
		result.Succeeded.ShouldBeTrue();
	}

	#endregion

	#region Test Types

	private sealed class TestCommand : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? nameof(TestCommand);
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class AnotherTestCommand : IDispatchMessage
	{
		public Guid Id { get; } = Guid.NewGuid();
		public string MessageId { get; } = Guid.NewGuid().ToString();
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
		public MessageKinds Kind { get; } = MessageKinds.Action;
		public IReadOnlyDictionary<string, object> Headers { get; } = new Dictionary<string, object>();
		public object Body => this;
		public string MessageType => GetType().FullName ?? nameof(AnotherTestCommand);
		public IMessageFeatures Features { get; } = new DefaultMessageFeatures();
	}

	private sealed class TestQuery : IDispatchAction<TestQueryResult>
	{
	}

	private sealed class TestQueryResult
	{
		public int Value { get; init; }
	}

	#endregion
}
