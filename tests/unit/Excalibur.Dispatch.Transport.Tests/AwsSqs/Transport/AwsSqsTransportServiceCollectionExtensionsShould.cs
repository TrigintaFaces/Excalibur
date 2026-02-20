// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Amazon.SQS;

using Excalibur.Dispatch.Transport.Aws;

using Microsoft.Extensions.DependencyInjection;

using Tests.Shared.Categories;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Transport;

/// <summary>
/// Unit tests for <see cref="AwsSqsTransportServiceCollectionExtensions"/>.
/// Part of S471.0 - AddAwsSqsTransport() Single Entry Point (Sprint 471).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("Component", "Transport")]
[Trait("Pattern", "TRANSPORT")]
public sealed class AwsSqsTransportServiceCollectionExtensionsShould : UnitTestBase
{
	private const string ValidRegion = "us-east-1";

	#region Named Overload Tests (name, configure)

	[Fact]
	public void AddAwsSqsTransport_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AwsSqsTransportServiceCollectionExtensions.AddAwsSqsTransport(null!, "sqs", _ => { }));
	}

	[Fact]
	public void AddAwsSqsTransport_ThrowWhenNameIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddAwsSqsTransport(null!, _ => { }));
	}

	[Fact]
	public void AddAwsSqsTransport_ThrowWhenNameIsEmpty()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentException>(() =>
			services.AddAwsSqsTransport("", _ => { }));
	}

	[Fact]
	public void AddAwsSqsTransport_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsSqsTransport("sqs", (Action<IAwsSqsTransportBuilder>)null!));
	}

	[Fact]
	public void AddAwsSqsTransport_InvokeConfigureCallback()
	{
		// Arrange
		var services = new ServiceCollection();
		var configureInvoked = false;

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs =>
		{
			configureInvoked = true;
			_ = sqs.UseRegion(ValidRegion);
		});

		// Assert
		configureInvoked.ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_RegisterAwsSqsClient()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));

		// Assert
		services.Any(d => d.ServiceType == typeof(IAmazonSQS)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_RegisterAwsSqsMessageBus()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));

		// Assert
		services.Any(d => d.ServiceType == typeof(AwsSqsMessageBus)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_RegisterAwsSqsChannelReceiver()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));

		// Assert
		services.Any(d => d.ServiceType == typeof(AwsSqsChannelReceiver)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_RegisterTransportAdapter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));

		// Assert
		services.Any(d => d.ServiceType == typeof(AwsSqsTransportAdapter)).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_RegisterKeyedTransportAdapter()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport("orders", sqs => sqs.UseRegion(ValidRegion));

		// Assert - Should have keyed service registration
		services.Any(d =>
			d.ServiceType == typeof(AwsSqsTransportAdapter) &&
			d.ServiceKey is string key && key == "orders").ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Default Name Overload Tests (configure)

	[Fact]
	public void AddAwsSqsTransport_DefaultOverload_ThrowWhenServicesIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			AwsSqsTransportServiceCollectionExtensions.AddAwsSqsTransport(null!, _ => { }));
	}

	[Fact]
	public void AddAwsSqsTransport_DefaultOverload_ThrowWhenConfigureIsNull()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			services.AddAwsSqsTransport((Action<IAwsSqsTransportBuilder>)null!));
	}

	[Fact]
	public void AddAwsSqsTransport_DefaultOverload_UseDefaultTransportName()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services.AddAwsSqsTransport(sqs => sqs.UseRegion(ValidRegion));

		// Assert - Should have keyed service with default name
		services.Any(d =>
			d.ServiceType == typeof(AwsSqsTransportAdapter) &&
			d.ServiceKey is string key &&
			key == AwsSqsTransportServiceCollectionExtensions.DefaultTransportName).ShouldBeTrue();
	}

	[Fact]
	public void AddAwsSqsTransport_DefaultOverload_ReturnServiceCollectionForChaining()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services.AddAwsSqsTransport(sqs => sqs.UseRegion(ValidRegion));

		// Assert
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Fluent Builder Tests

	[Fact]
	public void AddAwsSqsTransport_ConfigureRegion()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs => sqs.UseRegion(ValidRegion));
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureQueueOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureQueue(queue =>
				   {
					   _ = queue.VisibilityTimeout(TimeSpan.FromMinutes(5))
							.MessageRetentionPeriod(TimeSpan.FromDays(7))
							.ReceiveWaitTimeSeconds(20);
				   });
			});
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureFifoOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureFifo(fifo =>
				   {
					   _ = fifo.ContentBasedDeduplication(true);
				   });
			});
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureBatchOptions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .ConfigureBatch(batch =>
				   {
					   _ = batch.SendBatchSize(10)
							.SendBatchWindow(TimeSpan.FromMilliseconds(100))
							.ReceiveMaxMessages(10);
				   });
			});
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureQueueMapping()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .MapQueue<TestMessage>("https://sqs.us-east-1.amazonaws.com/123/orders");
			});
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureQueuePrefix()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("sqs", sqs =>
			{
				_ = sqs.UseRegion(ValidRegion)
				   .WithQueuePrefix("myapp-prod-");
			});
		});
	}

	[Fact]
	public void AddAwsSqsTransport_ConfigureFullFluentChain()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert - Should not throw
		Should.NotThrow(() =>
		{
			_ = services.AddAwsSqsTransport("orders", sqs =>
			{
				_ = sqs.UseRegion("us-east-1")
				   .ConfigureQueue(queue => queue.VisibilityTimeout(TimeSpan.FromMinutes(5)))
				   .ConfigureFifo(fifo => fifo.ContentBasedDeduplication(true))
				   .ConfigureBatch(batch => batch.SendBatchSize(10))
				   .MapQueue<TestMessage>("https://sqs.us-east-1.amazonaws.com/123/orders")
				   .WithQueuePrefix("myapp-");
			});
		});
	}

	#endregion

	#region Multiple Transports Tests

	[Fact]
	public void AddAwsSqsTransport_SupportMultipleTransportsWithDifferentNames()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		_ = services
			.AddAwsSqsTransport("orders-sqs", sqs => sqs.UseRegion("us-east-1"))
			.AddAwsSqsTransport("notifications-sqs", sqs => sqs.UseRegion("us-west-2"));

		// Assert - Both keyed services should exist
		services.Count(d =>
			d.ServiceType == typeof(AwsSqsTransportAdapter) &&
			d.ServiceKey is string).ShouldBe(2);
	}

	[Fact]
	public void AddAwsSqsTransport_SupportFluentChainWithOtherExtensions()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		var result = services
			.AddAwsSqsTransport("sqs-1", sqs => sqs.UseRegion(ValidRegion))
			.AddAwsSqsTransport("sqs-2", sqs => sqs.UseRegion("eu-west-1"));

		// Assert
		_ = result.ShouldNotBeNull();
		result.ShouldBeSameAs(services);
	}

	#endregion

	#region Helper Classes

	private sealed class TestMessage { }

	#endregion
}
