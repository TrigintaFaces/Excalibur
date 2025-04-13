using Excalibur.Core.Domain.Events;
using Excalibur.Data.Outbox;
using Excalibur.Data.Outbox.Exceptions;

using FakeItEasy;

using MediatR;

using Shouldly;

namespace Excalibur.Tests.Unit.Data.Outbox;

public class MediatorOutboxMessageDispatcherShould
{
	private readonly IPublisher _fakeMediator;
	private readonly MediatorOutboxMessageDispatcher _dispatcher;

	public MediatorOutboxMessageDispatcherShould()
	{
		_fakeMediator = A.Fake<IPublisher>();
		_dispatcher = new MediatorOutboxMessageDispatcher(_fakeMediator);
	}

	[Fact]
	public void ConstructSuccessfullyWithMediator()
	{
		// Arrange & Act
		var dispatcher = new MediatorOutboxMessageDispatcher(_fakeMediator);

		// Assert
		_ = dispatcher.ShouldNotBeNull();
	}

	[Fact]
	public async Task ThrowArgumentNullExceptionWhenMessageIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(async () =>
			await _dispatcher.DispatchAsync(null!).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task ThrowInvalidOperationExceptionWhenMessageBodyIsNotINotification()
	{
		// Arrange
		var message = new OutboxMessage
		{
			MessageId = "test-id",
			MessageBody = new object() // Not an INotification
		};

		// Act & Assert
		_ = await Should.ThrowAsync<InvalidOperationException>(async () =>
			await _dispatcher.DispatchAsync(message).ConfigureAwait(true)).ConfigureAwait(true);
	}

	[Fact]
	public async Task PublishNotificationWhenMessageBodyIsINotification()
	{
		// Arrange
		var notification = new TestNotification { Id = 1, Name = "Test Notification" };

		var message = new OutboxMessage { MessageId = "test-id", MessageBody = notification };

		// Act
		await _dispatcher.DispatchAsync(message).ConfigureAwait(true);

		// Assert
		_ = A.CallTo(() => _fakeMediator.Publish(
				A<INotification>.That.Matches(n => n == notification),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task WrapExceptionInOutboxMessageDispatchExceptionWhenPublishFails()
	{
		// Arrange
		var notification = new TestNotification { Id = 1, Name = "Test Notification" };
		var message = new OutboxMessage { MessageId = "test-id", MessageBody = notification };
		var originalException = new InvalidOperationException("Test exception");

		_ = A.CallTo(() => _fakeMediator.Publish(
				A<INotification>.That.Matches(n => n == notification),
				A<CancellationToken>._))
			.Throws(originalException);

		// Act & Assert
		var exception = await Should.ThrowAsync<OutboxMessageDispatchException>(async () =>
			await _dispatcher.DispatchAsync(message).ConfigureAwait(true)).ConfigureAwait(true);

		exception.MessageId.ShouldBe(message.MessageId);
		exception.InnerException.ShouldBeSameAs(originalException);
	}

	private sealed class TestNotification : INotification, IDomainEvent
	{
		public int Id { get; set; }
		public string Name { get; set; } = string.Empty;
		public DateTimeOffset Timestamp { get; } = DateTimeOffset.UtcNow;
	}
}
