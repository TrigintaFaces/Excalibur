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
/// Integration tests for <see cref="KafkaTransportSender"/> with a real Kafka container.
/// Uses a shared <see cref="KafkaContainerFixture"/> via xUnit collection to avoid
/// starting a new container per test (which overwhelms Docker).
/// </summary>
[Collection(ContainerCollections.Kafka)]
[Trait("Category", "Integration")]
[Trait("Provider", "Kafka")]
[Trait("Component", "Transport")]
public sealed class KafkaTransportSenderIntegrationShould
{
	private readonly KafkaContainerFixture _fixture;

	public KafkaTransportSenderIntegrationShould(KafkaContainerFixture fixture)
	{
		_fixture = fixture;
	}

	[Fact]
	public async Task SendSingleMessage_DeliversToTopic()
	{
		// Arrange
		var topic = $"test-send-single-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		var body = Encoding.UTF8.GetBytes("""{"orderId":"12345"}""");
		var message = new TransportMessage
		{
			Id = Guid.NewGuid().ToString(),
			Body = body,
			ContentType = "application/json",
			MessageType = "OrderCreated",
			CorrelationId = "corr-001",
			Subject = "orders",
		};

		// Act
		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();
		result.MessageId.ShouldBe(message.Id);
		result.Partition.ShouldNotBeNull();
		result.SequenceNumber.ShouldNotBeNull();
		result.AcceptedAt.ShouldNotBeNull();

		// Verify the message reaches Kafka via raw consumer
		using var consumer = BuildConsumer(topic, $"verify-send-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var consumed = consumer.Consume(TimeSpan.FromSeconds(10));
		consumed.ShouldNotBeNull();
		consumed.Message.ShouldNotBeNull();
		consumed.Message.Value.ShouldBe(body);

		// Verify headers were set correctly
		var headers = consumed.Message.Headers;
		GetHeaderValue(headers, "content-type").ShouldBe("application/json");
		GetHeaderValue(headers, "message-type").ShouldBe("OrderCreated");
		GetHeaderValue(headers, "correlation-id").ShouldBe("corr-001");
		GetHeaderValue(headers, "subject").ShouldBe("orders");
		GetHeaderValue(headers, "message-id").ShouldBe(message.Id);

		producer.Dispose();
	}

	[Fact]
	public async Task SendBatchMessages_AllDeliverSuccessfully()
	{
		// Arrange
		var topic = $"test-send-batch-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		var messages = Enumerable.Range(0, 5)
			.Select(i => new TransportMessage
			{
				Id = $"batch-msg-{i}",
				Body = Encoding.UTF8.GetBytes($"payload-{i}"),
				ContentType = "text/plain",
			})
			.ToList();

		// Act
		var batchResult = await sender.SendBatchAsync(messages, CancellationToken.None).ConfigureAwait(false);

		// Assert
		batchResult.TotalMessages.ShouldBe(5);
		batchResult.SuccessCount.ShouldBe(5);
		batchResult.FailureCount.ShouldBe(0);
		batchResult.IsCompleteSuccess.ShouldBeTrue();
		batchResult.Results.Count.ShouldBe(5);
		batchResult.Duration.ShouldNotBeNull();

		// Verify all messages reached Kafka
		using var consumer = BuildConsumer(topic, $"verify-batch-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var receivedCount = 0;
		for (var i = 0; i < 5; i++)
		{
			var consumed = consumer.Consume(TimeSpan.FromSeconds(10));
			if (consumed?.Message is not null)
			{
				receivedCount++;
			}
		}

		receivedCount.ShouldBe(5);

		producer.Dispose();
	}

	[Fact]
	public async Task SendEmptyBatch_ReturnsZeroCounts()
	{
		// Arrange
		var topic = $"test-empty-batch-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		// Act
		var batchResult = await sender.SendBatchAsync([], CancellationToken.None).ConfigureAwait(false);

		// Assert
		batchResult.TotalMessages.ShouldBe(0);
		batchResult.SuccessCount.ShouldBe(0);
		batchResult.FailureCount.ShouldBe(0);

		producer.Dispose();
	}

	[Fact]
	public async Task SendToAutoCreatedTopic_Succeeds()
	{
		// Arrange - Kafka auto-creates topics by default
		var topic = $"auto-create-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		var message = new TransportMessage
		{
			Id = "auto-create-msg",
			Body = Encoding.UTF8.GetBytes("auto-create-payload"),
		};

		// Act
		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();

		producer.Dispose();
	}

	[Fact]
	public async Task FlushAsync_CompletesWithoutError()
	{
		// Arrange
		var topic = $"test-flush-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		// Send a message first so there is something to flush
		var message = new TransportMessage
		{
			Id = "flush-msg",
			Body = Encoding.UTF8.GetBytes("flush-payload"),
		};
		await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Act & Assert - should not throw
		await sender.FlushAsync(CancellationToken.None).ConfigureAwait(false);

		producer.Dispose();
	}

	[Fact]
	public async Task GetService_ReturnsUnderlyingProducer()
	{
		// Arrange
		var topic = "test-getservice";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		// Act
		var service = sender.GetService(typeof(IProducer<string, byte[]>));

		// Assert
		service.ShouldNotBeNull();
		service.ShouldBeAssignableTo<IProducer<string, byte[]>>();

		producer.Dispose();
	}

	[Fact]
	public async Task GetService_ReturnsNullForUnknownType()
	{
		// Arrange
		var topic = "test-getservice-null";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		// Act
		var service = sender.GetService(typeof(string));

		// Assert
		service.ShouldBeNull();

		producer.Dispose();
	}

	[Fact]
	public async Task SendMessage_PreservesCustomProperties()
	{
		// Arrange
		var topic = $"test-custom-props-{Guid.NewGuid():N}";
		var producer = BuildProducer();
		await using var sender = CreateSender(producer, topic);

		var message = new TransportMessage
		{
			Id = "props-msg",
			Body = Encoding.UTF8.GetBytes("props-payload"),
			Properties =
			{
				["custom-header-1"] = "value-1",
				["custom-header-2"] = "value-2",
			},
		};

		// Act
		var result = await sender.SendAsync(message, CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsSuccess.ShouldBeTrue();

		using var consumer = BuildConsumer(topic, $"verify-props-{Guid.NewGuid():N}");
		consumer.Subscribe(topic);

		var consumed = consumer.Consume(TimeSpan.FromSeconds(10));
		consumed.ShouldNotBeNull();

		GetHeaderValue(consumed.Message.Headers, "custom-header-1").ShouldBe("value-1");
		GetHeaderValue(consumed.Message.Headers, "custom-header-2").ShouldBe("value-2");

		producer.Dispose();
	}

	#region Helpers

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
		// KafkaTransportSender is internal, so we use reflection to create it.
		// This is acceptable for integration tests that need to test the real implementation.
		var senderType = typeof(KafkaOptions).Assembly.GetType("Excalibur.Dispatch.Transport.Kafka.KafkaTransportSender")!;

		// Create Logger<KafkaTransportSender> via reflection since the type is internal
		var loggerOfT = typeof(Logger<>).MakeGenericType(senderType);
		var logger = Activator.CreateInstance(loggerOfT, NullLoggerFactory.Instance)!;

		var ctor = senderType.GetConstructors()[0];
		var instance = ctor.Invoke([producer, topic, logger]);

		return (ITransportSender)instance;
	}

	private static string? GetHeaderValue(Headers headers, string key)
	{
		var header = headers.FirstOrDefault(h => h.Key == key);
		return header is null ? null : Encoding.UTF8.GetString(header.GetValueBytes());
	}

	#endregion
}
