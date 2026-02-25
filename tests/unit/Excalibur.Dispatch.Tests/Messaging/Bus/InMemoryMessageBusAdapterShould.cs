// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Bus;

using Microsoft.Extensions.Logging.Abstractions;

namespace Excalibur.Dispatch.Tests.Messaging.Bus;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InMemoryMessageBusAdapterShould : IAsyncDisposable
{
	private readonly InMemoryMessageBusAdapter _adapter;

	public InMemoryMessageBusAdapterShould()
	{
		_adapter = new InMemoryMessageBusAdapter(NullLogger<InMemoryMessageBusAdapter>.Instance);
	}

	public async ValueTask DisposeAsync()
	{
		await _adapter.DisposeAsync();
	}

	[Fact]
	public void Name_ReturnsInMemory()
	{
		_adapter.Name.ShouldBe("InMemory");
	}

	[Fact]
	public void SupportsPublishing_ReturnsTrue()
	{
		_adapter.SupportsPublishing.ShouldBeTrue();
	}

	[Fact]
	public void SupportsSubscription_ReturnsTrue()
	{
		_adapter.SupportsSubscription.ShouldBeTrue();
	}

	[Fact]
	public void SupportsTransactions_ReturnsFalse()
	{
		_adapter.SupportsTransactions.ShouldBeFalse();
	}

	[Fact]
	public void IsConnected_InitiallyFalse()
	{
		_adapter.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task InitializeAsync_SetsIsConnected()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();

		// Act
		await _adapter.InitializeAsync(options, CancellationToken.None);

		// Assert
		_adapter.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task PublishAsync_WhenNotConnected_ReturnsFailedResult()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();

		// Act
		var result = await _adapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldNotBeNull();
	}

	[Fact]
	public async Task PublishAsync_WhenConnected_ReturnsSuccess()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		var message = A.Fake<IDispatchMessage>();
		var context = A.Fake<IMessageContext>();
		A.CallTo(() => context.MessageId).Returns("test-123");

		// Act
		var result = await _adapter.PublishAsync(message, context, CancellationToken.None);

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public async Task PublishAsync_WithNullMessage_Throws()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _adapter.PublishAsync(null!, A.Fake<IMessageContext>(), CancellationToken.None));
	}

	[Fact]
	public async Task PublishAsync_WithNullContext_Throws()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _adapter.PublishAsync(A.Fake<IDispatchMessage>(), null!, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsync_WhenNotConnected_Throws()
	{
		// Arrange
		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _adapter.SubscribeAsync("test-sub", handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsync_WhenConnected_Succeeds()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act & Assert - should not throw
		await _adapter.SubscribeAsync("test-sub", handler, null, CancellationToken.None);
	}

	[Fact]
	public async Task SubscribeAsync_WithNullName_Throws()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());

		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _adapter.SubscribeAsync(null!, handler, null, CancellationToken.None));
	}

	[Fact]
	public async Task SubscribeAsync_WithNullHandler_Throws()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _adapter.SubscribeAsync("test-sub", null!, null, CancellationToken.None));
	}

	[Fact]
	public async Task UnsubscribeAsync_Succeeds()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		Func<IDispatchMessage, IMessageContext, CancellationToken, Task<IMessageResult>> handler =
			(_, _, _) => Task.FromResult<IMessageResult>(MessageResult.Success());
		await _adapter.SubscribeAsync("test-sub", handler, null, CancellationToken.None);

		// Act & Assert - should not throw
		await _adapter.UnsubscribeAsync("test-sub", CancellationToken.None);
	}

	[Fact]
	public async Task UnsubscribeAsync_NonExistent_DoesNotThrow()
	{
		// Act & Assert
		await _adapter.UnsubscribeAsync("non-existent", CancellationToken.None);
	}

	[Fact]
	public async Task UnsubscribeAsync_WithNullName_Throws()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentException>(async () =>
			await _adapter.UnsubscribeAsync(null!, CancellationToken.None));
	}

	[Fact]
	public async Task CheckHealthAsync_WhenNotConnected_ReturnsUnhealthy()
	{
		// Act
		var result = await _adapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeFalse();
	}

	[Fact]
	public async Task CheckHealthAsync_WhenConnected_ReturnsHealthy()
	{
		// Arrange
		var options = A.Fake<IMessageBusOptions>();
		await _adapter.InitializeAsync(options, CancellationToken.None);

		// Act
		var result = await _adapter.CheckHealthAsync(CancellationToken.None);

		// Assert
		result.IsHealthy.ShouldBeTrue();
		result.Data.ShouldNotBeNull();
	}

	[Fact]
	public async Task StartAsync_SetsIsConnected()
	{
		// Act
		await _adapter.StartAsync(CancellationToken.None);

		// Assert
		_adapter.IsConnected.ShouldBeTrue();
	}

	[Fact]
	public async Task StopAsync_ClearsIsConnected()
	{
		// Arrange
		await _adapter.StartAsync(CancellationToken.None);

		// Act
		await _adapter.StopAsync(CancellationToken.None);

		// Assert
		_adapter.IsConnected.ShouldBeFalse();
	}

	[Fact]
	public async Task DisposeAsync_DoesNotThrow()
	{
		// Arrange
		await _adapter.StartAsync(CancellationToken.None);

		// Act & Assert - should not throw
		await _adapter.DisposeAsync();
	}

	[Fact]
	public async Task DisposeAsync_MultipleCalls_DoesNotThrow()
	{
		// Act & Assert - double dispose should be safe
		await _adapter.DisposeAsync();
		await _adapter.DisposeAsync();
	}

	[Fact]
	public async Task Dispatch_PublishedMessage_To_SubscribedHandler()
	{
		await _adapter.StartAsync(CancellationToken.None);

		var dispatched = new TaskCompletionSource<(IDispatchMessage Message, IMessageContext Context)>(
			TaskCreationOptions.RunContinuationsAsynchronously);

		await _adapter.SubscribeAsync(
			"orders-subscription",
			(message, context, _) =>
			{
				dispatched.TrySetResult((message, context));
				return Task.FromResult<IMessageResult>(MessageResult.Success());
			},
			null,
			CancellationToken.None);

		var message = new TestDispatchMessage("order-created");
		var context = new Excalibur.Dispatch.Messaging.MessageContext(message, A.Fake<IServiceProvider>())
		{
			MessageId = "msg-1001",
		};

		var publishResult = await _adapter.PublishAsync(message, context, CancellationToken.None);
		publishResult.Succeeded.ShouldBeTrue();

		var deliveryObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => dispatched.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		deliveryObserved.ShouldBeTrue("message should be delivered to subscribed handler");
		var delivered = await dispatched.Task;
		delivered.Message.ShouldBeSameAs(message);
		delivered.Context.MessageId.ShouldBe("msg-1001");
	}

	[Fact]
	public async Task Continue_Dispatch_When_One_Handler_Throws()
	{
		await _adapter.StartAsync(CancellationToken.None);

		var throwingHandlerCalls = 0;
		var successHandlerCalls = 0;

		await _adapter.SubscribeAsync(
			"throwing-handler",
			(_, _, _) =>
			{
				Interlocked.Increment(ref throwingHandlerCalls);
				throw new InvalidOperationException("handler failure");
			},
			null,
			CancellationToken.None);

		await _adapter.SubscribeAsync(
			"successful-handler",
			(_, _, _) =>
			{
				Interlocked.Increment(ref successHandlerCalls);
				return Task.FromResult<IMessageResult>(MessageResult.Success());
			},
			null,
			CancellationToken.None);

		var message = new TestDispatchMessage("payment-captured");
		var context = new Excalibur.Dispatch.Messaging.MessageContext(message, A.Fake<IServiceProvider>());

		var publishResult = await _adapter.PublishAsync(message, context, CancellationToken.None);
		publishResult.Succeeded.ShouldBeTrue();

		var handlersInvoked = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => Volatile.Read(ref throwingHandlerCalls) >= 1 && Volatile.Read(ref successHandlerCalls) >= 1,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
		handlersInvoked.ShouldBeTrue("both handlers should be invoked even when one throws");

		throwingHandlerCalls.ShouldBe(1);
		successHandlerCalls.ShouldBe(1);
	}

	[Fact]
	public async Task Populate_Context_Defaults_Before_Dispatch()
	{
		await _adapter.StartAsync(CancellationToken.None);

		var dispatched = new TaskCompletionSource<IMessageContext>(TaskCreationOptions.RunContinuationsAsynchronously);

		await _adapter.SubscribeAsync(
			"context-check",
			(_, context, _) =>
			{
				dispatched.TrySetResult(context);
				return Task.FromResult<IMessageResult>(MessageResult.Success());
			},
			null,
			CancellationToken.None);

		var message = new TestDispatchMessage("shipment-ready");
		var context = new Excalibur.Dispatch.Messaging.MessageContext(message, A.Fake<IServiceProvider>())
		{
			MessageId = null,
			MessageType = null
		};

		var publishResult = await _adapter.PublishAsync(message, context, CancellationToken.None);
		publishResult.Succeeded.ShouldBeTrue();

		var dispatchObserved = await global::Tests.Shared.Infrastructure.WaitHelpers.WaitUntilAsync(
			() => dispatched.Task.IsCompleted,
			TimeSpan.FromSeconds(10),
			TimeSpan.FromMilliseconds(20));
		dispatchObserved.ShouldBeTrue("context-check handler should run");
		var dispatchedContext = await dispatched.Task;
		dispatchedContext.Message.ShouldBeSameAs(message);
		dispatchedContext.MessageId.ShouldNotBeNullOrWhiteSpace();
		dispatchedContext.MessageType.ShouldBe(typeof(TestDispatchMessage).FullName);
		dispatchedContext.ReceivedTimestampUtc.ShouldNotBe(default);
	}

	[Fact]
	public void Dispose_DoesNotThrow()
	{
		// Act & Assert - IDisposable.Dispose should not throw
		_adapter.Dispose();
	}

	[Fact]
	public void Dispose_MultipleCalls_DoesNotThrow()
	{
		// Act & Assert
		_adapter.Dispose();
		_adapter.Dispose();
	}

	[Fact]
	public void Constructor_WithNullLogger_Throws()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new InMemoryMessageBusAdapter(null!));
	}

	private sealed record TestDispatchMessage(string Name) : IDispatchMessage;
}
