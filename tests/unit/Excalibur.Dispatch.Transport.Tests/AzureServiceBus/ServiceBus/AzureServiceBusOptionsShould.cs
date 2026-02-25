// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus.ServiceBus;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AzureServiceBusOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AzureServiceBusOptions();

		// Assert
		options.Namespace.ShouldBeNull();
		options.QueueName.ShouldBeNull();
		options.ConnectionString.ShouldBeNull();
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpTcp);
		options.MaxConcurrentCalls.ShouldBe(10);
		options.PrefetchCount.ShouldBe(50);
		options.CloudEventsMode.ShouldBe(CloudEventsMode.Structured);
		options.EnableEncryption.ShouldBeFalse();
		options.DeadLetterOnRejection.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingConnectionConfiguration()
	{
		// Arrange & Act
		var options = new AzureServiceBusOptions
		{
			Namespace = "myns.servicebus.windows.net",
			QueueName = "my-queue",
			ConnectionString = "Endpoint=sb://myns.servicebus.windows.net/",
			TransportType = ServiceBusTransportType.AmqpWebSockets,
		};

		// Assert
		options.Namespace.ShouldBe("myns.servicebus.windows.net");
		options.QueueName.ShouldBe("my-queue");
		options.ConnectionString.ShouldBe("Endpoint=sb://myns.servicebus.windows.net/");
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
	}

	[Fact]
	public void AllowSettingProcessingConfiguration()
	{
		// Arrange & Act
		var options = new AzureServiceBusOptions
		{
			MaxConcurrentCalls = 50,
			PrefetchCount = 100,
			CloudEventsMode = CloudEventsMode.Binary,
		};

		// Assert
		options.MaxConcurrentCalls.ShouldBe(50);
		options.PrefetchCount.ShouldBe(100);
		options.CloudEventsMode.ShouldBe(CloudEventsMode.Binary);
	}

	[Fact]
	public void AllowEnablingEncryptionAndDeadLetter()
	{
		// Arrange & Act
		var options = new AzureServiceBusOptions
		{
			EnableEncryption = true,
			DeadLetterOnRejection = true,
		};

		// Assert
		options.EnableEncryption.ShouldBeTrue();
		options.DeadLetterOnRejection.ShouldBeTrue();
	}
}
