// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Configuration;
using Excalibur.Dispatch.Options.Core;

namespace Excalibur.Dispatch.Tests.Options.Core;

/// <summary>
/// Unit tests for <see cref="MultiTransportOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Options")]
[Trait("Priority", "0")]
public sealed class MultiTransportOptionsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_Transports_IsNotNull()
	{
		// Arrange & Act
		var options = new MultiTransportOptions();

		// Assert
		_ = options.Transports.ShouldNotBeNull();
	}

	[Fact]
	public void Default_Transports_IsEmpty()
	{
		// Arrange & Act
		var options = new MultiTransportOptions();

		// Assert
		options.Transports.ShouldBeEmpty();
	}

	[Fact]
	public void Default_DefaultTransport_IsNull()
	{
		// Arrange & Act
		var options = new MultiTransportOptions();

		// Assert
		options.DefaultTransport.ShouldBeNull();
	}

	[Fact]
	public void Default_EnableFailover_IsTrue()
	{
		// Arrange & Act
		var options = new MultiTransportOptions();

		// Assert
		options.EnableFailover.ShouldBeTrue();
	}

	#endregion

	#region Property Setter Tests

	[Fact]
	public void DefaultTransport_CanBeSet()
	{
		// Arrange
		var options = new MultiTransportOptions();

		// Act
		options.DefaultTransport = "rabbitmq";

		// Assert
		options.DefaultTransport.ShouldBe("rabbitmq");
	}

	[Fact]
	public void EnableFailover_CanBeSet()
	{
		// Arrange
		var options = new MultiTransportOptions();

		// Act
		options.EnableFailover = false;

		// Assert
		options.EnableFailover.ShouldBeFalse();
	}

	[Fact]
	public void Transports_CanAddConfiguration()
	{
		// Arrange
		var options = new MultiTransportOptions();
		var config = new TransportConfiguration { Name = "rabbitmq", Enabled = true };

		// Act
		options.Transports["rabbitmq"] = config;

		// Assert
		options.Transports.Count.ShouldBe(1);
		options.Transports["rabbitmq"].ShouldBeSameAs(config);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsScalarProperties()
	{
		// Act
		var options = new MultiTransportOptions
		{
			DefaultTransport = "kafka",
			EnableFailover = false,
		};

		// Assert
		options.DefaultTransport.ShouldBe("kafka");
		options.EnableFailover.ShouldBeFalse();
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void Options_ForMultipleTransports_HasMultipleConfigurations()
	{
		// Arrange
		var options = new MultiTransportOptions
		{
			DefaultTransport = "rabbitmq",
			EnableFailover = true,
		};

		// Act
		options.Transports["rabbitmq"] = new TransportConfiguration { Name = "rabbitmq", Enabled = true };
		options.Transports["kafka"] = new TransportConfiguration { Name = "kafka", Enabled = true };
		options.Transports["azure-servicebus"] = new TransportConfiguration { Name = "azure-servicebus", Enabled = false };

		// Assert
		options.Transports.Count.ShouldBe(3);
		options.Transports["rabbitmq"].Enabled.ShouldBeTrue();
		options.Transports["azure-servicebus"].Enabled.ShouldBeFalse();
	}

	[Fact]
	public void Options_ForPrimaryOnly_HasNoFailover()
	{
		// Arrange
		var options = new MultiTransportOptions
		{
			DefaultTransport = "rabbitmq",
			EnableFailover = false,
		};

		// Act
		options.Transports["rabbitmq"] = new TransportConfiguration { Name = "rabbitmq", Enabled = true };

		// Assert
		options.EnableFailover.ShouldBeFalse();
		options.Transports.Count.ShouldBe(1);
	}

	[Fact]
	public void Options_ForFailoverScenario_HasMultipleEnabledTransports()
	{
		// Arrange
		var options = new MultiTransportOptions
		{
			DefaultTransport = "rabbitmq",
			EnableFailover = true,
		};

		// Act
		options.Transports["rabbitmq"] = new TransportConfiguration { Name = "rabbitmq", Enabled = true };
		options.Transports["fallback-kafka"] = new TransportConfiguration { Name = "fallback-kafka", Enabled = true };

		// Assert
		options.EnableFailover.ShouldBeTrue();
		options.Transports.Values.Count(t => t.Enabled).ShouldBe(2);
	}

	#endregion
}
