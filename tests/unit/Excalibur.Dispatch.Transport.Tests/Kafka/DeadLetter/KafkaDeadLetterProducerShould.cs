// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

#pragma warning disable CA2012 // Use ValueTasks correctly — FakeItEasy .Returns() stores ValueTask

using System.Text;

using Confluent.Kafka;

using Excalibur.Dispatch.Transport;
using Excalibur.Dispatch.Transport.Diagnostics;
using Excalibur.Dispatch.Transport.Kafka;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Unit tests for <see cref="KafkaDeadLetterProducer"/> — validates B1 fix:
/// <c>ProduceToOriginalTopicAsync</c> publishes to the exact target topic without DLQ suffix.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaDeadLetterProducerShould
{
	private readonly IProducer<string, byte[]> _kafkaProducer;
	private readonly KafkaDeadLetterProducer _sut;

	private static readonly byte[] TestBody = Encoding.UTF8.GetBytes("test-body");

	public KafkaDeadLetterProducerShould()
	{
		_kafkaProducer = A.Fake<IProducer<string, byte[]>>();
		var options = Microsoft.Extensions.Options.Options.Create(new KafkaDeadLetterOptions());

		// Configure fake producer to return a valid DeliveryResult
		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "captured-topic",
				Partition = new Partition(0),
				Offset = new Offset(42),
			});

		_sut = new KafkaDeadLetterProducer(_kafkaProducer, options, NullLogger<KafkaDeadLetterProducer>.Instance);
	}

	[Fact]
	public async Task ProduceAsync_AppliesDlqSuffix_ToSourceTopic()
	{
		// Arrange
		var message = CreateTransportMessage();
		string? capturedTopic = null;

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Invokes((string topic, Message<string, byte[]> _, CancellationToken _) =>
				capturedTopic = topic)
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders.dead-letter",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act
		await _sut.ProduceAsync(message, "orders", "Processing failed", 3, cancellationToken: CancellationToken.None);

		// Assert — DLQ suffix IS applied
		capturedTopic.ShouldBe("orders.dead-letter");
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_PublishesToExactTargetTopic_NoDlqSuffix()
	{
		// Arrange
		var message = CreateTransportMessage();
		string? capturedTopic = null;

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Invokes((string topic, Message<string, byte[]> _, CancellationToken _) =>
				capturedTopic = topic)
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act
		await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);

		// Assert — exact target topic, NO .dead-letter suffix
		capturedTopic.ShouldBe("orders");
		capturedTopic.ShouldNotEndWith(".dead-letter");
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_PreservesMessageKey()
	{
		// Arrange
		var message = CreateTransportMessage();
		message.Properties[TransportTelemetryConstants.PropertyKeys.PartitionKey] = "partition-key-123";
		string? capturedKey = null;

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Invokes((string _, Message<string, byte[]> msg, CancellationToken _) =>
				capturedKey = msg.Key)
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act
		await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);

		// Assert
		capturedKey.ShouldBe("partition-key-123");
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_UsesMessageId_WhenPartitionKeyIsAbsent()
	{
		// Arrange — TransportMessage with no PartitionKey in Properties
		var message = CreateTransportMessage();
		message.Properties.Remove(TransportTelemetryConstants.PropertyKeys.PartitionKey);
		string? capturedKey = null;

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Invokes((string _, Message<string, byte[]> msg, CancellationToken _) =>
				capturedKey = msg.Key)
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act
		await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);

		// Assert — falls back to message ID
		capturedKey.ShouldBe(message.Id);
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_ExcludesDlqHeaders()
	{
		// Arrange
		var message = CreateTransportMessage();
		message.Properties["custom_header"] = "keep-me";
		message.Properties["dlq_reason"] = "should-be-stripped";
		message.Properties["dlq_moved_at"] = "should-be-stripped";
		Headers? capturedHeaders = null;

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Invokes((string _, Message<string, byte[]> msg, CancellationToken _) =>
				capturedHeaders = msg.Headers)
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act
		await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);

		// Assert — custom headers preserved, dlq_ headers stripped
		capturedHeaders.ShouldNotBeNull();
		var headerKeys = capturedHeaders.Select(h => h.Key).ToList();
		headerKeys.ShouldContain("custom_header");
		headerKeys.ShouldNotContain("dlq_reason");
		headerKeys.ShouldNotContain("dlq_moved_at");
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_ReturnsTopicPartitionOffset()
	{
		// Arrange
		var message = CreateTransportMessage();

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(2),
				Offset = new Offset(99),
			});

		// Act
		var result = await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);

		// Assert
		result.ShouldBe("orders:2:99");
	}

	[Fact]
	public async Task ProduceToOriginalTopicAsync_WithNoProperties_DoesNotThrow()
	{
		// Arrange — message with empty properties
		var message = new TransportMessage
		{
			Id = "msg-empty-props",
			Body = TestBody,
		};

		A.CallTo(() => _kafkaProducer.ProduceAsync(
				A<string>._,
				A<Message<string, byte[]>>._,
				A<CancellationToken>._))
			.Returns(new DeliveryResult<string, byte[]>
			{
				Topic = "orders",
				Partition = new Partition(0),
				Offset = new Offset(1),
			});

		// Act & Assert — should not throw
		var result = await _sut.ProduceToOriginalTopicAsync(message, "orders", CancellationToken.None);
		result.ShouldNotBeNullOrWhiteSpace();
	}

	private static TransportMessage CreateTransportMessage() => new()
	{
		Id = "test-msg-001",
		Body = TestBody,
		Properties =
		{
			[TransportTelemetryConstants.PropertyKeys.PartitionKey] = "pk-001",
		},
	};
}
