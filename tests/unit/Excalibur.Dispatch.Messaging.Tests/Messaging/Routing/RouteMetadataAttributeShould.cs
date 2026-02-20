// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Routing;

namespace Excalibur.Dispatch.Tests.Messaging.Routing;

/// <summary>
/// Unit tests for <see cref="RouteMetadataAttribute"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Routing")]
[Trait("Priority", "0")]
public sealed class RouteMetadataAttributeShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithBusName_SetsBusNameProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: "kafka-primary");

		// Assert
		attribute.BusName.ShouldBe("kafka-primary");
	}

	[Fact]
	public void Constructor_WithNullBusName_SetsBusNameToNull()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null);

		// Assert
		attribute.BusName.ShouldBeNull();
	}

	[Fact]
	public void Constructor_WithForceRemoteTrue_SetsForceRemoteProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, forceRemote: true);

		// Assert
		attribute.ForceRemote.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithDefaultForceRemote_SetsForceRemoteToFalse()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null);

		// Assert
		attribute.ForceRemote.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithRoutingKey_SetsRoutingKeyProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, routingKey: "orders.created");

		// Assert
		attribute.RoutingKey.ShouldBe("orders.created");
	}

	[Fact]
	public void Constructor_WithActivityName_SetsActivityNameProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, activityName: "ProcessOrder");

		// Assert
		attribute.ActivityName.ShouldBe("ProcessOrder");
	}

	[Fact]
	public void Constructor_WithQueueName_SetsQueueNameProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, queueName: "orders-processing");

		// Assert
		attribute.QueueName.ShouldBe("orders-processing");
	}

	[Fact]
	public void Constructor_WithTopicName_SetsTopicNameProperty()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, topicName: "orders.events");

		// Assert
		attribute.TopicName.ShouldBe("orders.events");
	}

	[Fact]
	public void Constructor_WithAllParameters_SetsAllProperties()
	{
		// Act
		var attribute = new RouteMetadataAttribute(
			busName: "rabbitmq",
			forceRemote: true,
			routingKey: "orders.#",
			activityName: "OrderProcessing",
			queueName: "orders-queue",
			topicName: "orders-topic");

		// Assert
		attribute.BusName.ShouldBe("rabbitmq");
		attribute.ForceRemote.ShouldBeTrue();
		attribute.RoutingKey.ShouldBe("orders.#");
		attribute.ActivityName.ShouldBe("OrderProcessing");
		attribute.QueueName.ShouldBe("orders-queue");
		attribute.TopicName.ShouldBe("orders-topic");
	}

	#endregion

	#region IRouteMetadata Interface Tests

	[Fact]
	public void ImplementsIRouteMetadata()
	{
		// Arrange
		var attribute = new RouteMetadataAttribute(busName: "test");

		// Assert
		_ = attribute.ShouldBeAssignableTo<IRouteMetadata>();
	}

	[Fact]
	public void TargetBus_ReturnsBusName()
	{
		// Arrange
		var attribute = new RouteMetadataAttribute(busName: "kafka");
		IRouteMetadata metadata = attribute;

		// Assert
		metadata.TargetBus.ShouldBe("kafka");
	}

	[Fact]
	public void TargetBus_WithNullBusName_ReturnsNull()
	{
		// Arrange
		var attribute = new RouteMetadataAttribute(busName: null);
		IRouteMetadata metadata = attribute;

		// Assert
		metadata.TargetBus.ShouldBeNull();
	}

	#endregion

	#region Attribute Usage Tests

	[Fact]
	public void AttributeUsage_AllowsOnlyClasses()
	{
		// Arrange
		var attributeUsage = typeof(RouteMetadataAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.ValidOn.ShouldBe(AttributeTargets.Class);
	}

	[Fact]
	public void AttributeUsage_IsNotInherited()
	{
		// Arrange
		var attributeUsage = typeof(RouteMetadataAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.FirstOrDefault() as AttributeUsageAttribute;

		// Assert
		_ = attributeUsage.ShouldNotBeNull();
		attributeUsage.Inherited.ShouldBeFalse();
	}

	[Fact]
	public void InheritsFromAttribute()
	{
		// Arrange
		var attribute = new RouteMetadataAttribute(busName: null);

		// Assert
		_ = attribute.ShouldBeAssignableTo<Attribute>();
	}

	#endregion

	#region Init Properties Tests

	[Fact]
	public void BusName_CanBeOverriddenWithInit()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: "original")
		{
			BusName = "overridden",
		};

		// Assert
		attribute.BusName.ShouldBe("overridden");
	}

	[Fact]
	public void ForceRemote_CanBeOverriddenWithInit()
	{
		// Act
		var attribute = new RouteMetadataAttribute(busName: null, forceRemote: false)
		{
			ForceRemote = true,
		};

		// Assert
		attribute.ForceRemote.ShouldBeTrue();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Attribute_ForKafkaMessage_HasCorrectConfiguration()
	{
		// Act
		var attribute = new RouteMetadataAttribute(
			busName: "kafka",
			forceRemote: true,
			topicName: "events.orders.created",
			activityName: "PublishOrderCreated");

		// Assert
		attribute.BusName.ShouldBe("kafka");
		attribute.ForceRemote.ShouldBeTrue();
		attribute.TopicName.ShouldBe("events.orders.created");
		attribute.ActivityName.ShouldBe("PublishOrderCreated");
	}

	[Fact]
	public void Attribute_ForRabbitMQMessage_HasCorrectConfiguration()
	{
		// Act
		var attribute = new RouteMetadataAttribute(
			busName: "rabbitmq",
			routingKey: "orders.*.created",
			queueName: "order-handlers");

		// Assert
		attribute.BusName.ShouldBe("rabbitmq");
		attribute.RoutingKey.ShouldBe("orders.*.created");
		attribute.QueueName.ShouldBe("order-handlers");
	}

	#endregion
}
