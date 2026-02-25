// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Integration tests for <see cref="KafkaTransportReceiver"/> with a real Kafka container.
/// Uses a shared <see cref="KafkaContainerFixture"/> via xUnit collection to avoid
/// starting a new container per test (which overwhelms Docker).
/// </summary>
[Collection(ContainerCollections.Kafka)]
[Trait("Category", "Integration")]
[Trait("Provider", "Kafka")]
[Trait("Component", "Transport")]
public sealed class KafkaTransportReceiverIntegrationShould
{
	/// <summary>
	/// Maximum time to wait for partition assignment and message delivery.
	/// </summary>
	private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(30);

	private readonly KafkaContainerFixture _fixture;

	public KafkaTransportReceiverIntegrationShould(KafkaContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task ReceiveMessages_FromPopulatedTopic()
	{
		// Arrange
		var topic = $"test-receive-{Guid.NewGuid():N}";
		var groupId = $"test-group-{Guid.NewGuid():N}";

		// Produce messages with raw Confluent.Kafka producer
		var messageCount = 3;
		using (var producer = BuildProducer())
		{
			for (var i = 0; i < messageCount; i++)
			{
				var headers = new Headers
				{
					{ "content-type", Encoding.UTF8.GetBytes("application/json") },
					{ "message-type", Encoding.UTF8.GetBytes("TestEvent") },
					{ "correlation-id", Encoding.UTF8.GetBytes($"corr-{i}") },
					{ "message-id", Encoding.UTF8.GetBytes($"msg-{i}") },
				};

				await producer.ProduceAsync(topic, new Message<string, byte[]>
				{
					Key = $"key-{i}",
					Value = Encoding.UTF8.GetBytes($"{{\"index\":{i}}}"),
					Headers = headers,
				}).ConfigureAwait(false);
			}

			producer.Flush(TimeSpan.FromSeconds(5));
		}

		// Act - receive via our receiver with polling for partition assignment
		var consumer = BuildConsumer(topic, groupId);
		await using var receiver = CreateReceiver(consumer, topic);

		var received = await ReceiveWithRetryAsync(receiver, messageCount, ReceiveTimeout).ConfigureAwait(false);

		// Assert
		received.Count.ShouldBe(messageCount);

		for (var i = 0; i < messageCount; i++)
		{
			var msg = received[i];
			msg.Id.ShouldBe($"msg-{i}");
			msg.ContentType.ShouldBe("application/json");
			msg.MessageType.ShouldBe("TestEvent");
			msg.CorrelationId.ShouldBe($"corr-{i}");
			msg.Source.ShouldBe(topic);
			msg.PartitionKey.ShouldBe($"key-{i}");
			msg.Body.ToArray().ShouldNotBeEmpty();

			// Verify provider data
			msg.ProviderData.ShouldContainKey("kafka.topic");
			msg.ProviderData.ShouldContainKey("kafka.partition");
			msg.ProviderData.ShouldContainKey("kafka.offset");
			msg.ProviderData.ShouldContainKey("kafka.receipt_handle");
		}
	}

	[Fact]
	public async Task ReceiveFromEmptyTopic_ReturnsEmptyList()
	{
		// Arrange - create a topic, then consume from "latest" offset so no messages are visible
		var topic = $"test-empty-{Guid.NewGuid():N}";
		var groupId = $"test-group-empty-{Guid.NewGuid():N}";

		// Create topic by producing a message
		using (var producer = BuildProducer())
		{
			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "setup",
				Value = Encoding.UTF8.GetBytes("setup"),
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		// Consume the setup message with a throwaway consumer to ensure topic is stable
		using (var setupConsumer = BuildConsumerRaw(topic, $"setup-{Guid.NewGuid():N}", AutoOffsetReset.Earliest))
		{
			setupConsumer.Subscribe(topic);
			setupConsumer.Consume(TimeSpan.FromSeconds(10));
		}

		// Create a new consumer starting from "latest" - existing messages are already past
		var consumer = BuildConsumerRaw(topic, groupId, AutoOffsetReset.Latest);
		consumer.Subscribe(topic);
		await using var receiver = CreateReceiver(consumer, topic);

		// Act - try to receive; topic exists but has no new messages for this consumer group
		var received = await receiver.ReceiveAsync(10, CancellationToken.None).ConfigureAwait(false);

		// Assert
		received.Count.ShouldBe(0);
	}

	[Fact]
	public async Task ReceiveRespectsCancellationToken()
	{
		// Arrange
		var topic = $"test-cancel-{Guid.NewGuid():N}";
		var groupId = $"test-group-cancel-{Guid.NewGuid():N}";

		// Create topic by producing a message
		using (var producer = BuildProducer())
		{
			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "setup",
				Value = Encoding.UTF8.GetBytes("setup"),
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		var consumer = BuildConsumerRaw(topic, groupId, AutoOffsetReset.Latest);
		consumer.Subscribe(topic);
		await using var receiver = CreateReceiver(consumer, topic);

		using var cts = new CancellationTokenSource();
		cts.Cancel(); // Cancel immediately

		// Act - receive with already-cancelled token should return quickly with empty results
		// The receiver's loop checks cancellationToken.IsCancellationRequested between polls
		var received = await receiver.ReceiveAsync(10, cts.Token).ConfigureAwait(false);

		// Assert - should return without throwing (the receiver checks cancellation gracefully)
		received.Count.ShouldBe(0);
	}

	[Fact]
	public async Task AcknowledgeMessage_CommitsOffset()
	{
		// Arrange
		var topic = $"test-ack-{Guid.NewGuid():N}";
		var groupId = $"test-group-ack-{Guid.NewGuid():N}";

		using (var producer = BuildProducer())
		{
			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "ack-key",
				Value = Encoding.UTF8.GetBytes("ack-payload"),
				Headers = new Headers { { "message-id", Encoding.UTF8.GetBytes("ack-msg-1") } },
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		var consumer = BuildConsumer(topic, groupId);
		await using var receiver = CreateReceiver(consumer, topic);

		// Poll until we receive the message (partition assignment may take time)
		var received = await ReceiveWithRetryAsync(receiver, 1, ReceiveTimeout).ConfigureAwait(false);
		received.Count.ShouldBe(1);

		// Act - acknowledge the message (commits the offset)
		await receiver.AcknowledgeAsync(received[0], CancellationToken.None).ConfigureAwait(false);

		// Assert - a second receive from the same consumer should return nothing (offset committed)
		var secondReceive = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);
		secondReceive.Count.ShouldBe(0);
	}

	[Fact]
	public async Task RejectMessage_WithoutRequeue_CommitsOffset()
	{
		// Arrange
		var topic = $"test-reject-{Guid.NewGuid():N}";
		var groupId = $"test-group-reject-{Guid.NewGuid():N}";

		using (var producer = BuildProducer())
		{
			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "reject-key",
				Value = Encoding.UTF8.GetBytes("reject-payload"),
				Headers = new Headers { { "message-id", Encoding.UTF8.GetBytes("reject-msg-1") } },
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		var consumer = BuildConsumer(topic, groupId);
		await using var receiver = CreateReceiver(consumer, topic);

		// Poll until we receive the message
		var received = await ReceiveWithRetryAsync(receiver, 1, ReceiveTimeout).ConfigureAwait(false);
		received.Count.ShouldBe(1);

		// Act - reject without requeue (commits offset to skip the message)
		await receiver.RejectAsync(received[0], "test rejection", requeue: false, CancellationToken.None)
			.ConfigureAwait(false);

		// Assert - the message should not be redelivered
		var secondReceive = await receiver.ReceiveAsync(1, CancellationToken.None).ConfigureAwait(false);
		secondReceive.Count.ShouldBe(0);
	}

	[Fact]
	public async Task GetService_ReturnsUnderlyingConsumer()
	{
		// Arrange
		var topic = "test-getservice-receiver";
		var consumer = BuildConsumer(topic, "test-group-getservice");
		await using var receiver = CreateReceiver(consumer, topic);

		// Act
		var service = receiver.GetService(typeof(IConsumer<string, byte[]>));

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeAssignableTo<IConsumer<string, byte[]>>();
	}

	[Fact]
	public async Task GetService_ReturnsNullForUnknownType()
	{
		// Arrange
		var topic = "test-getservice-null-receiver";
		var consumer = BuildConsumer(topic, "test-group-getservice-null");
		await using var receiver = CreateReceiver(consumer, topic);

		// Act
		var service = receiver.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();
	}

	[Fact]
	public async Task ReceiveMessage_SetsEnqueuedAt()
	{
		// Arrange
		var topic = $"test-enqueued-{Guid.NewGuid():N}";
		var groupId = $"test-group-enqueued-{Guid.NewGuid():N}";
		var beforeSend = DateTimeOffset.UtcNow.AddSeconds(-1);

		using (var producer = BuildProducer())
		{
			await producer.ProduceAsync(topic, new Message<string, byte[]>
			{
				Key = "ts-key",
				Value = Encoding.UTF8.GetBytes("ts-payload"),
				Headers = new Headers { { "message-id", Encoding.UTF8.GetBytes("ts-msg") } },
			}).ConfigureAwait(false);
			producer.Flush(TimeSpan.FromSeconds(5));
		}

		var consumer = BuildConsumer(topic, groupId);
		await using var receiver = CreateReceiver(consumer, topic);

		// Act - poll until we receive the message
		var received = await ReceiveWithRetryAsync(receiver, 1, ReceiveTimeout).ConfigureAwait(false);

		// Assert
		received.Count.ShouldBe(1);
		received[0].EnqueuedAt.ShouldBeGreaterThan(beforeSend);
		received[0].EnqueuedAt.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
	}

	#region Helpers

	/// <summary>
	/// Polls ReceiveAsync until the expected number of messages is received, or timeout.
	/// This is needed because Kafka consumers require time for partition assignment after subscribing.
	/// </summary>
	private static async Task<IReadOnlyList<TransportReceivedMessage>> ReceiveWithRetryAsync(
		ITransportReceiver receiver,
		int expectedCount,
		TimeSpan timeout)
	{
		var allMessages = new List<TransportReceivedMessage>();
		var deadline = DateTime.UtcNow + timeout;

		while (allMessages.Count < expectedCount && DateTime.UtcNow < deadline)
		{
			var batch = await receiver.ReceiveAsync(expectedCount - allMessages.Count, CancellationToken.None)
				.ConfigureAwait(false);

			if (batch.Count > 0)
			{
				allMessages.AddRange(batch);
			}
			else
			{
				// Wait briefly before retrying (partition assignment may be pending)
				await Task.Delay(200).ConfigureAwait(false);
			}
		}

		return allMessages;
	}

	private IProducer<string, byte[]> BuildProducer()
	{
		var config = new ProducerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			AllowAutoCreateTopics = true,
		};

		return new ProducerBuilder<string, byte[]>(config).Build();
	}

	private IConsumer<string, byte[]> BuildConsumer(string topic, string groupId)
	{
		var consumer = BuildConsumerRaw(topic, groupId, AutoOffsetReset.Earliest);
		consumer.Subscribe(topic);
		return consumer;
	}

	private IConsumer<string, byte[]> BuildConsumerRaw(string topic, string groupId, AutoOffsetReset offsetReset)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = offsetReset,
			EnableAutoCommit = false,
		};

		return new ConsumerBuilder<string, byte[]>(config).Build();
	}

	private static ITransportReceiver CreateReceiver(IConsumer<string, byte[]> consumer, string topic)
	{
		// KafkaTransportReceiver is internal, so we use reflection to create it.
		// This is acceptable for integration tests that need to test the real implementation.
		var receiverType = typeof(KafkaOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Kafka.KafkaTransportReceiver")!;

		// Create Logger<KafkaTransportReceiver> via reflection since the type is internal
		var loggerOfT = typeof(Logger<>).MakeGenericType(receiverType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var ctor = receiverType.GetConstructors()[0];
		var instance = ctor.Invoke([consumer, topic, logger]);

		return (ITransportReceiver)instance;
	}

	#endregion
}
