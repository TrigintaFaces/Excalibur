// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Confluent.Kafka;

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka;

/// <summary>
/// Unit tests for <see cref="KafkaConsumerConfigBuilder"/>, covering the cooperative-sticky
/// partition-assignment default and its protocol gating (bd-89dfyw).
///
/// The fixtures set a TLS-bearing security protocol because the builder refuses to produce a
/// configuration that would connect in the clear. They are exercising assignment strategy under the
/// shipping security posture rather than opting out of it.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Platform)]
public sealed class KafkaConsumerConfigBuilderShould : UnitTestBase
{
	[Fact]
	public void Build_WithDefaults_UsesCooperativeStickyAssignment()
	{
		// Arrange
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };

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
			SecurityProtocol = SecurityProtocol.Ssl,
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
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };
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
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };
		options.Consumer.PartitionAssignmentStrategy = null;

		// Act
		var config = KafkaConsumerConfigBuilder.Build(options);

		// Assert
		config.PartitionAssignmentStrategy.ShouldBeNull();
	}
	/// <summary>
	/// SAFETY. The consumer must be built with the offset store under our control. librdkafka defaults
	/// enable.auto.offset.store to TRUE, which records offset+1 when a message is HANDED TO the
	/// application rather than when it is finished with — so a commit of stored offsets can move the
	/// group past work still inside handlers, and a rebalance at that moment loses it silently.
	/// </summary>
	[Fact]
	public void Build_AlwaysDisablesAutoOffsetStore()
	{
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };

		var config = KafkaConsumerConfigBuilder.Build(options);

		// RED on pre-fix: the builder never set this, so it was null and librdkafka's default (true) applied.
		config.EnableAutoOffsetStore.ShouldBe(false);
	}

	/// <summary>
	/// LIVENESS for the guard itself. AdditionalConfig is applied AFTER the strongly-typed settings and
	/// can set any librdkafka property by name, so it is the one path that could put auto-offset-store
	/// back. This is the single point that makes the stored-position rule unbypassable, and without an
	/// arm here it could be deleted with every other test still green.
	/// </summary>
	[Fact]
	public void Build_RefusesWhenAdditionalConfigReEnablesAutoOffsetStore()
	{
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };
		options.AdditionalConfig["enable.auto.offset.store"] = "true";

		var ex = Should.Throw<InvalidOperationException>(() => KafkaConsumerConfigBuilder.Build(options));

		// The message has to tell the consumer WHY, not merely that it was refused: they set this
		// deliberately and will otherwise reach for a way around it.
		ex.Message.ShouldContain("enable.auto.offset.store");
		ex.Message.ShouldContain("before the handler");
	}

	/// <summary>
	/// The guard must not fire on the value it is there to ALLOW. Without this arm a guard that refused
	/// every configuration would satisfy the arm above perfectly.
	/// </summary>
	[Fact]
	public void Build_AllowsAdditionalConfigThatLeavesAutoOffsetStoreDisabled()
	{
		var options = new KafkaOptions { BootstrapServers = "localhost:9092", SecurityProtocol = SecurityProtocol.Ssl };
		options.AdditionalConfig["enable.auto.offset.store"] = "false";

		var config = KafkaConsumerConfigBuilder.Build(options);

		config.EnableAutoOffsetStore.ShouldBe(false);
	}

}
