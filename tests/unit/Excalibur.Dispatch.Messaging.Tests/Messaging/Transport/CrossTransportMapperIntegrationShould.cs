// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Integration tests for cross-transport message mapping.
/// Tests mappers working together with <see cref="MessageMapperRegistry"/> per Sprint 395 T395.7.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class CrossTransportMapperIntegrationShould
{
	#region Registry Registration Tests

	[Fact]
	public void RegisterDefaultMappers_RegisterBothCrossTransportMappers()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act
		_ = registry.RegisterDefaultMappers();

		// Assert
		registry.Count.ShouldBe(3); // RabbitMqToKafka, KafkaToRabbitMq, Default
	}

	[Fact]
	public void RegisterDefaultMappers_RegisterRabbitMqToKafkaMapper()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act
		_ = registry.RegisterDefaultMappers();

		// Assert
		var mapper = registry.GetMapper("rabbitmq", "kafka");
		_ = mapper.ShouldNotBeNull();
		mapper.Name.ShouldBe("RabbitMqToKafka");
	}

	[Fact]
	public void RegisterDefaultMappers_RegisterKafkaToRabbitMqMapper()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act
		_ = registry.RegisterDefaultMappers();

		// Assert
		var mapper = registry.GetMapper("kafka", "rabbitmq");
		_ = mapper.ShouldNotBeNull();
		mapper.Name.ShouldBe("KafkaToRabbitMq");
	}

	[Fact]
	public void RegisterDefaultMappers_RegisterDefaultFallbackMapper()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act
		_ = registry.RegisterDefaultMappers();

		// Assert
		var mapper = registry.GetMapper("servicebus", "pubsub");
		_ = mapper.ShouldNotBeNull();
		mapper.Name.ShouldBe("Default");
	}

	[Fact]
	public void RegisterDefaultMappers_PrioritizeSpecificOverDefault()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act
		_ = registry.RegisterDefaultMappers();

		// Assert - Specific mappers should be returned, not default
		registry.GetMapper("rabbitmq", "kafka").Name.ShouldBe("RabbitMqToKafka");
		registry.GetMapper("kafka", "rabbitmq").Name.ShouldBe("KafkaToRabbitMq");
		registry.GetMapper("unknown1", "unknown2").Name.ShouldBe("Default");
	}

	#endregion

	#region End-to-End Mapping via Registry Tests

	[Fact]
	public void MapViaRegistry_RabbitMqToKafkaSuccessfully()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var source = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.created",
			Priority = 5,
			Expiration = "60000",
			CorrelationId = "corr-1"
		};

		// Act
		var mapper = registry.GetMapper("rabbitmq", "kafka");
		var result = mapper.Map(source, "kafka") as KafkaMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.Key.ShouldBe("orders.created");
		result.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("5");
		result.Headers[RabbitMqToKafkaMapper.ExpirationHeader].ShouldBe("60000");
		result.CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public void MapViaRegistry_KafkaToRabbitMqSuccessfully()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var source = new KafkaMessageContext("msg-1")
		{
			Key = "user-12345",
			Topic = "users",
			CorrelationId = "corr-1"
		};
		source.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "3");

		// Act
		var mapper = registry.GetMapper("kafka", "rabbitmq");
		var result = mapper.Map(source, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = result.ShouldNotBeNull();
		result.RoutingKey.ShouldBe("user-12345");
		result.Priority.ShouldBe((byte)3);
		result.DeliveryMode.ShouldBe((byte)2);
		result.CorrelationId.ShouldBe("corr-1");
	}

	[Fact]
	public void MapViaRegistry_UseDefaultMapperForUnknownTransports()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var source = new TransportMessageContext("msg-1")
		{
			SourceTransport = "servicebus",
			CorrelationId = "corr-1"
		};
		source.SetHeader("custom-header", "custom-value");

		// Act
		var mapper = registry.GetMapper("servicebus", "pubsub");
		var result = mapper.Map(source, "pubsub");

		// Assert
		_ = result.ShouldNotBeNull();
		result.MessageId.ShouldBe("msg-1");
		result.CorrelationId.ShouldBe("corr-1");
		result.Headers.ShouldContainKey("custom-header");
	}

	#endregion

	#region Multi-Hop Mapping Tests

	[Fact]
	public void MultiHopMapping_RabbitMqToKafkaToRabbitMq()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var original = new RabbitMqMessageContext("msg-1")
		{
			RoutingKey = "orders.created",
			Priority = 7,
			Expiration = "30000",
			ReplyTo = "reply-queue",
			CorrelationId = "corr-1",
			CausationId = "cause-1"
		};

		// Act - Map through Kafka and back
		var rmqToKafka = registry.GetMapper("rabbitmq", "kafka");
		var kafkaToRmq = registry.GetMapper("kafka", "rabbitmq");

		var kafka = rmqToKafka.Map(original, "kafka");
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
	public void MultiHopMapping_KafkaToRabbitMqToKafka()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var original = new KafkaMessageContext("msg-1")
		{
			Key = "user-12345",
			Topic = "users",
			Partition = 3,
			Offset = 12345,
			CorrelationId = "corr-1"
		};
		original.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "5");
		original.SetHeader("custom-header", "custom-value");

		// Act - Map through RabbitMQ and back
		var kafkaToRmq = registry.GetMapper("kafka", "rabbitmq");
		var rmqToKafka = registry.GetMapper("rabbitmq", "kafka");

		var rmq = kafkaToRmq.Map(original, "rabbitmq");
		var restored = rmqToKafka.Map(rmq, "kafka") as KafkaMessageContext;

		// Assert
		_ = restored.ShouldNotBeNull();
		restored.Key.ShouldBe(original.Key);
		restored.CorrelationId.ShouldBe(original.CorrelationId);
		restored.Headers.ShouldContainKey("custom-header");
		restored.Headers["custom-header"].ShouldBe("custom-value");
		// Priority should round-trip via headers
		restored.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("5");
	}

	#endregion

	#region Case Insensitivity Tests

	[Theory]
	[InlineData("RabbitMQ", "Kafka")]
	[InlineData("RABBITMQ", "KAFKA")]
	[InlineData("rabbitmq", "kafka")]
	[InlineData("RabbitMq", "kafka")]
	public void GetMapper_BeCaseInsensitiveForRabbitMqToKafka(string source, string target)
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var mapper = registry.GetMapper(source, target);

		// Assert
		_ = mapper.ShouldNotBeNull();
		mapper.Name.ShouldBe("RabbitMqToKafka");
	}

	[Theory]
	[InlineData("Kafka", "RabbitMQ")]
	[InlineData("KAFKA", "RABBITMQ")]
	[InlineData("kafka", "rabbitmq")]
	[InlineData("kafka", "RabbitMq")]
	public void GetMapper_BeCaseInsensitiveForKafkaToRabbitMq(string source, string target)
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var mapper = registry.GetMapper(source, target);

		// Assert
		_ = mapper.ShouldNotBeNull();
		mapper.Name.ShouldBe("KafkaToRabbitMq");
	}

	#endregion

	#region HasMapper Tests

	[Fact]
	public void HasMapper_ReturnTrueForRabbitMqToKafka()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var result = registry.HasMapper("rabbitmq", "kafka");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasMapper_ReturnTrueForKafkaToRabbitMq()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var result = registry.HasMapper("kafka", "rabbitmq");

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasMapper_ReturnTrueForUnknownTransportsViaDefault()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var result = registry.HasMapper("unknown1", "unknown2");

		// Assert
		result.ShouldBeTrue(); // Default wildcard mapper handles all
	}

	#endregion

	#region GetAllMappers Tests

	[Fact]
	public void GetAllMappers_ReturnAllThreeMappers()
	{
		// Arrange
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		// Act
		var mappers = registry.GetAllMappers().ToList();

		// Assert
		mappers.Count.ShouldBe(3);
		mappers.Select(m => m.Name).ShouldContain("RabbitMqToKafka");
		mappers.Select(m => m.Name).ShouldContain("KafkaToRabbitMq");
		mappers.Select(m => m.Name).ShouldContain("Default");
	}

	#endregion

	#region Fluent Registration Tests

	[Fact]
	public void RegisterDefaultMappers_SupportFluentChaining()
	{
		// Arrange
		var registry = new MessageMapperRegistry();

		// Act - Should return same registry for chaining
		var result = registry.RegisterDefaultMappers();

		// Assert
		result.ShouldBeSameAs(registry);
	}

	#endregion

	#region Header Consistency Tests

	[Fact]
	public void HeaderConstants_BeConsistentBetweenMappers()
	{
		// Assert - Headers should match for round-trip compatibility
		RabbitMqToKafkaMapper.PriorityHeader.ShouldBe(KafkaToRabbitMqMapper.PriorityHeader);
		RabbitMqToKafkaMapper.ExpirationHeader.ShouldBe(KafkaToRabbitMqMapper.ExpirationHeader);
		RabbitMqToKafkaMapper.ReplyToHeader.ShouldBe(KafkaToRabbitMqMapper.ReplyToHeader);
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Scenario_OrderEventFromRabbitMqToKafkaForAnalytics()
	{
		// Arrange - Order created in RabbitMQ, forwarded to Kafka for analytics
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var orderEvent = new RabbitMqMessageContext("order-123")
		{
			RoutingKey = "orders.created",
			Exchange = "orders-exchange",
			Priority = 3, // Normal priority
			CorrelationId = "session-456",
			ContentType = "application/json"
		};
		orderEvent.SetHeader("x-order-id", "ORD-2024-001");
		orderEvent.SetHeader("x-customer-id", "CUST-789");

		// Act
		var mapper = registry.GetMapper("rabbitmq", "kafka");
		var kafkaEvent = mapper.Map(orderEvent, "kafka") as KafkaMessageContext;

		// Assert
		_ = kafkaEvent.ShouldNotBeNull();
		kafkaEvent.Key.ShouldBe("orders.created"); // For Kafka partitioning
		kafkaEvent.CorrelationId.ShouldBe("session-456");
		kafkaEvent.ContentType.ShouldBe("application/json");
		kafkaEvent.Headers["x-order-id"].ShouldBe("ORD-2024-001");
		kafkaEvent.Headers["x-customer-id"].ShouldBe("CUST-789");
		kafkaEvent.Headers[RabbitMqToKafkaMapper.PriorityHeader].ShouldBe("3");
	}

	[Fact]
	public void Scenario_KafkaStreamEventToRabbitMqForWorker()
	{
		// Arrange - Kafka stream event forwarded to RabbitMQ worker queue
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var streamEvent = new KafkaMessageContext("evt-987")
		{
			Key = "user-123",
			Topic = "user-events",
			Partition = 5,
			Offset = 9876543,
			CorrelationId = "stream-batch-42"
		};
		streamEvent.SetHeader(KafkaToRabbitMqMapper.PriorityHeader, "7"); // High priority
		streamEvent.SetHeader("x-event-type", "UserUpdated");

		// Act
		var mapper = registry.GetMapper("kafka", "rabbitmq");
		var rmqEvent = mapper.Map(streamEvent, "rabbitmq") as RabbitMqMessageContext;

		// Assert
		_ = rmqEvent.ShouldNotBeNull();
		rmqEvent.RoutingKey.ShouldBe("user-123"); // For RabbitMQ routing
		rmqEvent.Priority.ShouldBe((byte)7);
		rmqEvent.DeliveryMode.ShouldBe((byte)2); // Persistent
		rmqEvent.CorrelationId.ShouldBe("stream-batch-42");
		rmqEvent.Headers["x-event-type"].ShouldBe("UserUpdated");
	}

	[Fact]
	public void Scenario_RequestReplyPatternAcrossTransports()
	{
		// Arrange - Request from RabbitMQ, reply via Kafka
		var registry = new MessageMapperRegistry();
		_ = registry.RegisterDefaultMappers();

		var request = new RabbitMqMessageContext("req-001")
		{
			RoutingKey = "api.requests",
			ReplyTo = "responses.queue",
			CorrelationId = "req-corr-001",
			Expiration = "30000" // 30 second timeout
		};

		// Act - Forward to Kafka
		var toKafka = registry.GetMapper("rabbitmq", "kafka");
		var kafkaRequest = toKafka.Map(request, "kafka") as KafkaMessageContext;

		// Act - Response comes back via RabbitMQ
		var kafkaResponse = new KafkaMessageContext("resp-001")
		{
			Key = request.RoutingKey,
			CorrelationId = request.CorrelationId
		};
		kafkaResponse.SetHeader(KafkaToRabbitMqMapper.ReplyToHeader, "responses.queue");

		var toRmq = registry.GetMapper("kafka", "rabbitmq");
		var rmqResponse = toRmq.Map(kafkaResponse, "rabbitmq") as RabbitMqMessageContext;

		// Assert - Reply-to preserved for response routing
		kafkaRequest.Headers[RabbitMqToKafkaMapper.ReplyToHeader].ShouldBe("responses.queue");
		rmqResponse.ReplyTo.ShouldBe("responses.queue");
		rmqResponse.CorrelationId.ShouldBe("req-corr-001");
	}

	#endregion
}
