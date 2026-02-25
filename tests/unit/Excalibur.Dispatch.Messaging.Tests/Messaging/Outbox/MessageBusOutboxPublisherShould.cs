// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Outbox;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Outbox;

[Trait("Category", "Unit")]
public sealed class MessageBusOutboxPublisherShould
{
	private readonly IOutboxStore _outboxStore;
	private readonly IPayloadSerializer _serializer;
	private readonly IMessageBusAdapter _messageBus;
	private readonly IServiceProvider _serviceProvider;
	private readonly ILogger<MessageBusOutboxPublisher> _logger;
	private readonly MessageBusOutboxPublisher _publisher;

	public MessageBusOutboxPublisherShould()
	{
		_outboxStore = A.Fake<IOutboxStore>(o => o.Implements<IOutboxStoreAdmin>());
		_serializer = A.Fake<IPayloadSerializer>();
		_messageBus = A.Fake<IMessageBusAdapter>();
		_serviceProvider = A.Fake<IServiceProvider>();
		_logger = A.Fake<ILogger<MessageBusOutboxPublisher>>();

		_publisher = new MessageBusOutboxPublisher(
			_outboxStore,
			_serializer,
			_messageBus,
			_serviceProvider,
			_logger);
	}

	[Fact]
	public async Task StageMessageInOutboxStore()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };
		var destination = "test-queue";
		var serializedPayload = new byte[] { 1, 2, 3 };

		_ = A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._)).Returns(serializedPayload);

		// Act
		var result = await _publisher.PublishAsync(message, destination, null, CancellationToken.None);

		// Assert
		_ = result.ShouldNotBeNull();
		result.Destination.ShouldBe(destination);
		result.Status.ShouldBe(OutboxStatus.Staged);
		result.Payload.ShouldBe(serializedPayload);

		_ = A.CallTo(() => _outboxStore.StageMessageAsync(
			A<OutboundMessage>.That.Matches(m => m.Destination == destination),
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task StageScheduledMessage()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };
		var destination = "test-queue";
		var scheduledAt = DateTimeOffset.UtcNow.AddHours(1);

		_ = A.CallTo(() => _serializer.SerializeObject(A<object>._, A<Type>._)).Returns(new byte[] { 1 });

		// Act
		var result = await _publisher.PublishAsync(message, destination, scheduledAt, CancellationToken.None);

		// Assert
		result.ScheduledAt.ShouldBe(scheduledAt);
	}

	[Fact]
	public async Task ThrowWhenMessageIsNull()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentNullException>(
			() => _publisher.PublishAsync(null!, "destination", null, CancellationToken.None));
	}

	[Fact]
	public async Task ThrowWhenDestinationIsNullOrEmpty()
	{
		// Arrange
		var message = new TestMessage { Content = "test" };

		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentException>(
			() => _publisher.PublishAsync(message, null!, null, CancellationToken.None));

		_ = await Should.ThrowAsync<ArgumentException>(
			() => _publisher.PublishAsync(message, "", null, CancellationToken.None));
	}

	[Fact]
	public async Task PublishPendingMessagesFromStore()
	{
		// Arrange
		var messages = new List<OutboundMessage>
		{
			new("TestMessage", new byte[] { 1 }, "queue1"),
			new("TestMessage", new byte[] { 2 }, "queue2")
		};

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(messages);

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(A.Fake<IMessageResult>());

		// Act
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(2);
		result.FailureCount.ShouldBe(0);
		result.IsSuccess.ShouldBeTrue();
	}

	[Fact]
	public async Task MarkMessagesAsSentAfterPublishing()
	{
		// Arrange
		var message = new OutboundMessage("TestMessage", new byte[] { 1 }, "queue1");

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(A.Fake<IMessageResult>());

		// Act
		_ = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		_ = A.CallTo(() => _outboxStore.MarkSentAsync(message.Id, A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task MarkMessagesAsFailedOnError()
	{
		// Arrange
		var message = new OutboundMessage("TestMessage", new byte[] { 1 }, "queue1");

		_ = A.CallTo(() => _outboxStore.GetUnsentMessagesAsync(A<int>._, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Throws(new InvalidOperationException("Bus error"));

		// Act
		var result = await _publisher.PublishPendingMessagesAsync(CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		result.IsSuccess.ShouldBeFalse();

		_ = A.CallTo(() => _outboxStore.MarkFailedAsync(
			message.Id,
			A<string>.That.Contains("Bus error"),
			A<int>._,
			A<CancellationToken>._)).MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task RetryFailedMessagesWithinMaxRetries()
	{
		// Arrange
		var message = new OutboundMessage("TestMessage", new byte[] { 1 }, "queue1")
		{
			Status = OutboxStatus.Failed,
			RetryCount = 1
		};

		_ = A.CallTo(() => ((IOutboxStoreAdmin)_outboxStore).GetFailedMessagesAsync(3, null, 100, A<CancellationToken>._))
			.Returns(new List<OutboundMessage> { message });

		_ = A.CallTo(() => _messageBus.PublishAsync(A<IDispatchMessage>._, A<IMessageContext>._, A<CancellationToken>._))
			.Returns(A.Fake<IMessageResult>());

		// Act
		var result = await _publisher.RetryFailedMessagesAsync(3, CancellationToken.None);

		// Assert
		result.SuccessCount.ShouldBe(1);
	}

	[Fact]
	public async Task ThrowWhenMaxRetriesIsNegative()
	{
		// Act & Assert
		_ = await Should.ThrowAsync<ArgumentOutOfRangeException>(
			() => _publisher.RetryFailedMessagesAsync(-1, CancellationToken.None));
	}

	[Fact]
	public void ReturnAccurateStatistics()
	{
		// Act
		var stats = _publisher.GetStatistics();

		// Assert
		_ = stats.ShouldNotBeNull();
		stats.TotalOperations.ShouldBe(0);
		stats.TotalMessagesPublished.ShouldBe(0);
		stats.TotalMessagesFailed.ShouldBe(0);
		stats.CurrentSuccessRate.ShouldBe(100.0);
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_WhenAdapterMissing_Throws()
	{
		// Arrange
		var multiStore = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>());
		var transportRegistry = new TransportRegistry();
		var publisher = new MessageBusOutboxPublisher(
			multiStore,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		// Act & Assert
		await Should.ThrowAsync<InvalidOperationException>(
			() => publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10));
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_MarksTransportSent_OnSuccess()
	{
		// Arrange
		var multiStoreBase = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>());
		var multiStore = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();

		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<CancellationToken>._))
			.Returns(Task.CompletedTask);

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka");

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		var message = new OutboundMessage("OrderCreated", [1, 2, 3], "orders-default");
		var transport = new OutboundMessageTransport(message.Id, "kafka")
		{
			Destination = "orders-topic"
		};

		_ = A.CallTo(() => multiStore.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		var result = await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		result.SuccessCount.ShouldBe(1);
		result.FailureCount.ShouldBe(0);
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, "orders-topic", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
		_ = A.CallTo(() => multiStore.MarkTransportSentAsync(message.Id, "kafka", A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	[Fact]
	public async Task PublishPendingTransportDeliveries_MarksTransportFailed_OnAdapterException()
	{
		// Arrange
		var multiStoreBase = A.Fake<IOutboxStore>(o => o.Implements<IMultiTransportOutboxStore>());
		var multiStore = multiStoreBase.ShouldBeAssignableTo<IMultiTransportOutboxStore>();

		var adapter = A.Fake<ITransportAdapter>();
		_ = A.CallTo(() => adapter.SendAsync(A<IDispatchMessage>._, A<string>._, A<CancellationToken>._))
			.ThrowsAsync(new InvalidOperationException("transport unavailable"));

		var transportRegistry = new TransportRegistry();
		transportRegistry.RegisterTransport("kafka", adapter, "Kafka");

		var publisher = new MessageBusOutboxPublisher(
			multiStoreBase,
			_serializer,
			transportRegistry,
			_serviceProvider,
			_logger);

		var message = new OutboundMessage("OrderCreated", [9, 9], "orders-default");
		var transport = new OutboundMessageTransport(message.Id, "kafka")
		{
			Destination = "orders-topic"
		};

		_ = A.CallTo(() => multiStore.GetPendingTransportDeliveriesAsync("kafka", 10, A<CancellationToken>._))
			.Returns(new[] { (message, transport) });

		// Act
		var result = await publisher.PublishPendingTransportDeliveriesAsync("kafka", CancellationToken.None, batchSize: 10);

		// Assert
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(1);
		result.Errors.ShouldContain(error => error.MessageId == message.Id);
		_ = A.CallTo(() => multiStore.MarkTransportFailedAsync(
				message.Id,
				"kafka",
				A<string>.That.Contains("transport unavailable"),
				A<CancellationToken>._))
			.MustHaveHappenedOnceExactly();
	}

	private sealed class TestMessage
	{
		public string Content { get; set; } = string.Empty;
	}
}
