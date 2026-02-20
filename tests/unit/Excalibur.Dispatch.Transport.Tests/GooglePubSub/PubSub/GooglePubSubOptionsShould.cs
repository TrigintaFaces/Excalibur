// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GooglePubSubOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions();

		// Assert
		options.ProjectId.ShouldBe(string.Empty);
		options.TopicId.ShouldBe(string.Empty);
		options.SubscriptionId.ShouldBe(string.Empty);
		options.EnableEncryption.ShouldBeFalse();
		options.MaxConcurrentMessages.ShouldBe(0);
		options.Subscriber.ShouldNotBeNull();
		options.Telemetry.ShouldNotBeNull();
		options.Compression.ShouldNotBeNull();
	}

	[Fact]
	public void GenerateCorrectSubscriptionName()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			ProjectId = "my-project",
			SubscriptionId = "my-subscription",
		};

		// Assert
		options.SubscriptionName.ShouldBe("projects/my-project/subscriptions/my-subscription");
	}

	[Fact]
	public void GenerateCorrectTopicName()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			ProjectId = "my-project",
			TopicId = "my-topic",
		};

		// Assert
		options.TopicName.ShouldBe("projects/my-project/topics/my-topic");
	}

	[Fact]
	public void DelegateSubscriberPropertiesToSubOptions()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			MaxPullMessages = 200,
			AckDeadlineSeconds = 120,
			EnableAutoAckExtension = false,
			MaxConcurrentAcks = 50,
			EnableDeadLetterTopic = true,
			DeadLetterTopicId = "dlq-topic",
		};

		// Assert
		options.Subscriber.MaxPullMessages.ShouldBe(200);
		options.Subscriber.AckDeadlineSeconds.ShouldBe(120);
		options.Subscriber.EnableAutoAckExtension.ShouldBeFalse();
		options.Subscriber.MaxConcurrentAcks.ShouldBe(50);
		options.Subscriber.EnableDeadLetterTopic.ShouldBeTrue();
		options.Subscriber.DeadLetterTopicId.ShouldBe("dlq-topic");
	}

	[Fact]
	public void DelegateTelemetryPropertiesToSubOptions()
	{
		// Arrange & Act
		var options = new GooglePubSubOptions
		{
			EnableOpenTelemetry = false,
			ExportToCloudMonitoring = true,
			OtlpEndpoint = "http://otel:4317",
			TelemetryExportIntervalSeconds = 30,
			EnableTracePropagation = false,
			IncludeMessageAttributesInTraces = true,
			TracingSamplingRatio = 0.5,
		};

		// Assert
		options.Telemetry.EnableOpenTelemetry.ShouldBeFalse();
		options.Telemetry.ExportToCloudMonitoring.ShouldBeTrue();
		options.Telemetry.OtlpEndpoint.ShouldBe("http://otel:4317");
		options.Telemetry.TelemetryExportIntervalSeconds.ShouldBe(30);
		options.Telemetry.EnableTracePropagation.ShouldBeFalse();
		options.Telemetry.IncludeMessageAttributesInTraces.ShouldBeTrue();
		options.Telemetry.TracingSamplingRatio.ShouldBe(0.5);
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
			TelemetryResourceLabels = labels,
		};

		// Assert
		options.TelemetryResourceLabels.Count.ShouldBe(2);
		options.TelemetryResourceLabels["env"].ShouldBe("prod");
	}
}
