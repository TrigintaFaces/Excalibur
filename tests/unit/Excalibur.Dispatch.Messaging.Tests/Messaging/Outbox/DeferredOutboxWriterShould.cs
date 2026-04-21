// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
using Excalibur.Dispatch.Middleware.Outbox;

namespace Excalibur.Dispatch.Messaging.Tests.Messaging.Outbox;

/// <summary>
/// Unit tests for <see cref="DeferredOutboxWriter"/>.
/// Validates buffering behavior, error paths, and scheduled delivery.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DeferredOutboxWriterShould : UnitTestBase
{
	private readonly IMessageContextAccessor _contextAccessor = A.Fake<IMessageContextAccessor>();
	private readonly IMessageContext _messageContext = A.Fake<IMessageContext>();
	private readonly DeferredOutboxWriter _sut;

	public DeferredOutboxWriterShould()
	{
		// Set up Items dictionary so GetItem<T> extension works
		var items = new Dictionary<string, object>();
		A.CallTo(() => _messageContext.Items).Returns(items);
		A.CallTo(() => _contextAccessor.MessageContext).Returns(_messageContext);
		_sut = new DeferredOutboxWriter(_contextAccessor);
	}

	[Fact]
	public async Task BufferMessageInOutboxContext()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext("corr-1", "cause-1", "tenant-1", "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;

		// Act
		await _sut.WriteAsync(message, "orders-topic", CancellationToken.None);

		// Assert
		outboxContext.OutboundMessages.Count.ShouldBe(1);
		outboxContext.OutboundMessages[0].Message.ShouldBe(message);
		outboxContext.OutboundMessages[0].Destination.ShouldBe("orders-topic");
		outboxContext.OutboundMessages[0].ScheduledAt.ShouldBeNull();
	}

	[Fact]
	public async Task BufferMultipleMessagesInOutboxContext()
	{
		// Arrange
		var message1 = A.Fake<IDispatchMessage>();
		var message2 = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext("corr-1", null, null, "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;

		// Act
		await _sut.WriteAsync(message1, "topic-a", CancellationToken.None);
		await _sut.WriteAsync(message2, "topic-b", CancellationToken.None);

		// Assert
		outboxContext.OutboundMessages.Count.ShouldBe(2);
		outboxContext.OutboundMessages[0].Destination.ShouldBe("topic-a");
		outboxContext.OutboundMessages[1].Destination.ShouldBe("topic-b");
	}

	[Fact]
	public async Task AcceptNullDestination()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext(null, null, null, "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;

		// Act
		await _sut.WriteAsync(message, null, CancellationToken.None);

		// Assert
		outboxContext.OutboundMessages.Count.ShouldBe(1);
		outboxContext.OutboundMessages[0].Destination.ShouldBeNull();
	}

	[Fact]
	public async Task ThrowWhenNoActiveMessageContext()
	{
		// Arrange
		A.CallTo(() => _contextAccessor.MessageContext).Returns(null);
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.WriteAsync(message, "dest", CancellationToken.None).AsTask());
		ex.Message.ShouldContain("active message context");
	}

	[Fact]
	public async Task ThrowWhenNoOutboxContextInItems()
	{
		// Arrange -- MessageContext exists but no OutboxContext key
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.WriteAsync(message, "dest", CancellationToken.None).AsTask());
		ex.Message.ShouldContain("OutboxContext not found");
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.WriteAsync(null!, "dest", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task ReturnCompletedValueTask()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext(null, null, null, "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;

		// Act
		var task = _sut.WriteAsync(message, null, CancellationToken.None);

		// Assert -- DeferredOutboxWriter is synchronous
		task.IsCompletedSuccessfully.ShouldBeTrue();
		await task;
	}

	[Fact]
	public async Task BufferScheduledDeliveryViaWriteScheduledAsync()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext(null, null, null, "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;
		var scheduledAt = new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);

		// Act -- use the public WriteScheduledAsync extension which sets the scope
		await _sut.WriteScheduledAsync(message, "dest", scheduledAt, CancellationToken.None);

		// Assert
		outboxContext.OutboundMessages.Count.ShouldBe(1);
		outboxContext.OutboundMessages[0].ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task NotSetScheduledAtForPlainWriteAsync()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		var outboxContext = new OutboxContext(null, null, null, "TestMessage");
		_messageContext.Items["OutboxContext"] = outboxContext;

		// Act -- plain WriteAsync without scheduled delivery scope
		await _sut.WriteAsync(message, "dest", CancellationToken.None);

		// Assert
		outboxContext.OutboundMessages[0].ScheduledAt.ShouldBeNull();
	}
}
