// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.Transport;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class KafkaTransportAdapterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaultsForKafkaTransportOptions()
	{
		// Arrange & Act
		var options = new KafkaTransportOptions();

		// Assert
		options.Name.ShouldBeNull();
		options.BootstrapServers.ShouldBe("localhost:9092");
	}

	[Fact]
	public void AllowSettingKafkaTransportOptionsProperties()
	{
		// Arrange & Act
		var options = new KafkaTransportOptions
		{
			Name = "my-kafka",
			BootstrapServers = "broker1:9092,broker2:9092",
		};

		// Assert
		options.Name.ShouldBe("my-kafka");
		options.BootstrapServers.ShouldBe("broker1:9092,broker2:9092");
	}

	[Fact]
	public void HaveCorrectDefaultsForKafkaProducerOptions()
	{
		// Arrange & Act
		var options = new KafkaProducerOptions();

		// Assert
		options.ClientId.ShouldBe("dispatch-producer");
		options.Acks.ShouldBe(KafkaAckLevel.All);
		options.EnableIdempotence.ShouldBeTrue();
		options.CompressionType.ShouldBe(KafkaCompressionType.None);
		options.EnableTransactions.ShouldBeFalse();
		options.TransactionalId.ShouldBeNull();
		options.LingerMs.ShouldBe(TimeSpan.FromMilliseconds(5));
		options.BatchSize.ShouldBe(16384);
		options.AdditionalConfig.ShouldNotBeNull();
		options.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllKafkaProducerOptionsProperties()
	{
		// Arrange & Act
		var options = new KafkaProducerOptions
		{
			ClientId = "custom-producer",
			Acks = KafkaAckLevel.Leader,
			EnableIdempotence = false,
			CompressionType = KafkaCompressionType.Snappy,
			EnableTransactions = true,
			TransactionalId = "txn-prod-1",
			LingerMs = TimeSpan.FromMilliseconds(10),
			BatchSize = 32768,
		};

		// Assert
		options.ClientId.ShouldBe("custom-producer");
		options.Acks.ShouldBe(KafkaAckLevel.Leader);
		options.EnableIdempotence.ShouldBeFalse();
		options.CompressionType.ShouldBe(KafkaCompressionType.Snappy);
		options.EnableTransactions.ShouldBeTrue();
		options.TransactionalId.ShouldBe("txn-prod-1");
		options.LingerMs.ShouldBe(TimeSpan.FromMilliseconds(10));
		options.BatchSize.ShouldBe(32768);
	}

	[Fact]
	public void SupportAdditionalConfigForProducer()
	{
		// Arrange
		var options = new KafkaProducerOptions();

		// Act
		options.AdditionalConfig["message.max.bytes"] = "2097152";

		// Assert
		options.AdditionalConfig.ShouldContainKeyAndValue("message.max.bytes", "2097152");
	}

	[Fact]
	public void HaveCorrectDefaultsForKafkaConsumerOptions()
	{
		// Arrange & Act
		var options = new KafkaConsumerOptions();

		// Assert
		options.GroupId.ShouldBe("dispatch-consumer");
		options.AutoOffsetReset.ShouldBe(KafkaOffsetReset.Latest);
		options.EnableAutoCommit.ShouldBeFalse();
		options.AutoCommitInterval.ShouldBe(TimeSpan.FromSeconds(5));
		options.SessionTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.MaxPollInterval.ShouldBe(TimeSpan.FromMinutes(5));
		options.MaxBatchSize.ShouldBe(100);
		options.AdditionalConfig.ShouldNotBeNull();
		options.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllKafkaConsumerOptionsProperties()
	{
		// Arrange & Act
		var options = new KafkaConsumerOptions
		{
			GroupId = "my-consumer-group",
			AutoOffsetReset = KafkaOffsetReset.Earliest,
			EnableAutoCommit = true,
			AutoCommitInterval = TimeSpan.FromSeconds(10),
			SessionTimeout = TimeSpan.FromSeconds(45),
			MaxPollInterval = TimeSpan.FromMinutes(10),
			MaxBatchSize = 500,
		};

		// Assert
		options.GroupId.ShouldBe("my-consumer-group");
		options.AutoOffsetReset.ShouldBe(KafkaOffsetReset.Earliest);
		options.EnableAutoCommit.ShouldBeTrue();
		options.AutoCommitInterval.ShouldBe(TimeSpan.FromSeconds(10));
		options.SessionTimeout.ShouldBe(TimeSpan.FromSeconds(45));
		options.MaxPollInterval.ShouldBe(TimeSpan.FromMinutes(10));
		options.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void SupportAdditionalConfigForConsumer()
	{
		// Arrange
		var options = new KafkaConsumerOptions();

		// Act
		options.AdditionalConfig["fetch.min.bytes"] = "1024";

		// Assert
		options.AdditionalConfig.ShouldContainKeyAndValue("fetch.min.bytes", "1024");
	}
}
