// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LongPollingOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new LongPollingOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.MinPollers.ShouldBe(5);
		options.MaxPollers.ShouldBe(20);
		options.ChannelCapacity.ShouldBe(1000);
		options.VisibilityTimeout.ShouldBe(300);
		options.AdaptiveIntervalSeconds.ShouldBe(30);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
		var options = new LongPollingOptions
		{
			QueueUrl = queueUrl,
			MinPollers = 2,
			MaxPollers = 50,
			ChannelCapacity = 5000,
			VisibilityTimeout = 600,
			AdaptiveIntervalSeconds = 60,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.MinPollers.ShouldBe(2);
		options.MaxPollers.ShouldBe(50);
		options.ChannelCapacity.ShouldBe(5000);
		options.VisibilityTimeout.ShouldBe(600);
		options.AdaptiveIntervalSeconds.ShouldBe(60);
	}
}
