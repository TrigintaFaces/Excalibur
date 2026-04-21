// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for KafkaOptions configuration.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class KafkaOptionsShould : UnitTestBase
{
	[Fact]
	public void Create_WithDefaults_HasExpectedDefaultValues()
	{
		// Arrange & Act
		var options = new KafkaOptions();

		// Assert
		options.Topic.ShouldBe(string.Empty);
		options.BootstrapServers.ShouldBe("localhost:9092");
		options.ConsumerGroup.ShouldBe("dispatch-consumer");
		options.EnableEncryption.ShouldBeFalse();
		options.Consumer.MaxBatchSize.ShouldBe(100);
		options.Consumer.MaxBatchWaitMs.ShouldBe(1000);
		options.Consumer.EnableAutoCommit.ShouldBeFalse();
		options.Consumer.AutoCommitIntervalMs.ShouldBe(5000);
		options.Consumer.SessionTimeoutMs.ShouldBe(30000);
		options.Consumer.MaxPollIntervalMs.ShouldBe(300000);
		options.Consumer.AutoOffsetReset.ShouldBe("latest");
		options.Consumer.EnablePartitionEof.ShouldBeFalse();
		options.Consumer.QueuedMinMessages.ShouldBe(1000);
		options.Consumer.MaxConcurrentCommits.ShouldBe(10);
		_ = options.AdditionalConfig.ShouldNotBeNull();
		options.AdditionalConfig.ShouldBeEmpty();
	}

	[Fact]
	public void BootstrapServers_CanBeCustomized()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.BootstrapServers = "kafka1:9092,kafka2:9092";

		// Assert
		options.BootstrapServers.ShouldBe("kafka1:9092,kafka2:9092");
	}

	[Fact]
	public void ConsumerGroup_CanBeCustomized()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.ConsumerGroup = "my-consumer-group";

		// Assert
		options.ConsumerGroup.ShouldBe("my-consumer-group");
	}

	[Fact]
	public void MaxBatchSize_CanBeCustomized()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.Consumer.MaxBatchSize = 500;

		// Assert
		options.Consumer.MaxBatchSize.ShouldBe(500);
	}

	[Fact]
	public void EnableAutoCommit_CanBeEnabled()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.Consumer.EnableAutoCommit = true;

		// Assert
		options.Consumer.EnableAutoCommit.ShouldBeTrue();
	}

	[Fact]
	public void AutoOffsetReset_CanBeChangedToEarliest()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.Consumer.AutoOffsetReset = "earliest";

		// Assert
		options.Consumer.AutoOffsetReset.ShouldBe("earliest");
	}

	[Fact]
	public void SessionTimeoutMs_CanBeCustomized()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.Consumer.SessionTimeoutMs = 60000;

		// Assert
		options.Consumer.SessionTimeoutMs.ShouldBe(60000);
	}

	[Fact]
	public void AdditionalConfig_CanAddProperties()
	{
		// Arrange
		var options = new KafkaOptions();

		// Act
		options.AdditionalConfig["security.protocol"] = "SASL_SSL";

		// Assert
		options.AdditionalConfig.ShouldContainKey("security.protocol");
		options.AdditionalConfig["security.protocol"].ShouldBe("SASL_SSL");
	}
}
