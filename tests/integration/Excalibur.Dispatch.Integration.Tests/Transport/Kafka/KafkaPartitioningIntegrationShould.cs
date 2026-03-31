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
using Tests.Shared.Infrastructure;

namespace Excalibur.Dispatch.Integration.Tests.Transport.Kafka;

/// <summary>
/// Integration tests for Kafka partition key routing and ordered delivery with a real Kafka container.
/// Verifies that messages with the same partition key are delivered to the same partition
/// and maintain ordering within that partition.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Database", "Kafka")]
[Trait("Component", "Transport")]
[Collection(ContainerCollections.Kafka)]
public sealed class KafkaPartitioningIntegrationShould
{
	private static readonly TimeSpan MessageWaitTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(15));
	private static readonly TimeSpan TopicReadyTimeout = TestTimeouts.Scale(TimeSpan.FromSeconds(90));

	private readonly KafkaContainerFixture _fixture;

	public KafkaPartitioningIntegrationShould(KafkaContainerFixture fixture)
	{
		_fixture = fixture;
	}

	private void EnsureKafkaAvailable() =>
		Skip.IfNot(_fixture.DockerAvailable, _fixture.InitializationError ?? "Kafka container not available");

	[SkippableFact]
	public async Task SamePartitionKey_RoutesToSamePartition()
	{
		EnsureKafkaAvailable();

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

	[SkippableFact]
	public async Task DifferentPartitionKeys_CanRouteToMultiplePartitions()
	{
		EnsureKafkaAvailable();

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

	[SkippableFact]
	public async Task OrderingKey_UsedAsKafkaMessageKey()
	{
		EnsureKafkaAvailable();

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

		var consumed = ConsumeWithRetry(consumer, MessageWaitTimeout);
		consumed.ShouldNotBeNull();
		consumed.Message.Key.ShouldBe(orderingKey);
	}

	[SkippableFact]
	public async Task MessagesWithSameKey_MaintainOrderWithinPartition()
	{
		EnsureKafkaAvailable();

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
			var consumed = ConsumeWithRetry(consumer, MessageWaitTimeout);
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

	[SkippableFact]
	public async Task DefaultKey_UsesMessageId_WhenNoPartitionKeySet()
	{
		EnsureKafkaAvailable();

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

		var consumed = ConsumeWithRetry(consumer, MessageWaitTimeout);
		consumed.ShouldNotBeNull();
		consumed.Message.Key.ShouldBe(messageId);
	}

	[SkippableFact]
	public async Task OrderingKey_TakesPrecedence_OverPartitionKey()
	{
		EnsureKafkaAvailable();

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

		var consumed = ConsumeWithRetry(consumer, MessageWaitTimeout);
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
		var topicCreated = await WaitHelpers.RetryUntilSuccessAsync(
			async () =>
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
				}
				catch (CreateTopicsException ex) when (AllTopicsAlreadyExist(ex))
				{
					// Topic was already created during retries.
				}
			},
			TopicReadyTimeout,
			TimeSpan.FromMilliseconds(500)).ConfigureAwait(false);

		if (!topicCreated)
		{
			throw new TimeoutException($"Kafka topic '{topicName}' creation timed out.");
		}

		var topicReady = await WaitHelpers.WaitUntilAsync(
			() =>
			{
				try
				{
					var metadata = adminClient.GetMetadata(topicName, TimeSpan.FromSeconds(5));
					for (var i = 0; i < metadata.Topics.Count; i++)
					{
						var topic = metadata.Topics[i];
						if (!string.Equals(topic.Topic, topicName, StringComparison.Ordinal))
						{
							continue;
						}

						return topic.Error.Code == ErrorCode.NoError && topic.Partitions.Count == partitions;
					}
				}
				catch (KafkaException)
				{
					// Metadata may not be ready yet.
				}

				return false;
			},
			TopicReadyTimeout,
			TimeSpan.FromMilliseconds(250)).ConfigureAwait(false);

		if (!topicReady)
		{
			throw new TimeoutException($"Kafka topic '{topicName}' was not ready with {partitions} partitions before timeout.");
		}
	}

	private static bool AllTopicsAlreadyExist(CreateTopicsException exception)
	{
		for (var i = 0; i < exception.Results.Count; i++)
		{
			if (exception.Results[i].Error.Code != ErrorCode.TopicAlreadyExists)
			{
				return false;
			}
		}

		return exception.Results.Count > 0;
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
	var stopwatch = System.Diagnostics.Stopwatch.StartNew();
	while (stopwatch.Elapsed < timeout)
	{
		var remaining = timeout - stopwatch.Elapsed;
		if (remaining <= TimeSpan.Zero)
		{
			break;
		}

		var pollTimeout = remaining < TimeSpan.FromMilliseconds(250) ? remaining : TimeSpan.FromMilliseconds(250);
		var result = consumer.Consume(pollTimeout);
		if (result?.Message is not null)
		{
			return result;
		}
	}

		return null;
	}

	#endregion
}
