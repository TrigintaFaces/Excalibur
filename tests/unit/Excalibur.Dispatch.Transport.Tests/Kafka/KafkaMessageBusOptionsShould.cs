// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaMessageBusOptionsShould
{
	[Fact]
	public void HaveCorrectDefaultValues()
	{
		// Act
		var options = new KafkaMessageBusOptions();

		// Assert
		options.BootstrapServers.ShouldBeNull();
		options.ProducerClientId.ShouldBe("dispatch-producer");
		options.ConsumerGroupId.ShouldBe("dispatch-consumer");
		options.EnableCloudEvents.ShouldBeTrue();
		options.CompressionType.ShouldBe(KafkaCompressionType.None);
		options.AckLevel.ShouldBe(KafkaAckLevel.All);
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.RoundRobin);
		options.EnableTransactions.ShouldBeFalse();
		options.TransactionalId.ShouldBeNull();
		options.AutoCreateTopics.ShouldBeFalse();
		options.DefaultTopic.ShouldBeNull();
	}

	[Fact]
	public void SetBootstrapServers()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.BootstrapServers = "broker1:9092,broker2:9092";

		// Assert
		options.BootstrapServers.ShouldBe("broker1:9092,broker2:9092");
	}

	[Fact]
	public void SetProducerClientId()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.ProducerClientId = "my-producer";

		// Assert
		options.ProducerClientId.ShouldBe("my-producer");
	}

	[Fact]
	public void SetConsumerGroupId()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.ConsumerGroupId = "my-consumer-group";

		// Assert
		options.ConsumerGroupId.ShouldBe("my-consumer-group");
	}

	[Fact]
	public void SetCompressionType()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.CompressionType = KafkaCompressionType.Zstd;

		// Assert
		options.CompressionType.ShouldBe(KafkaCompressionType.Zstd);
	}

	[Fact]
	public void SetAckLevel()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.AckLevel = KafkaAckLevel.Leader;

		// Assert
		options.AckLevel.ShouldBe(KafkaAckLevel.Leader);
	}

	[Fact]
	public void SetPartitioningStrategy()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.PartitioningStrategy = KafkaPartitioningStrategy.TenantId;

		// Assert
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.TenantId);
	}

	[Fact]
	public void SetTransactionProperties()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.EnableTransactions = true;
		options.TransactionalId = "tx-001";

		// Assert
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("tx-001");
	}

	[Fact]
	public void SetDefaultTopic()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.DefaultTopic = "events-topic";

		// Assert
		options.DefaultTopic.ShouldBe("events-topic");
	}

	[Fact]
	public void SetAutoCreateTopics()
	{
		// Arrange
		var options = new KafkaMessageBusOptions();

		// Act
		options.AutoCreateTopics = true;

		// Assert
		options.AutoCreateTopics.ShouldBeTrue();
	}
}
