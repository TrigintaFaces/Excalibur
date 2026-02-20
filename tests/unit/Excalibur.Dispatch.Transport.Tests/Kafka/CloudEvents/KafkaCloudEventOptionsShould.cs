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
		options.EnableIdempotentProducer.ShouldBeTrue();
		options.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.All);
		options.MaxMessageSizeBytes.ShouldBe(1024 * 1024);
		options.EnableCompression.ShouldBeTrue();
		options.CompressionType.ShouldBe(KafkaCompressionType.Snappy);
		options.CompressionThreshold.ShouldBe(1024);
		options.RetrySettings.ShouldNotBeNull();
		options.EnableTransactions.ShouldBeFalse();
		options.TransactionalId.ShouldBeNull();
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
			EnableIdempotentProducer = false,
			AcknowledgmentLevel = KafkaAckLevel.Leader,
			MaxMessageSizeBytes = 2_097_152,
			EnableCompression = false,
			CompressionType = KafkaCompressionType.Gzip,
			CompressionThreshold = 2048,
			RetrySettings = retryOptions,
			EnableTransactions = true,
			TransactionalId = "txn-1",
			AutoCreateTopics = true,
			ConsumerGroupId = "my-group",
			OffsetReset = KafkaOffsetReset.Earliest,
		};

		// Assert
		options.PartitioningStrategy.ShouldBe(KafkaPartitioningStrategy.RoundRobin);
		options.DefaultTopic.ShouldBe("my-topic");
		options.DefaultPartitionCount.ShouldBe(6);
		options.DefaultReplicationFactor.ShouldBe((short)3);
		options.EnableIdempotentProducer.ShouldBeFalse();
		options.AcknowledgmentLevel.ShouldBe(KafkaAckLevel.Leader);
		options.MaxMessageSizeBytes.ShouldBe(2_097_152);
		options.EnableCompression.ShouldBeFalse();
		options.CompressionType.ShouldBe(KafkaCompressionType.Gzip);
		options.CompressionThreshold.ShouldBe(2048);
		options.RetrySettings.ShouldBe(retryOptions);
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("txn-1");
		options.AutoCreateTopics.ShouldBeTrue();
		options.ConsumerGroupId.ShouldBe("my-group");
		options.OffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
	}
}
