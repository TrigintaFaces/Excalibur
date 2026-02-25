// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Kafka;

namespace Excalibur.Dispatch.Transport.Tests.Kafka.DeadLetter;

/// <summary>
/// Unit tests for <see cref="KafkaDeadLetterOptions"/> (S523.7).
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class KafkaDeadLetterOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Act
		var options = new KafkaDeadLetterOptions();

		// Assert
		options.TopicSuffix.ShouldBe(".dead-letter");
		options.ConsumerGroupId.ShouldBe("dlq-processor");
		options.MaxDeliveryAttempts.ShouldBe(5);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(14));
		options.IncludeStackTrace.ShouldBeTrue();
		options.ProduceTimeout.ShouldBe(TimeSpan.FromSeconds(30));
		options.ConsumeTimeout.ShouldBe(TimeSpan.FromSeconds(5));
		options.AutoCreateTopics.ShouldBeTrue();
		options.TopicPartitions.ShouldBe(1);
		options.TopicReplicationFactor.ShouldBe((short)-1);
	}

	[Theory]
	[InlineData("orders", "orders.dead-letter")]
	[InlineData("events", "events.dead-letter")]
	[InlineData("my-topic", "my-topic.dead-letter")]
	public void GetDeadLetterTopicName_UsesDefaultSuffix(string sourceTopic, string expected)
	{
		// Arrange
		var options = new KafkaDeadLetterOptions();

		// Act
		var dlqTopic = options.GetDeadLetterTopicName(sourceTopic);

		// Assert
		dlqTopic.ShouldBe(expected);
	}

	[Fact]
	public void GetDeadLetterTopicName_UsesCustomSuffix()
	{
		// Arrange
		var options = new KafkaDeadLetterOptions { TopicSuffix = ".dlq" };

		// Act
		var dlqTopic = options.GetDeadLetterTopicName("orders");

		// Assert
		dlqTopic.ShouldBe("orders.dlq");
	}

	[Fact]
	public void AllProperties_CanBeConfigured()
	{
		// Arrange & Act
		var options = new KafkaDeadLetterOptions
		{
			TopicSuffix = ".error",
			ConsumerGroupId = "custom-dlq-group",
			MaxDeliveryAttempts = 3,
			MessageRetentionPeriod = TimeSpan.FromDays(7),
			IncludeStackTrace = false,
			ProduceTimeout = TimeSpan.FromSeconds(10),
			ConsumeTimeout = TimeSpan.FromSeconds(2),
			AutoCreateTopics = false,
			TopicPartitions = 3,
			TopicReplicationFactor = 2,
		};

		// Assert
		options.TopicSuffix.ShouldBe(".error");
		options.ConsumerGroupId.ShouldBe("custom-dlq-group");
		options.MaxDeliveryAttempts.ShouldBe(3);
		options.MessageRetentionPeriod.ShouldBe(TimeSpan.FromDays(7));
		options.IncludeStackTrace.ShouldBeFalse();
		options.ProduceTimeout.ShouldBe(TimeSpan.FromSeconds(10));
		options.ConsumeTimeout.ShouldBe(TimeSpan.FromSeconds(2));
		options.AutoCreateTopics.ShouldBeFalse();
		options.TopicPartitions.ShouldBe(3);
		options.TopicReplicationFactor.ShouldBe((short)2);
	}
}
