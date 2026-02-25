// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Transport.Azure;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Transport.Builders;

/// <summary>
/// Unit tests for <see cref="IAzureServiceBusTransportBuilder"/>.
/// Part of S472.3 - AddAzureServiceBusTransport single entry point (Sprint 472).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AzureServiceBusTransportBuilderShould : UnitTestBase
{
	#region AddAzureServiceBusTransport Tests

	[Fact]
	public void AddAzureServiceBusTransport_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AzureServiceBusTransportServiceCollectionExtensions.AddAzureServiceBusTransport(
				null!, "test", _ => { }));
	}

	[Fact]
	public void AddAzureServiceBusTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddAzureServiceBusTransport(null!, _ => { }));
	}

	[Fact]
	public void AddAzureServiceBusTransport_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddAzureServiceBusTransport("", _ => { }));
	}

	[Fact]
	public void AddAzureServiceBusTransport_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAzureServiceBusTransport("test", null!));
	}

	[Fact]
	public void AddAzureServiceBusTransport_InvokeConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var callbackInvoked = false;

		// Act
		_ = services.AddAzureServiceBusTransport("test", _ =>
		{
			callbackInvoked = true;
		});

		// Assert
		callbackInvoked.ShouldBeTrue();
	}

	#endregion

	#region ConnectionString Tests

	[Fact]
	public void ConnectionString_ThrowWhenConnectionStringIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ConnectionString(null!));
	}

	[Fact]
	public void ConnectionString_ThrowWhenConnectionStringIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.ConnectionString(""));
	}

	[Fact]
	public void ConnectionString_SetConnectionStringInOptions()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=...");

		// Assert
		options.ConnectionString.ShouldBe("Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=...");
		options.UseManagedIdentity.ShouldBeFalse();
	}

	[Fact]
	public void ConnectionString_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;...");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region FullyQualifiedNamespace Tests

	[Fact]
	public void FullyQualifiedNamespace_ThrowWhenNamespaceIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.FullyQualifiedNamespace(null!));
	}

	[Fact]
	public void FullyQualifiedNamespace_ThrowWhenNamespaceIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.FullyQualifiedNamespace(""));
	}

	[Fact]
	public void FullyQualifiedNamespace_SetNamespaceAndEnableManagedIdentity()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.FullyQualifiedNamespace("mynamespace.servicebus.windows.net");

		// Assert
		options.FullyQualifiedNamespace.ShouldBe("mynamespace.servicebus.windows.net");
		options.UseManagedIdentity.ShouldBeTrue();
	}

	[Fact]
	public void FullyQualifiedNamespace_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.FullyQualifiedNamespace("mynamespace.servicebus.windows.net");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region TransportType Tests

	[Fact]
	public void TransportType_SetTransportTypeInOptions()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.TransportType(ServiceBusTransportType.AmqpWebSockets);

		// Assert
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
	}

	[Fact]
	public void TransportType_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.TransportType(ServiceBusTransportType.AmqpTcp);

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureSender Tests

	[Fact]
	public void ConfigureSender_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureSender(null!));
	}

	[Fact]
	public void ConfigureSender_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.ConfigureSender(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region ConfigureProcessor Tests

	[Fact]
	public void ConfigureProcessor_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => builder.ConfigureProcessor(null!));
	}

	[Fact]
	public void ConfigureProcessor_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.ConfigureProcessor(_ => { });

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region MapEntity Tests

	[Fact]
	public void MapEntity_ThrowWhenEntityNameIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapEntity<TestMessage>(null!));
	}

	[Fact]
	public void MapEntity_ThrowWhenEntityNameIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.MapEntity<TestMessage>(""));
	}

	[Fact]
	public void MapEntity_AddMappingToOptions()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.MapEntity<TestMessage>("orders-topic");

		// Assert
		options.EntityMappings.ShouldContainKey(typeof(TestMessage));
		options.EntityMappings[typeof(TestMessage)].ShouldBe("orders-topic");
	}

	[Fact]
	public void MapEntity_SupportMultipleMappings()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.MapEntity<TestMessage>("orders-topic")
			   .MapEntity<AnotherMessage>("payments-queue");

		// Assert
		options.EntityMappings.Count.ShouldBe(2);
	}

	[Fact]
	public void MapEntity_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.MapEntity<TestMessage>("orders-topic");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region WithEntityPrefix Tests

	[Fact]
	public void WithEntityPrefix_ThrowWhenPrefixIsNull()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithEntityPrefix(null!));
	}

	[Fact]
	public void WithEntityPrefix_ThrowWhenPrefixIsEmpty()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => builder.WithEntityPrefix(""));
	}

	[Fact]
	public void WithEntityPrefix_SetPrefixInOptions()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		_ = builder.WithEntityPrefix("myapp-prod-");

		// Assert
		options.EntityPrefix.ShouldBe("myapp-prod-");
	}

	[Fact]
	public void WithEntityPrefix_ReturnBuilderForChaining()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act
		var result = builder.WithEntityPrefix("myapp-");

		// Assert
		result.ShouldBeSameAs(builder);
	}

	#endregion

	#region Full Fluent Chain Tests

	[Fact]
	public void TransportBuilder_SupportFullFluentChain()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();
		var builder = new AzureServiceBusTransportBuilder(options);

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = builder.ConnectionString("Endpoint=sb://test.servicebus.windows.net/;...")
				   .TransportType(ServiceBusTransportType.AmqpWebSockets)
				   .ConfigureSender(sender =>
				   {
					   _ = sender.DefaultEntity("orders-queue")
							 .EnableBatching(true)
							 .MaxBatchSizeBytes(256 * 1024);
				   })
				   .ConfigureProcessor(processor =>
				   {
					   _ = processor.DefaultEntity("orders-queue")
							   .MaxConcurrentCalls(20)
							   .PrefetchCount(100);
				   })
				   .MapEntity<TestMessage>("orders-topic")
				   .WithEntityPrefix("myapp-");
		});

		// Verify all options set
		options.ConnectionString.ShouldBe("Endpoint=sb://test.servicebus.windows.net/;...");
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
		options.Sender.DefaultEntityName.ShouldBe("orders-queue");
		options.Sender.EnableBatching.ShouldBeTrue();
		options.Processor.DefaultEntityName.ShouldBe("orders-queue");
		options.Processor.MaxConcurrentCalls.ShouldBe(20);
		options.EntityMappings.ShouldContainKey(typeof(TestMessage));
		options.EntityPrefix.ShouldBe("myapp-");
	}

	#endregion

	// Test helper classes
	private sealed class TestMessage;
	private sealed class AnotherMessage;
}
