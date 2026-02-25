// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.Transport.Builders;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusTransportOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureServiceBusTransportOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.ConnectionString.ShouldBeNull();
		options.FullyQualifiedNamespace.ShouldBeNull();
		options.UseManagedIdentity.ShouldBeFalse();
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpTcp);
		options.Sender.ShouldNotBeNull();
		options.Processor.ShouldNotBeNull();
		options.CloudEvents.ShouldNotBeNull();
		options.EntityMappings.ShouldBeEmpty();
		options.EntityPrefix.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new AzureServiceBusTransportOptions
		{
			Name = "primary",
			ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=key;SharedAccessKey=value",
			FullyQualifiedNamespace = "test.servicebus.windows.net",
			UseManagedIdentity = true,
			TransportType = ServiceBusTransportType.AmqpWebSockets,
			EntityPrefix = "prod-",
		};

		// Assert
		options.Name.ShouldBe("primary");
		options.ConnectionString.ShouldNotBeNull();
		options.FullyQualifiedNamespace.ShouldBe("test.servicebus.windows.net");
		options.UseManagedIdentity.ShouldBeTrue();
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
		options.EntityPrefix.ShouldBe("prod-");
	}

	[Fact]
	public void AllowEntityMappings()
	{
		// Arrange
		var options = new AzureServiceBusTransportOptions();

		// Act
		options.EntityMappings[typeof(string)] = "string-queue";
		options.EntityMappings[typeof(int)] = "int-topic";

		// Assert
		options.EntityMappings.Count.ShouldBe(2);
		options.EntityMappings[typeof(string)].ShouldBe("string-queue");
		options.EntityMappings[typeof(int)].ShouldBe("int-topic");
	}

	[Fact]
	public void SenderOptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var sender = new AzureServiceBusSenderOptions();

		// Assert
		sender.DefaultEntityName.ShouldBeNull();
		sender.EnableBatching.ShouldBeTrue();
		sender.MaxBatchSizeBytes.ShouldBe(256 * 1024);
		sender.MaxBatchCount.ShouldBe(100);
		sender.BatchWindow.ShouldBe(TimeSpan.FromMilliseconds(100));
		sender.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void SenderOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var sender = new AzureServiceBusSenderOptions
		{
			DefaultEntityName = "orders-queue",
			EnableBatching = false,
			MaxBatchSizeBytes = 512 * 1024,
			MaxBatchCount = 50,
			BatchWindow = TimeSpan.FromMilliseconds(500),
		};
		sender.AdditionalConfig["retry.count"] = "3";

		// Assert
		sender.DefaultEntityName.ShouldBe("orders-queue");
		sender.EnableBatching.ShouldBeFalse();
		sender.MaxBatchSizeBytes.ShouldBe(512 * 1024);
		sender.MaxBatchCount.ShouldBe(50);
		sender.BatchWindow.ShouldBe(TimeSpan.FromMilliseconds(500));
		sender.AdditionalConfig["retry.count"].ShouldBe("3");
	}

	[Fact]
	public void ProcessorOptionsHaveCorrectDefaults()
	{
		// Arrange & Act
		var processor = new AzureServiceBusProcessorOptions();

		// Assert
		processor.DefaultEntityName.ShouldBeNull();
		processor.MaxConcurrentCalls.ShouldBe(10);
		processor.AutoCompleteMessages.ShouldBeTrue();
		processor.PrefetchCount.ShouldBe(50);
		processor.MaxAutoLockRenewalDuration.ShouldBeNull();
		processor.ReceiveMode.ShouldBe(ServiceBusReceiveMode.PeekLock);
		processor.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void ProcessorOptionsAllowSettingAllProperties()
	{
		// Arrange & Act
		var processor = new AzureServiceBusProcessorOptions
		{
			DefaultEntityName = "events-subscription",
			MaxConcurrentCalls = 32,
			AutoCompleteMessages = false,
			PrefetchCount = 100,
			MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5),
			ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
		};
		processor.AdditionalConfig["session.enabled"] = "true";

		// Assert
		processor.DefaultEntityName.ShouldBe("events-subscription");
		processor.MaxConcurrentCalls.ShouldBe(32);
		processor.AutoCompleteMessages.ShouldBeFalse();
		processor.PrefetchCount.ShouldBe(100);
		processor.MaxAutoLockRenewalDuration.ShouldBe(TimeSpan.FromMinutes(5));
		processor.ReceiveMode.ShouldBe(ServiceBusReceiveMode.ReceiveAndDelete);
		processor.AdditionalConfig["session.enabled"].ShouldBe("true");
	}
}
