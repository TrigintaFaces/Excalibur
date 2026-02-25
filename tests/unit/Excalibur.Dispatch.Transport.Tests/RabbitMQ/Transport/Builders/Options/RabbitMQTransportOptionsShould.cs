// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders.Options;

/// <summary>
/// Unit tests for <see cref="RabbitMQTransportOptions"/> and its sub-option classes
/// <see cref="RabbitMQConnectionOptions"/> and <see cref="RabbitMQTopologyOptions"/>.
/// Validates the S554.14 ISP split: root (7 props), Connection (8 props), Topology (7 props).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport.RabbitMQ")]
public sealed class RabbitMQTransportOptionsShould : UnitTestBase
{
	#region Default Values

	[Fact]
	public void HaveCorrectDefaultValues_Root()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();

		// Assert - Root properties
		options.Name.ShouldBe("rabbitmq");
		options.EnableDeadLetter.ShouldBeFalse();
		_ = options.Connection.ShouldNotBeNull();
		_ = options.Topology.ShouldNotBeNull();
		_ = options.DeadLetter.ShouldNotBeNull();
		_ = options.CloudEvents.ShouldNotBeNull();
		_ = options.AdditionalConfig.ShouldNotBeNull();
		options.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void HaveCorrectDefaultValues_Connection()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();

		// Assert - Connection sub-option defaults
		options.Connection.HostName.ShouldBe("localhost");
		options.Connection.Port.ShouldBe(5672);
		options.Connection.VirtualHost.ShouldBe("/");
		options.Connection.Username.ShouldBe("guest");
		options.Connection.Password.ShouldBe("guest");
		options.Connection.ConnectionString.ShouldBeNull();
		options.Connection.UseSsl.ShouldBeFalse();
		_ = options.Connection.Ssl.ShouldNotBeNull();
	}

	[Fact]
	public void HaveCorrectDefaultValues_Topology()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();

		// Assert - Topology sub-option defaults
		_ = options.Topology.Exchanges.ShouldNotBeNull();
		options.Topology.Exchanges.ShouldBeEmpty();
		_ = options.Topology.Queues.ShouldNotBeNull();
		options.Topology.Queues.ShouldBeEmpty();
		_ = options.Topology.Bindings.ShouldNotBeNull();
		options.Topology.Bindings.ShouldBeEmpty();
		_ = options.Topology.ExchangeMappings.ShouldNotBeNull();
		options.Topology.ExchangeMappings.ShouldBeEmpty();
		_ = options.Topology.QueueMappings.ShouldNotBeNull();
		options.Topology.QueueMappings.ShouldBeEmpty();
		options.Topology.ExchangePrefix.ShouldBeNull();
		options.Topology.QueuePrefix.ShouldBeNull();
	}

	#endregion

	#region Connection Properties

	[Fact]
	public void AllowSettingConnectionProperties()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();

		// Act
		options.Connection.HostName = "rabbitmq.example.com";
		options.Connection.Port = 5673;
		options.Connection.VirtualHost = "/production";
		options.Connection.Username = "admin";
		options.Connection.Password = "s3cret";
		options.Connection.ConnectionString = "amqp://admin:s3cret@rabbitmq.example.com:5673/production";
		options.Connection.UseSsl = true;

		// Assert
		options.Connection.HostName.ShouldBe("rabbitmq.example.com");
		options.Connection.Port.ShouldBe(5673);
		options.Connection.VirtualHost.ShouldBe("/production");
		options.Connection.Username.ShouldBe("admin");
		options.Connection.Password.ShouldBe("s3cret");
		options.Connection.ConnectionString.ShouldBe("amqp://admin:s3cret@rabbitmq.example.com:5673/production");
		options.Connection.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingSslProperties()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();

		// Act
		options.Connection.Ssl.ServerName = "rabbitmq.example.com";
		options.Connection.Ssl.CertificatePath = "/certs/client.pfx";
		options.Connection.Ssl.CertificatePassphrase = "cert-pass";
		options.Connection.Ssl.AcceptUntrustedCertificates = true;

		// Assert
		options.Connection.Ssl.ServerName.ShouldBe("rabbitmq.example.com");
		options.Connection.Ssl.CertificatePath.ShouldBe("/certs/client.pfx");
		options.Connection.Ssl.CertificatePassphrase.ShouldBe("cert-pass");
		options.Connection.Ssl.AcceptUntrustedCertificates.ShouldBeTrue();
	}

	#endregion

	#region Topology Properties

	[Fact]
	public void AllowSettingTopologyProperties()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();

		// Act
		options.Topology.ExchangePrefix = "myapp.";
		options.Topology.QueuePrefix = "myapp.";
		options.Topology.Exchanges.Add(new RabbitMQExchangeOptions { Name = "events" });
		options.Topology.Queues.Add(new RabbitMQQueueOptions { Name = "orders" });
		options.Topology.Bindings.Add(new RabbitMQBindingOptions
		{
			Exchange = "events",
			Queue = "orders",
			RoutingKey = "order.#",
		});
		options.Topology.ExchangeMappings[typeof(string)] = "string-exchange";
		options.Topology.QueueMappings[typeof(int)] = "int-queue";

		// Assert
		options.Topology.ExchangePrefix.ShouldBe("myapp.");
		options.Topology.QueuePrefix.ShouldBe("myapp.");
		options.Topology.Exchanges.Count.ShouldBe(1);
		options.Topology.Exchanges[0].Name.ShouldBe("events");
		options.Topology.Queues.Count.ShouldBe(1);
		options.Topology.Queues[0].Name.ShouldBe("orders");
		options.Topology.Bindings.Count.ShouldBe(1);
		options.Topology.Bindings[0].Exchange.ShouldBe("events");
		options.Topology.Bindings[0].Queue.ShouldBe("orders");
		options.Topology.Bindings[0].RoutingKey.ShouldBe("order.#");
		options.Topology.ExchangeMappings[typeof(string)].ShouldBe("string-exchange");
		options.Topology.QueueMappings[typeof(int)].ShouldBe("int-queue");
	}

	#endregion

	#region Non-Null Sub-Options

	[Fact]
	public void HaveNonNullSubOptions()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();

		// Assert - All sub-option objects must be instantiated by default
		_ = options.Connection.ShouldNotBeNull();
		_ = options.Topology.ShouldNotBeNull();
		_ = options.DeadLetter.ShouldNotBeNull();
		_ = options.CloudEvents.ShouldNotBeNull();
		_ = options.AdditionalConfig.ShouldNotBeNull();
		_ = options.Connection.Ssl.ShouldNotBeNull();
	}

	[Fact]
	public void HaveNonNullCollectionsInTopology()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();

		// Assert - All collection properties in Topology must be non-null
		_ = options.Topology.Exchanges.ShouldNotBeNull();
		_ = options.Topology.Queues.ShouldNotBeNull();
		_ = options.Topology.Bindings.ShouldNotBeNull();
		_ = options.Topology.ExchangeMappings.ShouldNotBeNull();
		_ = options.Topology.QueueMappings.ShouldNotBeNull();
	}

	#endregion

	#region Nested Initializer Syntax

	[Fact]
	public void SupportNestedInitializerSyntax_Connection()
	{
		// Arrange & Act
		// This verifies that the get-only sub-options support nested object initializer syntax.
		// The Connection property is get-only (no setter) but the instance is pre-created,
		// allowing { Connection = { HostName = "test" } } style initialization.
		var options = new RabbitMQTransportOptions
		{
			Name = "custom-rabbitmq",
			EnableDeadLetter = true,
		};

		// Nested property assignment works because Connection is pre-initialized
		options.Connection.HostName = "rabbitmq-cluster";
		options.Connection.Port = 5673;
		options.Connection.UseSsl = true;

		// Assert
		options.Name.ShouldBe("custom-rabbitmq");
		options.EnableDeadLetter.ShouldBeTrue();
		options.Connection.HostName.ShouldBe("rabbitmq-cluster");
		options.Connection.Port.ShouldBe(5673);
		options.Connection.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void SupportNestedInitializerSyntax_Topology()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();
		options.Topology.ExchangePrefix = "app.";
		options.Topology.QueuePrefix = "app.";

		// Assert
		options.Topology.ExchangePrefix.ShouldBe("app.");
		options.Topology.QueuePrefix.ShouldBe("app.");
	}

	[Fact]
	public void SupportNestedInitializerSyntax_DeadLetter()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions
		{
			EnableDeadLetter = true,
		};

		options.DeadLetter.Exchange = "custom-dlx";
		options.DeadLetter.Queue = "custom-dlq";
		options.DeadLetter.RoutingKey = "dead.#";
		options.DeadLetter.MaxRetries = 5;
		options.DeadLetter.RetryDelay = TimeSpan.FromMinutes(1);

		// Assert
		options.EnableDeadLetter.ShouldBeTrue();
		options.DeadLetter.Exchange.ShouldBe("custom-dlx");
		options.DeadLetter.Queue.ShouldBe("custom-dlq");
		options.DeadLetter.RoutingKey.ShouldBe("dead.#");
		options.DeadLetter.MaxRetries.ShouldBe(5);
		options.DeadLetter.RetryDelay.ShouldBe(TimeSpan.FromMinutes(1));
	}

	[Fact]
	public void SupportAdditionalConfigEntries()
	{
		// Arrange & Act
		var options = new RabbitMQTransportOptions();
		options.AdditionalConfig["custom-key"] = "custom-value";
		options.AdditionalConfig["another-key"] = "another-value";

		// Assert
		options.AdditionalConfig.Count.ShouldBe(2);
		options.AdditionalConfig["custom-key"].ShouldBe("custom-value");
		options.AdditionalConfig["another-key"].ShouldBe("another-value");
	}

	#endregion

	#region Property Count Validation (ISP Gate)

	[Fact]
	public void RootOptions_HaveAtMostSevenProperties()
	{
		// Verify the ISP split keeps root options at the documented 7 properties.
		// Properties: Name, Connection, Topology, DeadLetter, EnableDeadLetter, CloudEvents, AdditionalConfig
		var properties = typeof(RabbitMQTransportOptions).GetProperties();
		properties.Length.ShouldBeLessThanOrEqualTo(7,
			$"RabbitMQTransportOptions has {properties.Length} properties; ISP gate is 7 (Name, Connection, Topology, DeadLetter, EnableDeadLetter, CloudEvents, AdditionalConfig)");
	}

	[Fact]
	public void ConnectionOptions_HaveAtMostTenProperties()
	{
		// Verify Connection sub-options stay within the 10-property gate.
		// Properties: HostName, Port, VirtualHost, Username, Password, ConnectionString, UseSsl, Ssl
		var properties = typeof(RabbitMQConnectionOptions).GetProperties();
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RabbitMQConnectionOptions has {properties.Length} properties; ISP gate is 10");
	}

	[Fact]
	public void TopologyOptions_HaveAtMostTenProperties()
	{
		// Verify Topology sub-options stay within the 10-property gate.
		// Properties: Exchanges, Queues, Bindings, ExchangeMappings, QueueMappings, ExchangePrefix, QueuePrefix
		var properties = typeof(RabbitMQTopologyOptions).GetProperties();
		properties.Length.ShouldBeLessThanOrEqualTo(10,
			$"RabbitMQTopologyOptions has {properties.Length} properties; ISP gate is 10");
	}

	#endregion
}
