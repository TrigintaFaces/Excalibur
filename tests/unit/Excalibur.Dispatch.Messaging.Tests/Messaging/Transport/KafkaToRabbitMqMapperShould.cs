// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="KafkaToRabbitMqMapper"/>.
/// Tests cross-transport property mapping per Sprint 395.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class KafkaToRabbitMqMapperShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_InitializeWithCorrectName()
	{
		// Arrange & Act
		var mapper = new KafkaToRabbitMqMapper();

		// Assert
		mapper.Name.ShouldBe("KafkaToRabbitMq");
	}

	[Fact]
	public void Constructor_InitializeWithCorrectSourceTransport()
	{
		// Arrange & Act
		var mapper = new KafkaToRabbitMqMapper();

		// Assert
		mapper.SourceTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Constructor_InitializeWithCorrectTargetTransport()
	{
		// Arrange & Act
		var mapper = new KafkaToRabbitMqMapper();

		// Assert
		mapper.TargetTransport.ShouldBe("rabbitmq");
	}

	#endregion

	#region CanMap Tests

	[Fact]
	public void CanMap_ReturnTrueForKafkaToRabbitMq()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();

		// Act
		var result = mapper.CanMap("kafka", "rabbitmq");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnFalseForRabbitMqToKafka()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanMap_ReturnFalseForOtherTransports()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();

		// Act & Assert
		mapper.CanMap("servicebus", "rabbitmq").ShouldBeFalse();
		mapper.CanMap("kafka", "servicebus").ShouldBeFalse();
		mapper.CanMap("servicebus", "pubsub").ShouldBeFalse();
	}

	[Theory]
	[InlineData("KAFKA", "RABBITMQ")]
	[InlineData("Kafka", "RabbitMQ")]
	[InlineData("kafka", "RabbitMq")]
	public void CanMap_BeCaseInsensitive(string source, string target)
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();

		// Act
		var result = mapper.CanMap(source, target);

		// Assert
		result.ShouldBeTrue();
	}

	#endregion

	#region Map - Happy Path Tests

	[Fact]
	public void Map_CopyMessageIdToTarget()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("test-message-id");

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		result.MessageId.ShouldBe("test-message-id");
	}

	[Fact]
	public void Map_MapKeyToRoutingKey()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			Key = "orders.created"
		};

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe("orders.created");
	}

	[Fact]
	public void Map_RestorePriorityFromHeader()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "5");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBe((byte)5);
	}

	[Fact]
	public void Map_RestoreExpirationFromHeader()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.ExpirationHeader, "60000");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Expiration.ShouldBe("60000");
	}

	[Fact]
	public void Map_RestoreReplyToFromHeader()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.ReplyToHeader, "reply-queue");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void Map_SetDeliveryModeToPersistent()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.DeliveryMode.ShouldBe((byte)2); // Persistent
	}

	[Fact]
	public void Map_CopyAllPropertiesWhenAllSet()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			Key = "orders.created",
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			ContentType = "application/json"
		};
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "5");
		source.SetHeader(KafkaToRabbitMqMapper.ExpirationHeader, "60000");
		source.SetHeader(KafkaToRabbitMqMapper.ReplyToHeader, "reply-queue");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe("orders.created");
		result.Priority.ShouldBe((byte)5);
		result.Expiration.ShouldBe("60000");
		result.ReplyTo.ShouldBe("reply-queue");
		result.DeliveryMode.ShouldBe((byte)2);
		result.CorrelationId.ShouldBe("corr-1");
		result.CausationId.ShouldBe("cause-1");
		result.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Map_PreserveOtherHeaders()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader("custom-header", "custom-value");

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		result.Headers.ShouldContainKey("custom-header");
		result.Headers["custom-header"].ShouldBe("custom-value");
	}

	#endregion

	#region Map - Partial/Null Property Tests

	[Fact]
	public void Map_HandleNullKey()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			Key = null
		};

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void Map_HandleEmptyKey()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			Key = ""
		};

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe(string.Empty);
	}

	[Fact]
	public void Map_NotSetPriorityWhenHeaderMissing()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		// No x-priority header set

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBeNull();
	}

	[Fact]
	public void Map_NotSetPriorityWhenHeaderInvalid()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "not-a-number");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBeNull();
	}

	[Fact]
	public void Map_NotSetExpirationWhenHeaderMissing()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		// No x-expiration header set

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Expiration.ShouldBeNull();
	}

	[Fact]
	public void Map_NotSetReplyToWhenHeaderMissing()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		// No x-reply-to header set

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.ReplyTo.ShouldBeNull();
	}

	[Fact]
	public void Map_HandleMinimalSource()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		// No properties set except message ID

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.MessageId.ShouldBe("msg-1");
		result.RoutingKey.ShouldBe(string.Empty);
		result.Priority.ShouldBeNull();
		result.Expiration.ShouldBeNull();
		result.ReplyTo.ShouldBeNull();
		result.DeliveryMode.ShouldBe((byte)2); // Always persistent
	}

	#endregion

	#region Map - Edge Case Tests

	[Fact]
	public void Map_HandlePriorityZero()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "0");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBe((byte)0);
	}

	[Fact]
	public void Map_HandleMaxPriority()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "255");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBe((byte)255);
	}

	[Fact]
	public void Map_HandlePriorityOutOfRange()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "256"); // Above byte.MaxValue

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBeNull(); // Should not set invalid priority
	}

	[Fact]
	public void Map_HandleNegativePriority()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "-1");

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Priority.ShouldBeNull(); // Should not set invalid priority
	}

	[Fact]
	public void Map_CreateRabbitMqMessageContext()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		_ = result.ShouldBeOfType<RabbitMqMessageContext>();
	}

	[Fact]
	public void Map_SetTargetTransport()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		result.TargetTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void Map_PreserveSourceTransport()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		// KafkaMessageContext sets SourceTransport to "kafka" in constructor

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		result.SourceTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Map_PreserveTimestamp()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");
		var originalTimestamp = source.Timestamp;

		// Act
		var result = mapper.Map(source, "rabbitmq");

		// Assert
		result.Timestamp.ShouldBe(originalTimestamp);
	}

	[Fact]
	public void Map_HandleLongKey()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var longKey = new string('a', 10000);
		var source = new KafkaMessageContext("msg-1")
		{
			Key = longKey
		};

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe(longKey);
	}

	[Fact]
	public void Map_HandleSpecialCharactersInKey()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1")
		{
			Key = "orders.*.#.special-chars_123"
		};

		// Act
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe("orders.*.#.special-chars_123");
	}

	#endregion

	#region Map - Argument Validation Tests

	[Fact]
	public void Map_ThrowWhenSourceIsNull()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => mapper.Map(null!, "rabbitmq"));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsNull()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, null!));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsEmpty()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, ""));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsWhitespace()
	{
		// Arrange
		var mapper = new KafkaToRabbitMqMapper();
		var source = new KafkaMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, "   "));
	}

	#endregion

	#region Header Constant Tests

	[Fact]
	public void PriorityHeader_HaveCorrectValue()
	{
		// Assert
		KafkaToRabbitMqMapper.PriorityHeader.ShouldBe("x-priority");
	}

	[Fact]
	public void ExpirationHeader_HaveCorrectValue()
	{
		// Assert
		KafkaToRabbitMqMapper.ExpirationHeader.ShouldBe("x-expiration");
	}

	[Fact]
	public void ReplyToHeader_HaveCorrectValue()
	{
		// Assert
		KafkaToRabbitMqMapper.ReplyToHeader.ShouldBe("x-reply-to");
	}

	#endregion

	#region Round-Trip Tests

	[Fact]
	public void RoundTrip_PreservePropertiesWhenMappedBackAndForth()
	{
		// Arrange
		var rmqToKafka = new RabbitMqToKafkaMapper();
		var kafkaToRmq = new KafkaToRabbitMqMapper();

		var original = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.created",
			Priority = 5,
			Expiration = "60000",
			ReplyTo = "reply-queue",
			CorrelationId = "corr-1",
			CausationId = "cause-1"
		};

		// Act
		var kafka = rmqToKafka.Map(original, "kafka") as KafkaMessageContext;
		var restored = kafkaToRmq.Map(kafka, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = restored.ShouldNotBeNull();
		restored.RoutingKey.ShouldBe(original.RoutingKey);
		restored.Priority.ShouldBe(original.Priority);
		restored.Expiration.ShouldBe(original.Expiration);
		restored.ReplyTo.ShouldBe(original.ReplyTo);
		restored.CorrelationId.ShouldBe(original.CorrelationId);
		restored.CausationId.ShouldBe(original.CausationId);
	}

	[Fact]
	public void RoundTrip_PreserveKeyWhenMappedBackAndForth()
	{
		// Arrange
		var kafkaToRmq = new KafkaToRabbitMqMapper();
		var rmqToKafka = new RabbitMqToKafkaMapper();

		var original = new KafkaMessageContext("msg-1")
		{
			Key = "user-12345",
			CorrelationId = "corr-1"
		};

		// Act
		var rmq = kafkaToRmq.Map(original, "rabbitmq") as RabbitMqMessageContext;
		var restored = rmqToKafka.Map(rmq, "kafka") as KafkaMessageContext;

		// Assert
		_ = restored.ShouldNotBeNull();
		restored.Key.ShouldBe(original.Key);
		restored.CorrelationId.ShouldBe(original.CorrelationId);
	}

	[Fact]
	public void RoundTrip_PreservePriorityZero()
	{
		// Arrange - Edge case: Priority 0 should survive round trip
		var rmqToKafka = new RabbitMqToKafkaMapper();
		var kafkaToRmq = new KafkaToRabbitMqMapper();

		var original = new RabbitMqMessageContext("msg-1")
		{
			Priority = 0 // Edge case - zero is valid
		};

		// Act
		var kafka = rmqToKafka.Map(original, "kafka") as KafkaMessageContext;
		var restored = kafkaToRmq.Map(kafka, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = restored.ShouldNotBeNull();
		restored.Priority.ShouldBe((byte)0);
	}

	[Fact]
	public void RoundTrip_HandleMinimalProperties()
	{
		// Arrange - Only required properties
		var rmqToKafka = new RabbitMqToKafkaMapper();
		var kafkaToRmq = new KafkaToRabbitMqMapper();

		var original = new RabbitMqMessageContext("msg-1");
		// No optional properties set

		// Act
		var kafka = rmqToKafka.Map(original, "kafka") as KafkaMessageContext;
		var restored = kafkaToRmq.Map(kafka, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = restored.ShouldNotBeNull();
		restored.MessageId.ShouldBe(original.MessageId);
		restored.RoutingKey.ShouldBe(string.Empty); // Null → empty → empty
		restored.Priority.ShouldBeNull();
		restored.Expiration.ShouldBeNull();
		restored.ReplyTo.ShouldBeNull();
	}

	#endregion
}
