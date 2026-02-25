// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Options.Transport;

namespace Excalibur.Dispatch.Tests.Options.Transport;

/// <summary>
/// Unit tests for <see cref="RabbitMQOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class RabbitMQOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_VirtualHost_IsSlash()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.VirtualHost.ShouldBe("/");
	}

	[Fact]
	public void Default_PrefetchCount_Is100()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.PrefetchCount.ShouldBe((ushort)100);
	}

	[Fact]
	public void Default_AutoAck_IsFalse()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.AutoAck.ShouldBeFalse();
	}

	[Fact]
	public void Default_Durable_IsTrue()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.Durable.ShouldBeTrue();
	}

	[Fact]
	public void Default_Exchange_IsEmpty()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.Exchange.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_RoutingKey_IsEmpty()
	{
		// Arrange & Act
		var options = new RabbitMQOptions();

		// Assert
		options.RoutingKey.ShouldBe(string.Empty);
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void VirtualHost_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.VirtualHost = "/myapp";

		// Assert
		options.VirtualHost.ShouldBe("/myapp");
	}

	[Fact]
	public void PrefetchCount_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.PrefetchCount = 50;

		// Assert
		options.PrefetchCount.ShouldBe((ushort)50);
	}

	[Fact]
	public void AutoAck_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.AutoAck = true;

		// Assert
		options.AutoAck.ShouldBeTrue();
	}

	[Fact]
	public void Durable_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.Durable = false;

		// Assert
		options.Durable.ShouldBeFalse();
	}

	[Fact]
	public void Exchange_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.Exchange = "my-exchange";

		// Assert
		options.Exchange.ShouldBe("my-exchange");
	}

	[Fact]
	public void RoutingKey_CanBeSet()
	{
		// Arrange
		var options = new RabbitMQOptions();

		// Act
		options.RoutingKey = "orders.created";

		// Assert
		options.RoutingKey.ShouldBe("orders.created");
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var options = new RabbitMQOptions
		{
			VirtualHost = "/production",
			PrefetchCount = 200,
			AutoAck = true,
			Durable = false,
			Exchange = "events",
			RoutingKey = "domain.events",
		};

		// Assert
		options.VirtualHost.ShouldBe("/production");
		options.PrefetchCount.ShouldBe((ushort)200);
		options.AutoAck.ShouldBeTrue();
		options.Durable.ShouldBeFalse();
		options.Exchange.ShouldBe("events");
		options.RoutingKey.ShouldBe("domain.events");
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForHighThroughput_HasHighPrefetchCount()
	{
		// Act
		var options = new RabbitMQOptions
		{
			PrefetchCount = 500,
			AutoAck = false,
		};

		// Assert
		options.PrefetchCount.ShouldBeGreaterThan((ushort)100);
		options.AutoAck.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForDurableMessaging_EnablesDurable()
	{
		// Act
		var options = new RabbitMQOptions
		{
			Durable = true,
			AutoAck = false,
		};

		// Assert
		options.Durable.ShouldBeTrue();
		options.AutoAck.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForTopicExchange_SetsExchangeAndRoutingKey()
	{
		// Act
		var options = new RabbitMQOptions
		{
			Exchange = "topic.events",
			RoutingKey = "user.#",
		};

		// Assert
		options.Exchange.ShouldNotBeEmpty();
		options.RoutingKey.ShouldContain("user");
	}

	#endregion
}
