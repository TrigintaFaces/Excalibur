// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaConsumerConfigBuilder"/>, covering the cooperative-sticky
/// partition-assignment default and its protocol gating (bd-89dfyw).
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
public sealed class KafkaConsumerConfigBuilderShould : UnitTestBase
{
	[Fact]
	public void Build_WithDefaults_UsesCooperativeStickyAssignment()
	{
		// Arrange
		var options = new KafkaOptions { BootstrapServers = "localhost:9092" };

		// Act
		var config = KafkaConsumerConfigBuilder.Build(options);

		// Assert — RED on pre-fix: the builder never set PartitionAssignmentStrategy (was null).
		config.PartitionAssignmentStrategy.ShouldBe(PartitionAssignmentStrategy.CooperativeSticky);
	}

	[Fact]
	public void Build_WhenConsumerGroupProtocol_OmitsAssignmentStrategy()
	{
		// Arrange — KIP-848 consumer protocol performs server-side assignment; the client property
		// must not be set or librdkafka rejects it.
		var options = new KafkaOptions
		{
			BootstrapServers = "localhost:9092",
			GroupProtocol = GroupProtocol.Consumer,
		};

		// Act
		var config = KafkaConsumerConfigBuilder.Build(options);

		// Assert
		config.PartitionAssignmentStrategy.ShouldBeNull();
	}

	[Fact]
	public void Build_WhenStrategyOverridden_HonorsExplicitValue()
	{
		// Arrange
		var options = new KafkaOptions { BootstrapServers = "localhost:9092" };
		options.Consumer.PartitionAssignmentStrategy = PartitionAssignmentStrategy.Range;

		// Act
		var config = KafkaConsumerConfigBuilder.Build(options);

		// Assert — RED on pre-fix: the configured override was ignored (property never written).
		config.PartitionAssignmentStrategy.ShouldBe(PartitionAssignmentStrategy.Range);
	}

	[Fact]
	public void Build_WhenStrategyCleared_LeavesAssignmentUnset()
	{
		// Arrange — null opts out, deferring to the broker/client default.
		var options = new KafkaOptions { BootstrapServers = "localhost:9092" };
		options.Consumer.PartitionAssignmentStrategy = null;

		// Act
		var config = KafkaConsumerConfigBuilder.Build(options);

		// Assert
		config.PartitionAssignmentStrategy.ShouldBeNull();
	}
}
