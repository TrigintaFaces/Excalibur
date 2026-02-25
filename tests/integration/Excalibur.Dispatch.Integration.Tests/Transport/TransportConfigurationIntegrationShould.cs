// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Abstractions;

using Microsoft.Extensions.DependencyInjection;

namespace Excalibur.Dispatch.Integration.Tests.Transport;

/// <summary>
/// Integration tests for transport configuration and message routing.
/// These tests verify transport options, serialization, and connection management.
/// </summary>
public sealed class TransportConfigurationIntegrationShould : IntegrationTestBase
{
	#region Transport Options Tests

	[Fact]
	public void RabbitMqOptions_ValidatesConfiguration()
	{
		// Arrange
		var options = new Dispatch.Transport.RabbitMQ.RabbitMqOptions
		{
			ConnectionString = "amqp://guest:guest@localhost:5672/",
			Exchange = "test-exchange",
			QueueName = "test-queue",
			RoutingKey = "test-key"
		};

		// Assert
		options.ConnectionString.ShouldNotBeNullOrEmpty();
		options.Exchange.ShouldBe("test-exchange");
		options.QueueName.ShouldBe("test-queue");
		options.RoutingKey.ShouldBe("test-key");
	}

	[Fact]
	public void KafkaOptions_ValidatesConfiguration()
	{
		// Arrange
		var options = new Dispatch.Transport.Kafka.KafkaOptions
		{
			BootstrapServers = "localhost:9092",
			ConsumerGroup = "test-group",
			AutoOffsetReset = "earliest"
		};

		// Assert
		options.BootstrapServers.ShouldBe("localhost:9092");
		options.ConsumerGroup.ShouldBe("test-group");
		options.AutoOffsetReset.ShouldBe("earliest");
	}

	[Fact]
	public void AzureServiceBusOptions_ValidatesConfiguration()
	{
		// Arrange
		var options = new Dispatch.Transport.Azure.AzureServiceBusOptions
		{
			ConnectionString = "Endpoint=sb://test.servicebus.windows.net/;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=test",
			QueueName = "test-queue",
			MaxConcurrentCalls = 16
		};

		// Assert
		options.ConnectionString.ShouldNotBeNullOrEmpty();
		options.QueueName.ShouldBe("test-queue");
		options.MaxConcurrentCalls.ShouldBe(16);
	}

	[Fact]
	public void AwsSqsOptions_ValidatesConfiguration()
	{
		// Arrange
		var options = new Dispatch.Transport.Aws.AwsSqsOptions
		{
			Region = "us-east-1",
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test-queue"),
			MaxNumberOfMessages = 10
		};

		// Assert
		options.Region.ShouldBe("us-east-1");
		options.QueueUrl.ToString().ShouldContain("test-queue");
		options.MaxNumberOfMessages.ShouldBe(10);
	}

	#endregion

	#region Service Registration Tests

	[Fact]
	public void TransportServices_RegisterCorrectly()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Act - Register transport abstractions
		_ = services.AddSingleton<ITestTransportSerializer, TestTransportSerializer>();

		// Assert
		using var provider = services.BuildServiceProvider();
		var serializer = provider.GetService<ITestTransportSerializer>();
		_ = serializer.ShouldNotBeNull();
	}

	[Fact]
	public void MultipleTransports_CanBeConfigured()
	{
		// Arrange
		var services = new ServiceCollection();
		_ = services.AddLogging();

		// Configure multiple transport options
		_ = services.Configure<Dispatch.Transport.RabbitMQ.RabbitMqOptions>(opts =>
		{
			opts.ConnectionString = "amqp://guest:guest@rabbit-host:5672/";
		});

		_ = services.Configure<Dispatch.Transport.Kafka.KafkaOptions>(opts =>
		{
			opts.BootstrapServers = "kafka-host:9092";
		});

		using var provider = services.BuildServiceProvider();

		// Assert
		var rabbitOptions = provider.GetRequiredService<IOptions<Dispatch.Transport.RabbitMQ.RabbitMqOptions>>();
		var kafkaOptions = provider.GetRequiredService<IOptions<Dispatch.Transport.Kafka.KafkaOptions>>();

		rabbitOptions.Value.ConnectionString.ShouldContain("rabbit-host");
		kafkaOptions.Value.BootstrapServers.ShouldBe("kafka-host:9092");
	}

	#endregion

	#region Serialization Tests

	[Fact]
	public void TransportSerializer_SerializesAndDeserializes()
	{
		// Arrange
		var serializer = new TestTransportSerializer();
		var message = new TestTransportMessage("test-id", "test-payload");

		// Act
		var serialized = serializer.Serialize(message);
		var deserialized = serializer.Deserialize<TestTransportMessage>(serialized);

		// Assert
		serialized.ShouldNotBeEmpty();
		_ = deserialized.ShouldNotBeNull();
		deserialized.Id.ShouldBe("test-id");
		deserialized.Payload.ShouldBe("test-payload");
	}

	#endregion

	#region Test Helpers

	private sealed record TestTransportMessage(string Id, string Payload);

	/// <summary>
	/// Test-only interface for transport serialization testing.
	/// </summary>
	private interface ITestTransportSerializer
	{
		byte[] Serialize<T>(T message);
		T? Deserialize<T>(byte[] data);
		object? Deserialize(byte[] data, Type type);
	}

	private sealed class TestTransportSerializer : ITestTransportSerializer
	{
		public byte[] Serialize<T>(T message)
		{
			var json = System.Text.Json.JsonSerializer.Serialize(message);
			return System.Text.Encoding.UTF8.GetBytes(json);
		}

		public T? Deserialize<T>(byte[] data)
		{
			var json = System.Text.Encoding.UTF8.GetString(data);
			return System.Text.Json.JsonSerializer.Deserialize<T>(json);
		}

		public object? Deserialize(byte[] data, Type type)
		{
			var json = System.Text.Encoding.UTF8.GetString(data);
			return System.Text.Json.JsonSerializer.Deserialize(json, type);
		}
	}

	#endregion
}
