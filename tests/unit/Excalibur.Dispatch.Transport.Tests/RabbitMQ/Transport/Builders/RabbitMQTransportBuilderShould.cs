// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.RabbitMQ;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.RabbitMQ.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IRabbitMQTransportBuilder"/>.
/// Part of S473.2 - AddRabbitMQTransport single entry point (Sprint 473).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class RabbitMQTransportBuilderShould : UnitTestBase
{
	#region AddRabbitMQTransport Tests

	[Fact]
	public void AddRabbitMQTransport_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			RabbitMQTransportServiceCollectionExtensions.AddRabbitMQTransport(
				null!, "test", _ => { }));
	}

	[Fact]
	public void AddRabbitMQTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddRabbitMQTransport(null!, _ => { }));
	}

	[Fact]
	public void AddRabbitMQTransport_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddRabbitMQTransport("", _ => { }));
	}

	[Fact]
	public void AddRabbitMQTransport_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddRabbitMQTransport("test", null!));
	}

	[Fact]
	public void AddRabbitMQTransport_InvokeConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var callbackInvoked = false;

		// Act
		_ = services.AddRabbitMQTransport("test", _ =>
		{
			callbackInvoked = true;
		});

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	#endregion

	#region HostName Tests

	[Fact]
	public void HostName_ThrowWhenHostNameIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.HostName(null!));
	}

	[Fact]
	public void HostName_ThrowWhenHostNameIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.HostName(""));
	}

	[Fact]
	public void HostName_SetHostNameInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.HostName("rabbitmq.example.com");

		// Assert
		options.Connection.HostName.ShouldBe("rabbitmq.example.com");
	}

	[Fact]
	public void HostName_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.HostName("localhost");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Port Tests

	[Fact]
	public void Port_ThrowWhenPortIsZero()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.Port(0));
	}

	[Fact]
	public void Port_ThrowWhenPortIsNegative()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.Port(-1));
	}

	[Fact]
	public void Port_ThrowWhenPortExceedsMax()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => builder.Port(65536));
	}

	[Fact]
	public void Port_SetPortInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.Port(5672);

		// Assert
		options.Connection.Port.ShouldBe(5672);
	}

	[Fact]
	public void Port_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.Port(5672);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region VirtualHost Tests

	[Fact]
	public void VirtualHost_ThrowWhenVHostIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.VirtualHost(null!));
	}

	[Fact]
	public void VirtualHost_ThrowWhenVHostIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.VirtualHost(""));
	}

	[Fact]
	public void VirtualHost_SetVHostInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.VirtualHost("/myapp");

		// Assert
		options.Connection.VirtualHost.ShouldBe("/myapp");
	}

	[Fact]
	public void VirtualHost_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.VirtualHost("/");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Credentials Tests

	[Fact]
	public void Credentials_ThrowWhenUsernameIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Credentials(null!, "password"));
	}

	[Fact]
	public void Credentials_ThrowWhenUsernameIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.Credentials("", "password"));
	}

	[Fact]
	public void Credentials_SetCredentialsInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.Credentials("admin", "secret");

		// Assert
		options.Connection.Username.ShouldBe("admin");
		options.Connection.Password.ShouldBe("secret");
	}

	[Fact]
	public void Credentials_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.Credentials("guest", "guest");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConnectionString Tests

	[Fact]
	public void ConnectionString_ThrowWhenConnectionStringIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ConnectionString(null!));
	}

	[Fact]
	public void ConnectionString_ThrowWhenConnectionStringIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ConnectionString(""));
	}

	[Fact]
	public void ConnectionString_SetConnectionStringInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConnectionString("amqp://guest:guest@localhost:5672/");

		// Assert
		options.Connection.ConnectionString.ShouldBe("amqp://guest:guest@localhost:5672/");
	}

	[Fact]
	public void ConnectionString_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.ConnectionString("amqp://localhost");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region UseSsl Tests

	[Fact]
	public void UseSsl_EnableSslInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.UseSsl();

		// Assert
		options.Connection.UseSsl.ShouldBeTrue();
	}

	[Fact]
	public void UseSsl_ConfigureSslOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.UseSsl(ssl =>
		{
			ssl.ServerName = "rabbitmq.example.com";
			ssl.CertificatePath = "/path/to/cert.pem";
		});

		// Assert
		options.Connection.UseSsl.ShouldBeTrue();
		options.Connection.Ssl.ServerName.ShouldBe("rabbitmq.example.com");
		options.Connection.Ssl.CertificatePath.ShouldBe("/path/to/cert.pem");
	}

	[Fact]
	public void UseSsl_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.UseSsl();

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureExchange Tests

	[Fact]
	public void ConfigureExchange_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureExchange(null!));
	}

	[Fact]
	public void ConfigureExchange_AddExchangeToOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConfigureExchange(exchange =>
		{
			_ = exchange.Name("events").Type(RabbitMQExchangeType.Topic);
		});

		// Assert
		options.Topology.Exchanges.Count.ShouldBe(1);
		options.Topology.Exchanges[0].Name.ShouldBe("events");
		options.Topology.Exchanges[0].Type.ShouldBe(RabbitMQExchangeType.Topic);
	}

	[Fact]
	public void ConfigureExchange_SupportMultipleExchanges()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConfigureExchange(e => e.Name("events"))
			   .ConfigureExchange(e => e.Name("commands"));

		// Assert
		options.Topology.Exchanges.Count.ShouldBe(2);
	}

	[Fact]
	public void ConfigureExchange_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.ConfigureExchange(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureQueue Tests

	[Fact]
	public void ConfigureQueue_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureQueue(null!));
	}

	[Fact]
	public void ConfigureQueue_AddQueueToOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConfigureQueue(queue =>
		{
			_ = queue.Name("order-handlers").PrefetchCount(10);
		});

		// Assert
		options.Topology.Queues.Count.ShouldBe(1);
		options.Topology.Queues[0].Name.ShouldBe("order-handlers");
		options.Topology.Queues[0].PrefetchCount.ShouldBe((ushort)10);
	}

	[Fact]
	public void ConfigureQueue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.ConfigureQueue(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureBinding Tests

	[Fact]
	public void ConfigureBinding_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureBinding(null!));
	}

	[Fact]
	public void ConfigureBinding_AddBindingToOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConfigureBinding(binding =>
		{
			_ = binding.Exchange("events").Queue("handlers").RoutingKey("orders.*");
		});

		// Assert
		options.Topology.Bindings.Count.ShouldBe(1);
		options.Topology.Bindings[0].Exchange.ShouldBe("events");
		options.Topology.Bindings[0].Queue.ShouldBe("handlers");
		options.Topology.Bindings[0].RoutingKey.ShouldBe("orders.*");
	}

	[Fact]
	public void ConfigureBinding_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.ConfigureBinding(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureDeadLetter Tests

	[Fact]
	public void ConfigureDeadLetter_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureDeadLetter(null!));
	}

	[Fact]
	public void ConfigureDeadLetter_EnableDeadLetterAndConfigureOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.ConfigureDeadLetter(dlx =>
		{
			_ = dlx.Exchange("dead-letters").MaxRetries(5);
		});

		// Assert
		options.EnableDeadLetter.ShouldBeTrue();
		options.DeadLetter.Exchange.ShouldBe("dead-letters");
		options.DeadLetter.MaxRetries.ShouldBe(5);
	}

	[Fact]
	public void ConfigureDeadLetter_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.ConfigureDeadLetter(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MapExchange Tests

	[Fact]
	public void MapExchange_ThrowWhenExchangeIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapExchange<TestMessage>(null!));
	}

	[Fact]
	public void MapExchange_ThrowWhenExchangeIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapExchange<TestMessage>(""));
	}

	[Fact]
	public void MapExchange_AddMappingToOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.MapExchange<TestMessage>("orders-topic");

		// Assert
		options.Topology.ExchangeMappings.ShouldContainKey(typeof(TestMessage));
		options.Topology.ExchangeMappings[typeof(TestMessage)].ShouldBe("orders-topic");
	}

	[Fact]
	public void MapExchange_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.MapExchange<TestMessage>("exchange");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MapQueue Tests

	[Fact]
	public void MapQueue_ThrowWhenQueueIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapQueue<TestMessage>(null!));
	}

	[Fact]
	public void MapQueue_ThrowWhenQueueIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapQueue<TestMessage>(""));
	}

	[Fact]
	public void MapQueue_AddMappingToOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.MapQueue<TestMessage>("order-commands");

		// Assert
		options.Topology.QueueMappings.ShouldContainKey(typeof(TestMessage));
		options.Topology.QueueMappings[typeof(TestMessage)].ShouldBe("order-commands");
	}

	[Fact]
	public void MapQueue_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.MapQueue<TestMessage>("queue");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithExchangePrefix Tests

	[Fact]
	public void WithExchangePrefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithExchangePrefix(null!));
	}

	[Fact]
	public void WithExchangePrefix_ThrowWhenPrefixIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithExchangePrefix(""));
	}

	[Fact]
	public void WithExchangePrefix_SetPrefixInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.WithExchangePrefix("myapp-prod-");

		// Assert
		options.Topology.ExchangePrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void WithExchangePrefix_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.WithExchangePrefix("prefix-");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithQueuePrefix Tests

	[Fact]
	public void WithQueuePrefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithQueuePrefix(null!));
	}

	[Fact]
	public void WithQueuePrefix_ThrowWhenPrefixIsEmpty()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithQueuePrefix(""));
	}

	[Fact]
	public void WithQueuePrefix_SetPrefixInOptions()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		_ = builder.WithQueuePrefix("myapp-prod-");

		// Assert
		options.Topology.QueuePrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void WithQueuePrefix_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act
		var result = builder.WithQueuePrefix("prefix-");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void TransportBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new RabbitMQTransportOptions();
		var builder = new RabbitMQTransportBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.HostName("localhost")
				   .Port(5672)
				   .VirtualHost("/")
				   .Credentials("guest", "guest")
				   .UseSsl(ssl => ssl.ServerName = "localhost")
				   .ConfigureExchange(exchange =>
				   {
					   _ = exchange.Name("events")
							   .Type(RabbitMQExchangeType.Topic)
							   .Durable(true);
				   })
				   .ConfigureQueue(queue =>
				   {
					   _ = queue.Name("order-handlers")
							.Durable(true)
							.PrefetchCount(10)
							.AutoAck(false);
				   })
				   .ConfigureBinding(binding =>
				   {
					   _ = binding.Exchange("events")
							  .Queue("order-handlers")
							  .RoutingKey("orders.*");
				   })
				   .ConfigureDeadLetter(dlx =>
				   {
					   _ = dlx.Exchange("dead-letters")
						  .MaxRetries(3);
				   })
				   .MapExchange<TestMessage>("events")
				   .MapQueue<AnotherMessage>("commands")
				   .WithExchangePrefix("myapp-")
				   .WithQueuePrefix("myapp-");
		});

		// Verify all options set
		options.Connection.HostName.ShouldBe("localhost");
		options.Connection.Port.ShouldBe(5672);
		options.Connection.VirtualHost.ShouldBe("/");
		options.Connection.Username.ShouldBe("guest");
		options.Connection.Password.ShouldBe("guest");
		options.Connection.UseSsl.ShouldBeTrue();
		options.Topology.Exchanges.Count.ShouldBe(1);
		options.Topology.Queues.Count.ShouldBe(1);
		options.Topology.Bindings.Count.ShouldBe(1);
		options.EnableDeadLetter.ShouldBeTrue();
		options.Topology.ExchangeMappings.ShouldContainKey(typeof(TestMessage));
		options.Topology.QueueMappings.ShouldContainKey(typeof(AnotherMessage));
		options.Topology.ExchangePrefix.ShouldBe("myapp-");
		options.Topology.QueuePrefix.ShouldBe("myapp-");
	}

	#endregion

	// Test helper classes
	private sealed class TestMessage;
	private sealed class AnotherMessage;
}
