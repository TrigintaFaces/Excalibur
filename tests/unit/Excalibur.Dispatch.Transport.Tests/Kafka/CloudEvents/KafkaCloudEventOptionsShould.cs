// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new KafkaCloudEventOptions();

		// Assert
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.CorrelationId);
		options.DefaultTopic.ShouldBeNull();
		options.DefaultPartitionCount.ShouldBe(3);
		options.DefaultReplicationFactor.ShouldBe((short)1);
		options.Producer.EnableIdempotentProducer.ShouldBeTrue();
		options.Producer.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.All);
		options.Producer.MaxMessageSizeBytes.ShouldBe(1024 * 1024);
		options.Producer.EnableCompression.ShouldBeTrue();
		options.Producer.CompressionType.ShouldBe(KafkaCompressionType.Snappy);
		options.Producer.CompressionThreshold.ShouldBe(1024);
		options.Producer.RetrySettings.ShouldNotBeNull();
		options.Producer.EnableTransactions.ShouldBeFalse();
		options.Producer.TransactionalId.ShouldBeNull();
		options.AutoCreateTopics.ShouldBeFalse();
		options.ConsumerGroupId.ShouldBeNull();
		options.OffsetReset.ShouldBe(KafkaOffsetReset.Latest);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var retryOptions = new KafkaRetryOptions();
		var options = new KafkaCloudEventOptions
		{
			PartitioningStrategy = KafkaPartitioningStrategy.RoundRobin,
			DefaultTopic = "my-topic",
			DefaultPartitionCount = 6,
			DefaultReplicationFactor = 3,
			Producer = new KafkaCloudEventProducerOptions
			{
				EnableIdempotentProducer = false,
				AcknowledgmentLevel = KafkaAckLevel.Leader,
				MaxMessageSizeBytes = 2_097_152,
				EnableCompression = false,
				CompressionType = KafkaCompressionType.Gzip,
				CompressionThreshold = 2048,
				RetrySettings = retryOptions,
				EnableTransactions = true,
				TransactionalId = "txn-1",
			},
			AutoCreateTopics = true,
			ConsumerGroupId = "my-group",
			OffsetReset = KafkaOffsetReset.Earliest,
		};

		// Assert
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.RoundRobin);
		options.DefaultTopic.ShouldBe("my-topic");
		options.DefaultPartitionCount.ShouldBe(6);
		options.DefaultReplicationFactor.ShouldBe((short)3);
		options.Producer.EnableIdempotentProducer.ShouldBeFalse();
		options.Producer.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.Leader);
		options.Producer.MaxMessageSizeBytes.ShouldBe(2_097_152);
		options.Producer.EnableCompression.ShouldBeFalse();
		options.Producer.CompressionType.ShouldBe(KafkaCompressionType.Gzip);
		options.Producer.CompressionThreshold.ShouldBe(2048);
		options.Producer.RetrySettings.ShouldBe(retryOptions);
		options.Producer.EnableTransactions.ShouldBeTrue();
		options.Producer.TransactionalId.ShouldBe("txn-1");
		options.AutoCreateTopics.ShouldBeTrue();
		options.ConsumerGroupId.ShouldBe("my-group");
		options.OffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
	}
}
