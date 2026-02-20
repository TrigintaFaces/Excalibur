// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsBatchOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqsBatchOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.MaxConcurrentReceiveBatches.ShouldBe(10);
		options.MaxConcurrentSendBatches.ShouldBe(10);
		options.LongPollingSeconds.ShouldBe(20);
		options.VisibilityTimeout.ShouldBe(300);
		options.BatchFlushIntervalMs.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue");
		var options = new SqsBatchOptions
		{
			QueueUrl = queueUrl,
			MaxConcurrentReceiveBatches = 5,
			MaxConcurrentSendBatches = 20,
			LongPollingSeconds = 10,
			VisibilityTimeout = 600,
			BatchFlushIntervalMs = 50,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.MaxConcurrentReceiveBatches.ShouldBe(5);
		options.MaxConcurrentSendBatches.ShouldBe(20);
		options.LongPollingSeconds.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(600);
		options.BatchFlushIntervalMs.ShouldBe(50);
	}
}
