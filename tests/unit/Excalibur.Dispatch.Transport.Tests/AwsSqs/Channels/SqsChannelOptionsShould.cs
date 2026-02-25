// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsChannelOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqsChannelOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.ConcurrentPollers.ShouldBe(10);
		options.MaxConcurrentPollers.ShouldBe(20);
		options.ReceiveChannelCapacity.ShouldBe(1000);
		options.VisibilityTimeout.ShouldBe(300);
		options.BatchIntervalMs.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/test");
		var options = new SqsChannelOptions
		{
			QueueUrl = queueUrl,
			ConcurrentPollers = 5,
			MaxConcurrentPollers = 50,
			ReceiveChannelCapacity = 2000,
			VisibilityTimeout = 120,
			BatchIntervalMs = 200,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.ConcurrentPollers.ShouldBe(5);
		options.MaxConcurrentPollers.ShouldBe(50);
		options.ReceiveChannelCapacity.ShouldBe(2000);
		options.VisibilityTimeout.ShouldBe(120);
		options.BatchIntervalMs.ShouldBe(200);
	}
}
