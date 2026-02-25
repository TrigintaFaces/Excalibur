// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.MultiTransport;

namespace Excalibur.Outbox.Tests.MultiTransport;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiTransportOutboxOptionsShould
{
	[Fact]
	public void DefaultTransportBindingsToEmptyDictionary()
	{
		// Arrange & Act
		var options = new MultiTransportOutboxOptions();

		// Assert
		options.TransportBindings.ShouldNotBeNull();
		options.TransportBindings.ShouldBeEmpty();
	}

	[Fact]
	public void DefaultTransportToDefault()
	{
		// Arrange & Act
		var options = new MultiTransportOutboxOptions();

		// Assert
		options.DefaultTransport.ShouldBe("default");
	}

	[Fact]
	public void DefaultRequireExplicitBindingsToFalse()
	{
		// Arrange & Act
		var options = new MultiTransportOutboxOptions();

		// Assert
		options.RequireExplicitBindings.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingDefaultTransport()
	{
		// Arrange
		var options = new MultiTransportOutboxOptions();

		// Act
		options.DefaultTransport = "kafka";

		// Assert
		options.DefaultTransport.ShouldBe("kafka");
	}

	[Fact]
	public void AllowSettingRequireExplicitBindings()
	{
		// Arrange
		var options = new MultiTransportOutboxOptions();

		// Act
		options.RequireExplicitBindings = true;

		// Assert
		options.RequireExplicitBindings.ShouldBeTrue();
	}

	[Fact]
	public void AllowAddingTransportBindings()
	{
		// Arrange
		var options = new MultiTransportOutboxOptions();

		// Act
		options.TransportBindings["OrderCreated"] = "rabbitmq";
		options.TransportBindings["Payment*"] = "kafka";

		// Assert
		options.TransportBindings.Count.ShouldBe(2);
		options.TransportBindings["OrderCreated"].ShouldBe("rabbitmq");
		options.TransportBindings["Payment*"].ShouldBe("kafka");
	}

	[Fact]
	public void UseCaseInsensitiveTransportBindingsKeys()
	{
		// Arrange
		var options = new MultiTransportOutboxOptions();

		// Act
		options.TransportBindings["OrderCreated"] = "rabbitmq";

		// Assert — key lookup should be case-insensitive
		options.TransportBindings["ordercreated"].ShouldBe("rabbitmq");
		options.TransportBindings["ORDERCREATED"].ShouldBe("rabbitmq");
	}
}
