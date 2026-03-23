// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Outbox;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="TransactionalOutboxWriter"/>.
/// Validates delegation to IOutboxStore and ambient transaction enforcement.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class TransactionalOutboxWriterShould : UnitTestBase
{
	private readonly IOutboxStore _outboxStore = A.Fake<IOutboxStore>();
	private readonly IMessageContextAccessor _contextAccessor = A.Fake<IMessageContextAccessor>();
	private readonly IMessageContext _messageContext = A.Fake<IMessageContext>();
	private readonly TransactionalOutboxWriter _sut;

	public TransactionalOutboxWriterShould()
	{
		var items = new Dictionary<string, object>();
		A.CallTo(() => _messageContext.Items).Returns(items);
		A.CallTo(() => _contextAccessor.MessageContext).Returns(_messageContext);
		_sut = new TransactionalOutboxWriter(_outboxStore, _contextAccessor);
	}

	[Fact]
	public async Task DelegateToOutboxStoreEnqueueAsync()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object(); // ambient transaction present
		using var cts = new CancellationTokenSource();

		// Act
		await _sut.WriteAsync(message, "orders-topic", cts.Token);

		// Assert
		A.CallTo(() => _outboxStore.EnqueueAsync(message, _messageContext, cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task ThrowWhenNoAmbientTransaction()
	{
		// Arrange -- no "Transaction" key in Items
		var message = A.Fake<IDispatchMessage>();

		// Act & Assert
		var ex = await Should.ThrowAsync<InvalidOperationException>(
			() => _sut.WriteAsync(message, "dest", CancellationToken.None).AsTask());
		ex.Message.ShouldContain("ambient transaction");
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
	public async Task ThrowWhenMessageIsNull()
	{
		// Arrange
		_messageContext.Items["Transaction"] = new object();

		// Act & Assert
		await Should.ThrowAsync<ArgumentNullException>(
			() => _sut.WriteAsync(null!, "dest", CancellationToken.None).AsTask());
	}

	[Fact]
	public async Task AcceptNullDestination()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();

		// Act
		await _sut.WriteAsync(message, null, CancellationToken.None);

		// Assert
		A.CallTo(() => _outboxStore.EnqueueAsync(message, _messageContext, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task NotCallOutboxStoreWhenTransactionMissing()
	{
		// Arrange -- no transaction
		var message = A.Fake<IDispatchMessage>();

		// Act
		try
		{
			await _sut.WriteAsync(message, "dest", CancellationToken.None);
		}
		catch (InvalidOperationException)
		{
			// Expected
		}

		// Assert -- store should never have been called
		A.CallTo(() => _outboxStore.EnqueueAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.MustNotHaveHappened();
	}

	[Fact]
	public async Task PropagateMessageContextToOutboxStore()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();
		_messageContext.CorrelationId = "corr-123";
		_messageContext.CausationId = "cause-456";

		// Act
		await _sut.WriteAsync(message, "dest", CancellationToken.None);

		// Assert -- the same IMessageContext instance is passed through
		A.CallTo(() => _outboxStore.EnqueueAsync(
				message,
				A<IMessageContext>.That.Matches(ctx => ctx == _messageContext),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PassCancellationTokenToOutboxStore()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();
		using var cts = new CancellationTokenSource();

		// Act
		await _sut.WriteAsync(message, "dest", cts.Token);

		// Assert
		A.CallTo(() => _outboxStore.EnqueueAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				cts.Token))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PropagateDestinationViaContextItems()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();

		// Act
		await _sut.WriteAsync(message, "orders-topic", CancellationToken.None);

		// Assert -- destination must be set in context before EnqueueAsync
		_messageContext.Items.ShouldContainKey("OutboxDestination");
		_messageContext.Items["OutboxDestination"].ShouldBe("orders-topic");
	}

	[Fact]
	public async Task NotSetDestinationWhenNull()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();

		// Act
		await _sut.WriteAsync(message, null, CancellationToken.None);

		// Assert -- null destination should not be stored
		_messageContext.Items.ShouldNotContainKey("OutboxDestination");
	}

	[Fact]
	public async Task SetDestinationBeforeCallingEnqueueAsync()
	{
		// Arrange
		var message = A.Fake<IDispatchMessage>();
		_messageContext.Items["Transaction"] = new object();
		string? capturedDestination = null;

		// Capture destination at the moment EnqueueAsync is called
		A.CallTo(() => _outboxStore.EnqueueAsync(
				A<IDispatchMessage>._,
				A<IMessageContext>._,
				A<CancellationToken>._))
			.Invokes(call =>
			{
				var ctx = call.GetArgument<IMessageContext>(1)!;
				capturedDestination = ctx.Items.TryGetValue("OutboxDestination", out var val)
					? val as string
					: null;
			});

		// Act
		await _sut.WriteAsync(message, "events-queue", CancellationToken.None);

		// Assert -- destination was present when EnqueueAsync was invoked
		capturedDestination.ShouldBe("events-queue");
	}
}
