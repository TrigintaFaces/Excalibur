// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.CloudEvents;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class GooglePubSubCloudEventOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new GooglePubSubCloudEventOptions();

		// Assert
		options.UseOrderingKeys.ShouldBeTrue();
		options.MaxMessageSizeBytes.ShouldBe(10 * 1024 * 1024);
		options.EnableDeduplication.ShouldBeTrue();
		options.ProjectId.ShouldBeNull();
		options.DefaultTopic.ShouldBeNull();
		options.DefaultSubscription.ShouldBeNull();
		options.EnableCompression.ShouldBeFalse();
		options.CompressionThreshold.ShouldBe(1024 * 1024);
		options.UseExactlyOnceDelivery.ShouldBeFalse();
		options.AckDeadline.ShouldBe(TimeSpan.FromMinutes(10));
		options.RetryPolicy.ShouldNotBeNull();
		options.EnableCloudMonitoring.ShouldBeFalse();
		options.CloudMonitoringPrefix.ShouldBe("dispatch.cloudevents");
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var options = new GooglePubSubCloudEventOptions
		{
			UseOrderingKeys = false,
			MaxMessageSizeBytes = 1024 * 1024,
			EnableDeduplication = false,
			ProjectId = "my-project",
			DefaultTopic = "my-topic",
			DefaultSubscription = "my-sub",
			EnableCompression = true,
			CompressionThreshold = 512 * 1024,
			UseExactlyOnceDelivery = true,
			AckDeadline = TimeSpan.FromMinutes(5),
			EnableCloudMonitoring = true,
			CloudMonitoringPrefix = "custom.prefix",
		};

		// Assert
		options.UseOrderingKeys.ShouldBeFalse();
		options.MaxMessageSizeBytes.ShouldBe(1024 * 1024);
		options.EnableDeduplication.ShouldBeFalse();
		options.ProjectId.ShouldBe("my-project");
		options.DefaultTopic.ShouldBe("my-topic");
		options.DefaultSubscription.ShouldBe("my-sub");
		options.EnableCompression.ShouldBeTrue();
		options.CompressionThreshold.ShouldBe(512 * 1024);
		options.UseExactlyOnceDelivery.ShouldBeTrue();
		options.AckDeadline.ShouldBe(TimeSpan.FromMinutes(5));
		options.EnableCloudMonitoring.ShouldBeTrue();
		options.CloudMonitoringPrefix.ShouldBe("custom.prefix");
	}

	[Fact]
	public void RetryPolicyHaveCorrectDefaults()
	{
		// Arrange & Act
		var policy = new GooglePubSubRetryPolicy();

		// Assert
		policy.MaxRetryAttempts.ShouldBe(3);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(100));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(60));
		policy.DelayMultiplier.ShouldBe(2.0);
		policy.UseJitter.ShouldBeTrue();
	}

	[Fact]
	public void RetryPolicyAllowSettingAllProperties()
	{
		// Arrange & Act
		var policy = new GooglePubSubRetryPolicy
		{
			MaxRetryAttempts = 5,
			InitialDelay = TimeSpan.FromMilliseconds(200),
			MaxDelay = TimeSpan.FromSeconds(120),
			DelayMultiplier = 3.0,
			UseJitter = false,
		};

		// Assert
		policy.MaxRetryAttempts.ShouldBe(5);
		policy.InitialDelay.ShouldBe(TimeSpan.FromMilliseconds(200));
		policy.MaxDelay.ShouldBe(TimeSpan.FromSeconds(120));
		policy.DelayMultiplier.ShouldBe(3.0);
		policy.UseJitter.ShouldBeFalse();
	}
}
