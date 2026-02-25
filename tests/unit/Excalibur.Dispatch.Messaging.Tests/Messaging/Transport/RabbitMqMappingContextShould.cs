// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Transport;
using Excalibur.Dispatch.Transport;

namespace Excalibur.Dispatch.Tests.Messaging.Transport;

/// <summary>
/// Unit tests for <see cref="RabbitMqMappingContext"/>.
/// </summary>
/// <remarks>
/// Tests the RabbitMQ mapping context implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
[Trait("Priority", "0")]
public sealed class RabbitMqMappingContextShould
{
	#region Constructor Tests

	[Fact]
	public void Default_HasNullExchange()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.Exchange.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullRoutingKey()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.RoutingKey.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullPriority()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.Priority.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullReplyTo()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.ReplyTo.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullExpiration()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.Expiration.ShouldBeNull();
	}

	[Fact]
	public void Default_HasNullDeliveryMode()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.DeliveryMode.ShouldBeNull();
	}

	[Fact]
	public void Default_HasEmptyHeaders()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		context.Headers.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void Exchange_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.Exchange = "my-exchange";

		// Assert
		context.Exchange.ShouldBe("my-exchange");
	}

	[Fact]
	public void RoutingKey_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.RoutingKey = "orders.created";

		// Assert
		context.RoutingKey.ShouldBe("orders.created");
	}

	[Fact]
	public void Priority_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.Priority = 5;

		// Assert
		context.Priority.ShouldBe((byte)5);
	}

	[Fact]
	public void ReplyTo_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.ReplyTo = "reply-queue";

		// Assert
		context.ReplyTo.ShouldBe("reply-queue");
	}

	[Fact]
	public void Expiration_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.Expiration = "60000"; // 60 seconds in milliseconds

		// Assert
		context.Expiration.ShouldBe("60000");
	}

	[Fact]
	public void DeliveryMode_CanBeSet()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.DeliveryMode = 2; // Persistent

		// Assert
		context.DeliveryMode.ShouldBe((byte)2);
	}

	#endregion

	#region SetHeader Tests

	[Fact]
	public void SetHeader_AddsHeader()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.SetHeader("x-custom-header", "custom-value");

		// Assert
		context.Headers.ShouldContainKey("x-custom-header");
		context.Headers["x-custom-header"].ShouldBe("custom-value");
	}

	[Fact]
	public void SetHeader_WithSameKey_OverwritesValue()
	{
		// Arrange
		var context = new RabbitMqMappingContext();
		context.SetHeader("x-header", "value1");

		// Act
		context.SetHeader("x-header", "value2");

		// Assert
		context.Headers["x-header"].ShouldBe("value2");
		context.Headers.Count.ShouldBe(1);
	}

	[Fact]
	public void SetHeader_IsCaseInsensitive()
	{
		// Arrange
		var context = new RabbitMqMappingContext();
		context.SetHeader("X-Custom-Header", "value1");

		// Act
		context.SetHeader("x-custom-header", "value2");

		// Assert
		context.Headers.Count.ShouldBe(1);
		context.Headers["X-CUSTOM-HEADER"].ShouldBe("value2");
	}

	[Fact]
	public void SetHeader_WithNullKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(null!, "value"));
	}

	[Fact]
	public void SetHeader_WithEmptyKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader(string.Empty, "value"));
	}

	[Fact]
	public void SetHeader_WithWhitespaceKey_ThrowsArgumentException()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => context.SetHeader("   ", "value"));
	}

	[Fact]
	public void SetHeader_CanAddMultipleHeaders()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act
		context.SetHeader("header1", "value1");
		context.SetHeader("header2", "value2");
		context.SetHeader("header3", "value3");

		// Assert
		context.Headers.Count.ShouldBe(3);
		context.Headers["header1"].ShouldBe("value1");
		context.Headers["header2"].ShouldBe("value2");
		context.Headers["header3"].ShouldBe("value3");
	}

	#endregion

	#region ApplyTo Tests

	[Fact]
	public void ApplyTo_WithNullContext_ThrowsArgumentNullException()
	{
		// Arrange
		var context = new RabbitMqMappingContext();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => context.ApplyTo(null!));
	}

	[Fact]
	public void ApplyTo_AppliesExchange()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { Exchange = "test-exchange" };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Exchange.ShouldBe("test-exchange");
	}

	[Fact]
	public void ApplyTo_AppliesRoutingKey()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { RoutingKey = "test.routing.key" };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.RoutingKey.ShouldBe("test.routing.key");
	}

	[Fact]
	public void ApplyTo_AppliesPriority()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { Priority = 9 };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Priority.ShouldBe((byte)9);
	}

	[Fact]
	public void ApplyTo_AppliesReplyTo()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { ReplyTo = "reply-to-queue" };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.ReplyTo.ShouldBe("reply-to-queue");
	}

	[Fact]
	public void ApplyTo_AppliesExpiration()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { Expiration = "30000" };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Expiration.ShouldBe("30000");
	}

	[Fact]
	public void ApplyTo_AppliesDeliveryMode()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext { DeliveryMode = 2 };
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.DeliveryMode.ShouldBe((byte)2);
	}

	[Fact]
	public void ApplyTo_AppliesHeaders()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext();
		mappingContext.SetHeader("x-custom", "custom-value");
		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Headers.ShouldContainKey("x-custom");
		messageContext.Headers["x-custom"].ShouldBe("custom-value");
	}

	[Fact]
	public void ApplyTo_DoesNotApplyNullValues()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext(); // All values are null by default
		var messageContext = new RabbitMqMessageContext { Exchange = "existing-exchange" };

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert - Original value should be preserved
		messageContext.Exchange.ShouldBe("existing-exchange");
	}

	[Fact]
	public void ApplyTo_AppliesAllConfiguredValues()
	{
		// Arrange
		var mappingContext = new RabbitMqMappingContext
		{
			Exchange = "my-exchange",
			RoutingKey = "my.routing.key",
			Priority = 5,
			ReplyTo = "reply-queue",
			Expiration = "60000",
			DeliveryMode = 2,
		};
		mappingContext.SetHeader("header1", "value1");
		mappingContext.SetHeader("header2", "value2");

		var messageContext = new RabbitMqMessageContext();

		// Act
		mappingContext.ApplyTo(messageContext);

		// Assert
		messageContext.Exchange.ShouldBe("my-exchange");
		messageContext.RoutingKey.ShouldBe("my.routing.key");
		messageContext.Priority.ShouldBe((byte)5);
		messageContext.ReplyTo.ShouldBe("reply-queue");
		messageContext.Expiration.ShouldBe("60000");
		messageContext.DeliveryMode.ShouldBe((byte)2);
		messageContext.Headers["header1"].ShouldBe("value1");
		messageContext.Headers["header2"].ShouldBe("value2");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIRabbitMqMappingContext()
	{
		// Arrange & Act
		var context = new RabbitMqMappingContext();

		// Assert
		_ = context.ShouldBeAssignableTo<IRabbitMqMappingContext>();
	}

	#endregion
}
