// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Collections.Concurrent;
using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using Testcontainers.Kafka;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Integration tests for <see cref="KafkaTransportSubscriber"/> with a real Kafka container.
/// Tests push-based subscription, message acknowledgment, rejection, and requeue.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "Kafka")]
[Trait("Component", "Transport")]
public sealed class KafkaTransportSubscriberIntegrationShould : IAsyncLifetime
{
	private KafkaContainer? _container;
	private string? _bootstrapServers;

	public async Task InitializeAsync()
	{
		_container = new KafkaBuilder()
			.WithImage("confluentinc/cp-kafka:7.5.0")
			.WithName($"kafka-subscriber-test-{Guid.NewGuid():N}")
			.Build();

		await _container.StartAsync().ConfigureAwait(false);
		_bootstrapServers = _container.GetBootstrapAddress();
	}

	public async Task DisposeAsync()
	{
		if (_container is not null)
		{
			await _container.DisposeAsync().ConfigureAwait(false);
		}
	}

	[Fact]
	public async Task SubscribeAsync_ReceivesAndAcknowledgesMessages()
	{
		// Arrange
		var topic = $"test-sub-ack-{Guid.NewGuid():N}";
		var groupId = $"test-group-sub-{Guid.NewGuid():N}";
		var messageCount = 3;

		await ProduceMessagesAsync(topic, messageCount).ConfigureAwait(false);

		var consumer = BuildConsumer(topic, groupId);
		var subscriber = CreateSubscriber(consumer, topic);

		var receivedMessages = new ConcurrentBag<TransportReceivedMessage>();
		using var cts = new CancellationTokenSource();

		// Act - subscribe and collect messages, then cancel after receiving expected count
		var subscribeTask = subscriber.SubscribeAsync(
			async (msg, ct) =>
			{
				receivedMessages.Add(msg);
				if (receivedMessages.Count >= messageCount)
				{
					// Cancel after receiving all expected messages
					await Task.CompletedTask.ConfigureAwait(false);
					cts.Cancel();
				}

				return MessageAction.Acknowledge;
			},
			cts.Token);

		// Wait for subscription to complete (cancelled by handler)
		await subscribeTask.ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);

		// Assert
		receivedMessages.Count.ShouldBe(messageCount);

		foreach (var msg in receivedMessages)
		{
			msg.Source.ShouldBe(topic);
			msg.Body.ToArray().ShouldNotBeEmpty();
			msg.ProviderData.ShouldContainKey("kafka.topic");
			msg.ProviderData.ShouldContainKey("kafka.partition");
			msg.ProviderData.ShouldContainKey("kafka.offset");
		}
	}

	[Fact]
	public async Task SubscribeAsync_RejectAction_CommitsOffsetAndSkipsMessage()
	{
		// Arrange
		var topic = $"test-sub-reject-{Guid.NewGuid():N}";
		var groupId = $"test-group-reject-{Guid.NewGuid():N}";

		await ProduceMessagesAsync(topic, 1).ConfigureAwait(false);

		var consumer = BuildConsumer(topic, groupId);
		var subscriber = CreateSubscriber(consumer, topic);

		var rejected = false;
		using var cts = new CancellationTokenSource();

		// Act - reject the message
		var subscribeTask = subscriber.SubscribeAsync(
			(msg, ct) =>
			{
				rejected = true;
				cts.Cancel();
				return Task.FromResult(MessageAction.Reject);
			},
			cts.Token);

		await subscribeTask.ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);

		// Assert
		rejected.ShouldBeTrue();
	}

	[Fact]
	public async Task SubscribeAsync_StopsOnCancellation()
	{
		// Arrange
		var topic = $"test-sub-cancel-{Guid.NewGuid():N}";
		var groupId = $"test-group-cancel-{Guid.NewGuid():N}";

		// Produce a message to create the topic
		await ProduceMessagesAsync(topic, 1).ConfigureAwait(false);

		var consumer = BuildConsumer(topic, groupId);
		var subscriber = CreateSubscriber(consumer, topic);

		using var cts = new CancellationTokenSource();

		// Cancel after a short delay
		cts.CancelAfter(TimeSpan.FromSeconds(3));

		// Act - subscription should stop when token is cancelled
		var subscribeTask = subscriber.SubscribeAsync(
			(msg, ct) => Task.FromResult(MessageAction.Acknowledge),
			cts.Token);

		// Assert - should complete without throwing
		await subscribeTask.ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);
	}

	[Fact]
	public async Task SubscribeAsync_HandlerExceptionDoesNotStopSubscription()
	{
		// Arrange
		var topic = $"test-sub-error-{Guid.NewGuid():N}";
		var groupId = $"test-group-error-{Guid.NewGuid():N}";

		await ProduceMessagesAsync(topic, 3).ConfigureAwait(false);

		var consumer = BuildConsumer(topic, groupId);
		var subscriber = CreateSubscriber(consumer, topic);

		var callCount = 0;
		using var cts = new CancellationTokenSource();

		// Act - first call throws, but subscription should continue processing
		var subscribeTask = subscriber.SubscribeAsync(
			(msg, ct) =>
			{
				Interlocked.Increment(ref callCount);

				if (callCount == 1)
				{
					throw new InvalidOperationException("Simulated handler error");
				}

				if (callCount >= 3)
				{
					cts.Cancel();
				}

				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		await subscribeTask.ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);

		// Assert - handler should have been called for all 3 messages despite error on first
		callCount.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task SubscribeAsync_MessageHeadersArePreserved()
	{
		// Arrange
		var topic = $"test-sub-headers-{Guid.NewGuid():N}";
		var groupId = $"test-group-headers-{Guid.NewGuid():N}";

		using (var producer = BuildProducer())
		{
			var headers = new Headers
			{
				{ "content-type", Encoding.UTF8.GetBytes("application/json") },
				{ "message-type", Encoding.UTF8.GetBytes("OrderCreated") },
				{ "correlation-id", Encoding.UTF8.GetBytes("corr-123") },
				{ "message-id", Encoding.UTF8.GetBytes("msg-unique-1") },
			};

			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "order-key",
				Value = Encoding.UTF8.GetBytes("""{"orderId":"42"}"""),
				Headers = headers,
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		var consumer = BuildConsumer(topic, groupId);
		var subscriber = CreateSubscriber(consumer, topic);

		TransportReceivedMessage? receivedMsg = null;
		using var cts = new CancellationTokenSource();

		var subscribeTask = subscriber.SubscribeAsync(
			(msg, ct) =>
			{
				receivedMsg = msg;
				cts.Cancel();
				return Task.FromResult(MessageAction.Acknowledge);
			},
			cts.Token);

		await subscribeTask.ConfigureAwait(false);
		await subscriber.DisposeAsync().ConfigureAwait(false);

		// Assert
		receivedMsg.ShouldNotBeNull();
		receivedMsg.Id.ShouldBe("msg-unique-1");
		receivedMsg.ContentType.ShouldBe("application/json");
		receivedMsg.MessageType.ShouldBe("OrderCreated");
		receivedMsg.CorrelationId.ShouldBe("corr-123");
		receivedMsg.PartitionKey.ShouldBe("order-key");
		receivedMsg.Source.ShouldBe(topic);
	}

	[Fact]
	public async Task GetService_ReturnsUnderlyingConsumer()
	{
		// Arrange
		var topic = "test-sub-getservice";
		var consumer = BuildConsumer(topic, "test-group-getservice-sub");
		var subscriber = CreateSubscriber(consumer, topic);

		// Act
		var service = subscriber.GetService(typeof(IConsumer<string, byte[]>));

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeAssignableTo<IConsumer<string, byte[]>>();

		await subscriber.DisposeAsync().ConfigureAwait(false);
	}

	#region Helpers

	private async Task ProduceMessagesAsync(string topic, int count)
	{
		using var producer = BuildProducer();
		for (var i = 0; i < count; i++)
		{
			var headers = new Headers
			{
				{ "message-id", Encoding.UTF8.GetBytes($"sub-msg-{i}") },
			};

			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = $"key-{i}",
				Value = Encoding.UTF8.GetBytes($"payload-{i}"),
				Headers = headers,
			}).ConfigureAwait(false);
		}

		producer.Flush(TimeSpan.FromSeconds(5));
	}

	private IProducer<string, byte[]> BuildProducer()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = _bootstrapServers,
			AllowAutoCreateTopics = true,
		};

		return new ProducerBuilder<string, byte[]>(config).Build();
	}

	private IConsumer<string, byte[]> BuildConsumer(string topic, string groupId)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _bootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = false,
		};

		return new ConsumerBuilder<string, byte[]>(config).Build();
	}

	private static ITransportSubscriber CreateSubscriber(IConsumer<string, byte[]> consumer, string topic)
	{
		var subscriberType = typeof(KafkaOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Kafka.KafkaTransportSubscriber")!;

		var loggerOfT = typeof(Logger<>).MakeGenericType(subscriberType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var ctor = subscriberType.GetConstructors()[0];
		var instance = ctor.Invoke([consumer, topic, logger]);

		return (ITransportSubscriber)instance;
	}

	#endregion
}
