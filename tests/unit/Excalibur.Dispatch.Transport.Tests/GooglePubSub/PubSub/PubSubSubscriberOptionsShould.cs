// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PubSubSubscriberOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new PubSubSubscriberOptions();

		// Assert
		options.MaxPullMessages.ShouldBe(100);
		options.AckDeadlineSeconds.ShouldBe(60);
		options.EnableAutoAckExtension.ShouldBeTrue();
		options.MaxConcurrentAcks.ShouldBe(10);
		options.EnableDeadLetterTopic.ShouldBeFalse();
		options.DeadLetterTopicId.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new PubSubSubscriberOptions
		{
			MaxPullMessages = 250,
			AckDeadlineSeconds = 120,
			EnableAutoAckExtension = false,
			MaxConcurrentAcks = 50,
			EnableDeadLetterTopic = true,
			DeadLetterTopicId = "my-dlq-topic",
		};

		// Assert
		options.MaxPullMessages.ShouldBe(250);
		options.AckDeadlineSeconds.ShouldBe(120);
		options.EnableAutoAckExtension.ShouldBeFalse();
		options.MaxConcurrentAcks.ShouldBe(50);
		options.EnableDeadLetterTopic.ShouldBeTrue();
		options.DeadLetterTopicId.ShouldBe("my-dlq-topic");
	}
}
