// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="KafkaMessageContext"/>.
/// </summary>
/// <remarks>
/// Tests the Kafka-specific message context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class KafkaMessageContextShould
{
	#region Constant Property Names

	[Fact]
	public void TopicPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.TopicPropertyName.ShouldBe("Topic");
	}

	[Fact]
	public void PartitionPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.PartitionPropertyName.ShouldBe("Partition");
	}

	[Fact]
	public void OffsetPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.OffsetPropertyName.ShouldBe("Offset");
	}

	[Fact]
	public void KeyPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.KeyPropertyName.ShouldBe("Key");
	}

	[Fact]
	public void LeaderEpochPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.LeaderEpochPropertyName.ShouldBe("LeaderEpoch");
	}

	[Fact]
	public void SchemaIdPropertyName_HasCorrectValue()
	{
		// Assert
		KafkaMessageContext.SchemaIdPropertyName.ShouldBe("SchemaId");
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_WithMessageId_SetsMessageId()
	{
		// Arrange
		var messageId = "kafka-message-123";

		// Act
		var context = new KafkaMessageContext(messageId);

		// Assert
		context.MessageId.ShouldBe(messageId);
	}

	[Fact]
	public void Constructor_WithMessageId_SetsSourceTransportToKafka()
	{
		// Arrange & Act
		var context = new KafkaMessageContext("test-id");

		// Assert
		context.SourceTransport.ShouldBe("kafka");
	}

	[Fact]
	public void Constructor_Default_GeneratesMessageId()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.MessageId.ShouldNotBeNullOrWhiteSpace();
	}

	[Fact]
	public void Constructor_Default_SetsSourceTransportToKafka()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.SourceTransport.ShouldBe("kafka");
	}

	#endregion

	#region Topic Property Tests

	[Fact]
	public void Topic_Default_IsNull()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.Topic.ShouldBeNull();
	}

	[Fact]
	public void Topic_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.Topic = "orders-topic";

		// Assert
		context.Topic.ShouldBe("orders-topic");
	}

	[Fact]
	public void Topic_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.Topic = "my-topic";

		// Act
		var result = context.GetTransportProperty<string>(KafkaMessageContext.TopicPropertyName);

		// Assert
		result.ShouldBe("my-topic");
	}

	#endregion

	#region Partition Property Tests

	[Fact]
	public void Partition_Default_IsZero()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.Partition.ShouldBe(0);
	}

	[Fact]
	public void Partition_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.Partition = 7;

		// Assert
		context.Partition.ShouldBe(7);
	}

	[Fact]
	public void Partition_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.Partition = 5;

		// Act
		var result = context.GetTransportProperty<int>(KafkaMessageContext.PartitionPropertyName);

		// Assert
		result.ShouldBe(5);
	}

	#endregion

	#region Offset Property Tests

	[Fact]
	public void Offset_Default_IsZero()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.Offset.ShouldBe(0);
	}

	[Fact]
	public void Offset_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.Offset = 12345L;

		// Assert
		context.Offset.ShouldBe(12345L);
	}

	[Fact]
	public void Offset_CanBeLargeValue()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.Offset = long.MaxValue;

		// Assert
		context.Offset.ShouldBe(long.MaxValue);
	}

	[Fact]
	public void Offset_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.Offset = 999L;

		// Act
		var result = context.GetTransportProperty<long>(KafkaMessageContext.OffsetPropertyName);

		// Assert
		result.ShouldBe(999L);
	}

	#endregion

	#region Key Property Tests

	[Fact]
	public void Key_Default_IsNull()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.Key.ShouldBeNull();
	}

	[Fact]
	public void Key_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.Key = "order-12345";

		// Assert
		context.Key.ShouldBe("order-12345");
	}

	[Fact]
	public void Key_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.Key = "my-key";

		// Act
		var result = context.GetTransportProperty<string>(KafkaMessageContext.KeyPropertyName);

		// Assert
		result.ShouldBe("my-key");
	}

	#endregion

	#region LeaderEpoch Property Tests

	[Fact]
	public void LeaderEpoch_Default_IsNull()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.LeaderEpoch.ShouldBeNull();
	}

	[Fact]
	public void LeaderEpoch_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.LeaderEpoch = 10;

		// Assert
		context.LeaderEpoch.ShouldBe(10);
	}

	[Fact]
	public void LeaderEpoch_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.LeaderEpoch = 15;

		// Act
		var result = context.GetTransportProperty<int?>(KafkaMessageContext.LeaderEpochPropertyName);

		// Assert
		result.ShouldBe(15);
	}

	#endregion

	#region SchemaId Property Tests

	[Fact]
	public void SchemaId_Default_IsNull()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		context.SchemaId.ShouldBeNull();
	}

	[Fact]
	public void SchemaId_CanBeSet()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.SchemaId = 42;

		// Assert
		context.SchemaId.ShouldBe(42);
	}

	[Fact]
	public void SchemaId_UsesTransportProperty()
	{
		// Arrange
		var context = new KafkaMessageContext();
		context.SchemaId = 100;

		// Act
		var result = context.GetTransportProperty<int?>(KafkaMessageContext.SchemaIdPropertyName);

		// Assert
		result.ShouldBe(100);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromTransportMessageContext()
	{
		// Arrange & Act
		var context = new KafkaMessageContext();

		// Assert
		_ = context.ShouldBeAssignableTo<TransportMessageContext>();
	}

	[Fact]
	public void InheritsHeaders()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.SetHeader("x-custom", "value");

		// Assert
		context.Headers["x-custom"].ShouldBe("value");
	}

	[Fact]
	public void InheritsCorrelationId()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.CorrelationId = "correlation-123";

		// Assert
		context.CorrelationId.ShouldBe("correlation-123");
	}

	[Fact]
	public void InheritsCausationId()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.CausationId = "causation-456";

		// Assert
		context.CausationId.ShouldBe("causation-456");
	}

	[Fact]
	public void InheritsContentType()
	{
		// Arrange
		var context = new KafkaMessageContext();

		// Act
		context.ContentType = "application/avro";

		// Assert
		context.ContentType.ShouldBe("application/avro");
	}

	[Fact]
	public void InheritsTimestamp()
	{
		// Arrange
		var context = new KafkaMessageContext();
		var timestamp = new DateTimeOffset(2025, 6, 15, 12, 0, 0, TimeSpan.Zero);

		// Act
		context.Timestamp = timestamp;

		// Assert
		context.Timestamp.ShouldBe(timestamp);
	}

	#endregion

	#region Combined Property Tests

	[Fact]
	public void AllPropertiesCanBeSetTogether()
	{
		// Arrange
		var context = new KafkaMessageContext("msg-123")
		{
			Topic = "orders",
			Partition = 3,
			Offset = 54321L,
			Key = "order-abc",
			LeaderEpoch = 5,
			SchemaId = 42,
			CorrelationId = "corr-id",
			CausationId = "cause-id",
			ContentType = "application/json",
		};

		// Assert
		context.MessageId.ShouldBe("msg-123");
		context.Topic.ShouldBe("orders");
		context.Partition.ShouldBe(3);
		context.Offset.ShouldBe(54321L);
		context.Key.ShouldBe("order-abc");
		context.LeaderEpoch.ShouldBe(5);
		context.SchemaId.ShouldBe(42);
		context.CorrelationId.ShouldBe("corr-id");
		context.CausationId.ShouldBe("cause-id");
		context.ContentType.ShouldBe("application/json");
		context.SourceTransport.ShouldBe("kafka");
	}

	#endregion
}
