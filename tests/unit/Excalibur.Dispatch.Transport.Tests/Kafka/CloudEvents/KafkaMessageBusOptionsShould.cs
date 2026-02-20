// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaMessageBusOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
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
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new KafkaMessageBusOptions
		{
			BootstrapServers = "localhost:9092",
			ProducerClientId = "my-producer",
			ConsumerGroupId = "my-group",
			EnableCloudEvents = false,
			CompressionType = KafkaCompressionType.Gzip,
			AckLevel = KafkaAckLevel.Leader,
			PartitioningStrategy = KafkaPartitioningStrategy.CorrelationId,
			EnableTransactions = true,
			TransactionalId = "txn-123",
			AutoCreateTopics = true,
			DefaultTopic = "my-topic",
		};

		// Assert
		options.BootstrapServers.ShouldBe("localhost:9092");
		options.ProducerClientId.ShouldBe("my-producer");
		options.ConsumerGroupId.ShouldBe("my-group");
		options.EnableCloudEvents.ShouldBeFalse();
		options.CompressionType.ShouldBe(KafkaCompressionType.Gzip);
		options.AckLevel.ShouldBe(KafkaAckLevel.Leader);
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.CorrelationId);
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("txn-123");
		options.AutoCreateTopics.ShouldBeTrue();
		options.DefaultTopic.ShouldBe("my-topic");
	}
}
