// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Text;

using Confluent.Kafka;
using Confluent.Kafka.Admin;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;

using Tests.Shared.Fixtures;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Integration tests for Kafka partition key routing and ordered delivery with a real Kafka container.
/// Verifies that messages with the same partition key are delivered to the same partition
/// and maintain ordering within that partition.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Provider", "Kafka")]
[Trait("Component", "Transport")]
[Collection(ContainerCollections.Kafka)]
public sealed class KafkaPartitioningIntegrationShould
{
	private readonly KafkaContainerFixture _fixture;

	public KafkaPartitioningIntegrationShould(KafkaContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task SamePartitionKey_RoutesToSamePartition()
	{
		// Arrange - create topic with multiple partitions
		var topic = $"test-partition-{Guid.NewGuid():N}";
		await CreateTopicAsync(topic, partitions: 3).ConfigureAwait(false);

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		// Send messages with the same partition key
		var partitionKey = "order-42";
		var results = new List<SendResult>();
		for (var i = 0; i < 5; i++)
		{
			var msg = new TransportMessage
			{
				Id = $"pk-msg-{i}",
				Body = Encoding.UTF8.GetBytes($"payload-{i}"),
				Properties =
				{
					[TransportTelemetryConstants.PropertyKeys.PartitionKey] = partitionKey,
				},
			};

			var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
			result.IsSuccess.ShouldBeTrue();
			results.Add(result);
		}

		await sender.DisposeAsync().ConfigureAwait(false);

		// Assert - all messages should be on the same partition
		var partitions = results.Select(r => r.Partition).Distinct().ToList();
		partitions.Count.ShouldBe(1, "All messages with the same partition key should go to the same partition");
	}

	[Fact]
	public async Task DifferentPartitionKeys_CanRouteToMultiplePartitions()
	{
		// Arrange - create topic with multiple partitions
		var topic = $"test-multi-part-{Guid.NewGuid():N}";
		await CreateTopicAsync(topic, partitions: 6).ConfigureAwait(false);

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		// Send messages with different partition keys
		var results = new List<SendResult>();
		for (var i = 0; i < 20; i++)
		{
			var msg = new TransportMessage
			{
				Id = $"mp-msg-{i}",
				Body = Encoding.UTF8.GetBytes($"payload-{i}"),
				Properties =
				{
					[TransportTelemetryConstants.PropertyKeys.PartitionKey] = $"unique-key-{i}",
				},
			};

			var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
			result.IsSuccess.ShouldBeTrue();
			results.Add(result);
		}

		await sender.DisposeAsync().ConfigureAwait(false);

		// Assert - with 20 different keys across 6 partitions, we should see multiple partitions used
		var distinctPartitions = results.Select(r => r.Partition).Distinct().Count();
		distinctPartitions.ShouldBeGreaterThan(1,
			"Messages with different partition keys should distribute across partitions");
	}

	[Fact]
	public async Task OrderingKey_UsedAsKafkaMessageKey()
	{
		// Arrange
		var topic = $"test-ordering-key-{Guid.NewGuid():N}";

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		var orderingKey = "customer-123";
		var msg = new TransportMessage
		{
			Id = "ok-msg-1",
			Body = Encoding.UTF8.GetBytes("test"),
			Properties =
			{
				[TransportTelemetryConstants.PropertyKeys.OrderingKey] = orderingKey,
			},
		};

		// Act
		var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
		result.IsSuccess.ShouldBeTrue();

		await sender.DisposeAsync().ConfigureAwait(false);

		// Assert - verify the Kafka message key is set to the ordering key
		using var consumer = BuildRawConsumer(topic, $"verify-ok-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();
		consumed.Message.Key.ShouldBe(orderingKey);
	}

	[Fact]
	public async Task MessagesWithSameKey_MaintainOrderWithinPartition()
	{
		// Arrange - single partition to guarantee ordering
		var topic = $"test-order-{Guid.NewGuid():N}";
		await CreateTopicAsync(topic, partitions: 1).ConfigureAwait(false);

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		var messageCount = 10;
		var partitionKey = "sequential-order";

		for (var i = 0; i < messageCount; i++)
		{
			var msg = new TransportMessage
			{
				Id = $"order-msg-{i}",
				Body = Encoding.UTF8.GetBytes($"{i}"),
				Properties =
				{
					[TransportTelemetryConstants.PropertyKeys.PartitionKey] = partitionKey,
				},
			};

			var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
			result.IsSuccess.ShouldBeTrue();
		}

		await sender.DisposeAsync().ConfigureAwait(false);

		// Act - consume and verify order
		using var consumer = BuildRawConsumer(topic, $"verify-order-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var receivedOffsets = new List<long>();
		var receivedBodies = new List<string>();

		for (var i = 0; i < messageCount; i++)
		{
			var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
			consumed.ShouldNotBeNull();
			receivedOffsets.Add(consumed.Offset.Value);
			receivedBodies.Add(Encoding.UTF8.GetString(consumed.Message.Value));
		}

		// Assert - offsets should be strictly increasing (ordered)
		for (var i = 1; i < receivedOffsets.Count; i++)
		{
			receivedOffsets[i].ShouldBeGreaterThan(receivedOffsets[i - 1]);
		}

		// Assert - bodies should be in original order
		for (var i = 0; i < messageCount; i++)
		{
			receivedBodies[i].ShouldBe($"{i}");
		}
	}

	[Fact]
	public async Task DefaultKey_UsesMessageId_WhenNoPartitionKeySet()
	{
		// Arrange
		var topic = $"test-default-key-{Guid.NewGuid():N}";

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		var messageId = "explicit-msg-id-42";
		var msg = new TransportMessage
		{
			Id = messageId,
			Body = Encoding.UTF8.GetBytes("no-partition-key"),
		};

		// Act
		var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
		result.IsSuccess.ShouldBeTrue();

		await sender.DisposeAsync().ConfigureAwait(false);

		// Assert - when no partition/ordering key, the message ID should be used as the Kafka key
		using var consumer = BuildRawConsumer(topic, $"verify-default-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();
		consumed.Message.Key.ShouldBe(messageId);
	}

	[Fact]
	public async Task OrderingKey_TakesPrecedence_OverPartitionKey()
	{
		// Arrange
		var topic = $"test-key-precedence-{Guid.NewGuid():N}";

		using var producer = BuildProducer();
		var sender = CreateSender(producer, topic);

		var msg = new TransportMessage
		{
			Id = "precedence-msg",
			Body = Encoding.UTF8.GetBytes("test"),
			Properties =
			{
				[TransportTelemetryConstants.PropertyKeys.OrderingKey] = "ordering-key-value",
				[TransportTelemetryConstants.PropertyKeys.PartitionKey] = "partition-key-value",
			},
		};

		// Act
		var result = await sender.SendAsync(msg, CancellationToken.None).ConfigureAwait(false);
		result.IsSuccess.ShouldBeTrue();

		await sender.DisposeAsync().ConfigureAwait(false);

		// Assert - ordering key should take precedence over partition key
		using var consumer = BuildRawConsumer(topic, $"verify-precedence-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var consumed = ConsumeWithRetry(consumer, TimeSpan.FromSeconds(15));
		consumed.ShouldNotBeNull();
		consumed.Message.Key.ShouldBe("ordering-key-value");
	}

	#region Helpers

	private async Task CreateTopicAsync(string topicName, int partitions)
	{
		var adminConfig = new AdminClientConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			SocketTimeoutMs = 30000,
		};

		using var adminClient = new AdminClientBuilder(adminConfig).Build();

		// Retry topic creation and metadata readiness — controller elections can take time in CI.
		var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(90);
		while (true)
		{
			try
			{
				await adminClient.CreateTopicsAsync([
					new global::Confluent.Kafka.Admin.TopicSpecification
					{
						Name = topicName,
						NumPartitions = partitions,
						ReplicationFactor = 1,
					}
				], new CreateTopicsOptions
				{
					OperationTimeout = TimeSpan.FromSeconds(30),
					RequestTimeout = TimeSpan.FromSeconds(30),
				}).ConfigureAwait(false);

				break;
			}
			catch (CreateTopicsException) when (DateTime.UtcNow < deadline)
			{
				await Task.Delay(1000).ConfigureAwait(false);
			}
			catch (KafkaException) when (DateTime.UtcNow < deadline)
			{
				await Task.Delay(1000).ConfigureAwait(false);
			}
		}

		while (DateTime.UtcNow < deadline)
		{
			try
			{
				var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
				var topic = metadata.Topics.FirstOrDefault(t => string.Equals(t.Topic, topicName, StringComparison.Ordinal));
				if (topic is not null && topic.Error.Code == ErrorCode.NoError && topic.Partitions.Count == partitions)
				{
					return;
				}
			}
			catch (KafkaException)
			{
				// keep retrying until deadline
			}

			await Task.Delay(500).ConfigureAwait(false);
		}

		throw new TimeoutException($"Kafka topic '{topicName}' was not ready with {partitions} partitions before deadline.");
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

	private IConsumer<string, byte[]> BuildRawConsumer(string topic, string groupId)
	{
		var config = new ConsumerConfig
		{
			BootstrapServers = _fixture.BootstrapServers,
			GroupId = groupId,
			AutoOffsetReset = AutoOffsetReset.Earliest,
			EnableAutoCommit = true,
		};

		return new ConsumerBuilder<string, byte[]>(config).Build();
	}

	private static ITransportSender CreateSender(IProducer<string, byte[]> producer, string topic)
	{
		var senderType = typeof(KafkaOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Kafka.KafkaTransportSender")!;

		var loggerOfT = typeof(Logger<>).MakeGenericType(senderType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var ctor = senderType.GetConstructors()[0];
		return (ITransportSender)ctor.Invoke([producer, topic, logger]);
	}

	private static global::Confluent.Kafka.ConsumeResult<string, byte[]>? ConsumeWithRetry(IConsumer<string, byte[]> consumer, TimeSpan timeout)
	{
		var deadline = DateTime.UtcNow + timeout;
		while (DateTime.UtcNow < deadline)
		{
			var result = consumer.Consume(TimeSpan.FromSeconds(2));
			if (result?.Message is not null)
			{
				return result;
			}
		}

		return null;
	}

	#endregion
}
