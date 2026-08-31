// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class GooglePubSubOptionsShould
{
	[Fact]
	public void GenerateCorrectSubscriptionName()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			Connection = new()
			{
				ProjectId = "my-project",
				SubscriptionId = "my-subscription",
			},
		};

		// Assert
		options.Connection.SubscriptionName.ShouldBe("projects/my-project/subscriptions/my-subscription");
	}

	[Fact]
	public void GenerateCorrectTopicName()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			Connection = new()
			{
				ProjectId = "my-project",
				TopicId = "my-topic",
			},
		};

		// Assert
		options.Connection.TopicName.ShouldBe("projects/my-project/topics/my-topic");
	}

	[Fact]
	public void AllowSettingTelemetryResourceLabels()
	{
		// Arrange & Act
		var labels = new Dictionary<string, string>
		{
			["env"] = "prod",
			["service"] = "orders",
		};

		var options = new GooglePubSubOptions
		{
			Telemetry =
			{
				TelemetryResourceLabels = labels,
			},
		};

		// Assert
		options.Telemetry.TelemetryResourceLabels.Count.ShouldBe(2);
		options.Telemetry.TelemetryResourceLabels["env"].ShouldBe("prod");
	}
}
