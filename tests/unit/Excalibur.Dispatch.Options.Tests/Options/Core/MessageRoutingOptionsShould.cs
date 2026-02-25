// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="MessageRoutingOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MessageRoutingOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_MessageTypeRouting_IsNotNull()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		_ = options.MessageTypeRouting.ShouldNotBeNull();
	}

	[Fact]
	public void Default_MessageTypeRouting_IsEmpty()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		options.MessageTypeRouting.ShouldBeEmpty();
	}

	[Fact]
	public void Default_DefaultRoutingPattern_IsMessageType()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		options.DefaultRoutingPattern.ShouldBe("{MessageType}");
	}

	[Fact]
	public void Default_UseMessageTypeAsRoutingKey_IsTrue()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		options.UseMessageTypeAsRoutingKey.ShouldBeTrue();
	}

	[Fact]
	public void Default_RoutingKeyGenerators_IsNotNull()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		_ = options.RoutingKeyGenerators.ShouldNotBeNull();
	}

	[Fact]
	public void Default_RoutingKeyGenerators_IsEmpty()
	{
		// Arrange & Act
		var options = new MessageRoutingOptions();

		// Assert
		options.RoutingKeyGenerators.ShouldBeEmpty();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultRoutingPattern_CanBeSet()
	{
		// Arrange
		var options = new MessageRoutingOptions();

		// Act
		options.DefaultRoutingPattern = "{Namespace}.{MessageType}";

		// Assert
		options.DefaultRoutingPattern.ShouldBe("{Namespace}.{MessageType}");
	}

	[Fact]
	public void UseMessageTypeAsRoutingKey_CanBeSet()
	{
		// Arrange
		var options = new MessageRoutingOptions();

		// Act
		options.UseMessageTypeAsRoutingKey = false;

		// Assert
		options.UseMessageTypeAsRoutingKey.ShouldBeFalse();
	}

	[Fact]
	public void MessageTypeRouting_CanAddMappings()
	{
		// Arrange
		var options = new MessageRoutingOptions();

		// Act
		options.MessageTypeRouting["OrderCreated"] = "orders-topic";
		options.MessageTypeRouting["OrderUpdated"] = "orders-topic";

		// Assert
		options.MessageTypeRouting.Count.ShouldBe(2);
		options.MessageTypeRouting["OrderCreated"].ShouldBe("orders-topic");
	}

	[Fact]
	public void RoutingKeyGenerators_CanAddGenerator()
	{
		// Arrange
		var options = new MessageRoutingOptions();
		Func<object, string> generator = msg => "custom-key";

		// Act
		options.RoutingKeyGenerators["OrderCreated"] = generator;

		// Assert
		options.RoutingKeyGenerators.Count.ShouldBe(1);
		options.RoutingKeyGenerators["OrderCreated"].ShouldBeSameAs(generator);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new MessageRoutingOptions
		{
			DefaultRoutingPattern = "custom-{MessageType}",
			UseMessageTypeAsRoutingKey = false,
		};

		// Assert
		options.DefaultRoutingPattern.ShouldBe("custom-{MessageType}");
		options.UseMessageTypeAsRoutingKey.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForMultiTopicRouting_HasMappings()
	{
		// Arrange
		var options = new MessageRoutingOptions
		{
			UseMessageTypeAsRoutingKey = true,
		};

		// Act
		options.MessageTypeRouting["OrderCreated"] = "orders";
		options.MessageTypeRouting["PaymentReceived"] = "payments";
		options.MessageTypeRouting["ShipmentDispatched"] = "shipments";

		// Assert
		options.MessageTypeRouting.Count.ShouldBe(3);
		options.MessageTypeRouting.ContainsKey("OrderCreated").ShouldBeTrue();
	}

	[Fact]
	public void Options_ForCustomRoutingKeys_HasGenerator()
	{
		// Arrange
		var options = new MessageRoutingOptions
		{
			UseMessageTypeAsRoutingKey = false,
		};

		// Act
		options.RoutingKeyGenerators["OrderCreated"] = msg =>
		{
			// In real usage, extract a key from the message
			return "order-key";
		};

		// Assert
		options.RoutingKeyGenerators.Count.ShouldBe(1);
		var key = options.RoutingKeyGenerators["OrderCreated"](new object());
		key.ShouldBe("order-key");
	}

	[Fact]
	public void Options_ForPatternBasedRouting_HasCustomPattern()
	{
		// Act
		var options = new MessageRoutingOptions
		{
			DefaultRoutingPattern = "{Namespace}/{MessageType}",
			UseMessageTypeAsRoutingKey = true,
		};

		// Assert
		options.DefaultRoutingPattern.ShouldContain("{Namespace}");
		options.DefaultRoutingPattern.ShouldContain("{MessageType}");
	}

	#endregion
}
