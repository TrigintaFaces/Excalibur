// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="RabbitMqToKafkaMapper"/>.
/// Tests cross-transport property mapping per Sprint 395.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class RabbitMqToKafkaMapperShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_InitializeWithCorrectName()
	{
		// Arrange & Act
		var mapper = new RabbitMqToKafkaMapper();

		// Assert
		mapper.Name.ShouldBe("RabbitMqToKafka");
	}

	[Fact]
	public void Constructor_InitializeWithCorrectSourceTransport()
	{
		// Arrange & Act
		var mapper = new RabbitMqToKafkaMapper();

		// Assert
		mapper.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void Constructor_InitializeWithCorrectTargetTransport()
	{
		// Arrange & Act
		var mapper = new RabbitMqToKafkaMapper();

		// Assert
		mapper.TargetTransport.ShouldBe("kafka");
	}

	#endregion

	#region CanMap Tests

	[Fact]
	public void CanMap_ReturnTrueForRabbitMqToKafka()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();

		// Act
		var result = mapper.CanMap("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void CanMap_ReturnFalseForKafkaToRabbitMq()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();

		// Act
		var result = mapper.CanMap("kafka", "rabbitmq");

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void CanMap_ReturnFalseForOtherTransports()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();

		// Act & Assert
		mapper.CanMap("servicebus", "kafka").ShouldBeFalse();
		mapper.CanMap("rabbitmq", "servicebus").ShouldBeFalse();
		mapper.CanMap("servicebus", "pubsub").ShouldBeFalse();
	}

	[Theory]
	[InlineData("RABBITMQ", "KAFKA")]
	[InlineData("RabbitMQ", "Kafka")]
	[InlineData("RabbitMq", "kafka")]
	public void CanMap_BeCaseInsensitive(string source, string target)
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();

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
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("test-message-id");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.MessageId.ShouldBe("test-message-id");
	}

	[Fact]
	public void Map_MapRoutingKeyToKey()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.created"
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe("orders.created");
	}

	[Fact]
	public void Map_MapPriorityToHeader()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Priority = 5
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldContainKey(RabbitMqToKafkaMapper.PriorityHeader);
		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("5");
	}

	[Fact]
	public void Map_MapExpirationToHeader()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Expiration = "60000"
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldContainKey(RabbitMqToKafkaMapper.ExpirationHeader);
		result.Headers[RabbitMqToKafkaMapper.ExpirationHeader].ShouldBe("60000");
	}

	[Fact]
	public void Map_MapReplyToToHeader()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			ReplyTo = "reply-queue"
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldContainKey(RabbitMqToKafkaMapper.ReplyToHeader);
		result.Headers[RabbitMqToKafkaMapper.ReplyToHeader].ShouldBe("reply-queue");
	}

	[Fact]
	public void Map_CopyAllPropertiesWhenAllSet()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.created",
			Priority = 5,
			Expiration = "60000",
			ReplyTo = "reply-queue",
			CorrelationId = "corr-1",
			CausationId = "cause-1",
			ContentType = "application/json"
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe("orders.created");
		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("5");
		result.Headers[RabbitMqToKafkaMapper.ExpirationHeader].ShouldBe("60000");
		result.Headers[RabbitMqToKafkaMapper.ReplyToHeader].ShouldBe("reply-queue");
		result.CorrelationId.ShouldBe("corr-1");
		result.CausationId.ShouldBe("cause-1");
		result.ContentType.ShouldBe("application/json");
	}

	[Fact]
	public void Map_PreserveExistingHeaders()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");
		source.SetHeader("custom-header", "custom-value");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.Headers.ShouldContainKey("custom-header");
		result.Headers["custom-header"].ShouldBe("custom-value");
	}

	#endregion

	#region Map - Partial/Null Property Tests

	[Fact]
	public void Map_HandleNullRoutingKey()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = null
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe(string.Empty);
	}

	[Fact]
	public void Map_HandleEmptyRoutingKey()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = ""
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe(string.Empty);
	}

	[Fact]
	public void Map_NotSetPriorityHeaderWhenNull()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Priority = null
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.PriorityHeader);
	}

	[Fact]
	public void Map_NotSetExpirationHeaderWhenNull()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Expiration = null
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ExpirationHeader);
	}

	[Fact]
	public void Map_NotSetExpirationHeaderWhenEmpty()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Expiration = ""
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ExpirationHeader);
	}

	[Fact]
	public void Map_NotSetReplyToHeaderWhenNull()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			ReplyTo = null
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ReplyToHeader);
	}

	[Fact]
	public void Map_NotSetReplyToHeaderWhenEmpty()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			ReplyTo = ""
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ReplyToHeader);
	}

	[Fact]
	public void Map_HandleMinimalSource()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");
		// No properties set except message ID

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.MessageId.ShouldBe("msg-1");
		result.Key.ShouldBe(string.Empty);
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.PriorityHeader);
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ExpirationHeader);
		result.Headers.ShouldNotContainKey(RabbitMqToKafkaMapper.ReplyToHeader);
	}

	#endregion

	#region Map - Edge Case Tests

	[Fact]
	public void Map_HandlePriorityZero()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Priority = 0
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers.ShouldContainKey(RabbitMqToKafkaMapper.PriorityHeader);
		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("0");
	}

	[Fact]
	public void Map_HandleMaxPriority()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			Priority = 255
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("255");
	}

	[Fact]
	public void Map_CreateKafkaMessageContext()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		_ = result.ShouldBeOfType<KafkaMessageContext>();
	}

	[Fact]
	public void Map_SetTargetTransport()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.TargetTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Map_PreserveSourceTransport()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");
		// RabbitMqMessageContext sets SourceTransport to "rabbitmq" in constructor

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.SourceTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void Map_PreserveTimestamp()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");
		var originalTimestamp = source.Timestamp;

		// Act
		var result = mapper.Map(source, "kafka");

		// Assert
		result.Timestamp.ShouldBe(originalTimestamp);
	}

	[Fact]
	public void Map_HandleLongRoutingKey()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var longRoutingKey = new string('a', 10000);
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = longRoutingKey
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe(longRoutingKey);
	}

	[Fact]
	public void Map_HandleSpecialCharactersInRoutingKey()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.*.#.special-chars_123"
		};

		// Act
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe("orders.*.#.special-chars_123");
	}

	#endregion

	#region Map - Argument Validation Tests

	[Fact]
	public void Map_ThrowWhenSourceIsNull()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => mapper.Map(null!, "kafka"));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsNull()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, null!));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsEmpty()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, ""));
	}

	[Fact]
	public void Map_ThrowWhenTargetTransportIsWhitespace()
	{
		// Arrange
		var mapper = new RabbitMqToKafkaMapper();
		var source = new RabbitMqMessageContext("msg-1");

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => mapper.Map(source, "   "));
	}

	#endregion

	#region Header Constant Tests

	[Fact]
	public void PriorityHeader_HaveCorrectValue()
	{
		// Assert
		RabbitMqToKafkaMapper.PriorityHeader.ShouldBe("x-priority");
	}

	[Fact]
	public void ExpirationHeader_HaveCorrectValue()
	{
		// Assert
		RabbitMqToKafkaMapper.ExpirationHeader.ShouldBe("x-expiration");
	}

	[Fact]
	public void ReplyToHeader_HaveCorrectValue()
	{
		// Assert
		RabbitMqToKafkaMapper.ReplyToHeader.ShouldBe("x-reply-to");
	}

	#endregion
}
