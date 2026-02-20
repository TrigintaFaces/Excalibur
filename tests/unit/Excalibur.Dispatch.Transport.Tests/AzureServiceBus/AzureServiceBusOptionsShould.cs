using Azure.Messaging.ServiceBus;

using Excalibur.Dispatch.Messaging;
using Excalibur.Dispatch.Transport.Azure;

namespace Excalibur.Dispatch.Transport.Tests.AzureServiceBus;

/// <summary>
/// Unit tests for AzureServiceBusOptions configuration.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AzureServiceBusOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new AzureServiceBusOptions();

		// Assert
		options.MaxConcurrentCalls.ShouldBe(10);
		options.PrefetchCount.ShouldBe(50);
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpTcp);
		options.CloudEventsMode.ShouldBe(CloudEventsMode.Structured);
		options.EnableEncryption.ShouldBeFalse();
		options.DeadLetterOnRejection.ShouldBeFalse();
	}

	[Fact]
	public void Namespace_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.Namespace = "my-namespace.servicebus.windows.net";

		// Assert
		options.Namespace.ShouldBe("my-namespace.servicebus.windows.net");
	}

	[Fact]
	public void QueueName_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.QueueName = "my-queue";

		// Assert
		options.QueueName.ShouldBe("my-queue");
	}

	[Fact]
	public void ConnectionString_CanBeSetAndRetrieved()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=test;SharedAccessKey=test";

		// Assert
		options.ConnectionString.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void MaxConcurrentCalls_CanBeCustomized()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.MaxConcurrentCalls = 20;

		// Assert
		options.MaxConcurrentCalls.ShouldBe(20);
	}

	[Fact]
	public void PrefetchCount_CanBeCustomized()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.PrefetchCount = 100;

		// Assert
		options.PrefetchCount.ShouldBe(100);
	}

	[Fact]
	public void TransportType_CanBeChangedToWebSockets()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.TransportType = ServiceBusTransportType.AmqpWebSockets;

		// Assert
		options.TransportType.ShouldBe(ServiceBusTransportType.AmqpWebSockets);
	}

	[Fact]
	public void EnableEncryption_CanBeEnabled()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.EnableEncryption = true;

		// Assert
		options.EnableEncryption.ShouldBeTrue();
	}

	[Fact]
	public void DeadLetterOnRejection_CanBeEnabled()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.DeadLetterOnRejection = true;

		// Assert
		options.DeadLetterOnRejection.ShouldBeTrue();
	}

	[Fact]
	public void CloudEventsMode_CanBeChangedToBinary()
	{
		// Arrange
		var options = new AzureServiceBusOptions();

		// Act
		options.CloudEventsMode = CloudEventsMode.Binary;

		// Assert
		options.CloudEventsMode.ShouldBe(CloudEventsMode.Binary);
	}
}
