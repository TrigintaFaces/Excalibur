// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class HighThroughputSqsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new HighThroughputSqsOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.MaxConcurrency.ShouldBe(10);
		options.ConcurrentPollers.ShouldBe(5);
		options.MaxConcurrentPollers.ShouldBe(10);
		options.ChannelCapacity.ShouldBe(1000);
		options.MaxConcurrentMessages.ShouldBe(100);
		options.BatchDeleteIntervalMs.ShouldBe(1000);
		options.BatchSize.ShouldBe(10);
		options.VisibilityTimeout.ShouldBe(30);
		options.WaitTimeSeconds.ShouldBe(20);
		options.EnableBatching.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/high-throughput-q");

		// Act
		var options = new HighThroughputSqsOptions
		{
			QueueUrl = queueUrl,
			MaxConcurrency = 50,
			ConcurrentPollers = 20,
			MaxConcurrentPollers = 30,
			ChannelCapacity = 5000,
			MaxConcurrentMessages = 500,
			BatchDeleteIntervalMs = 500,
			BatchSize = 5,
			VisibilityTimeout = 60,
			WaitTimeSeconds = 10,
			EnableBatching = false,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.MaxConcurrency.ShouldBe(50);
		options.ConcurrentPollers.ShouldBe(20);
		options.MaxConcurrentPollers.ShouldBe(30);
		options.ChannelCapacity.ShouldBe(5000);
		options.MaxConcurrentMessages.ShouldBe(500);
		options.BatchDeleteIntervalMs.ShouldBe(500);
		options.BatchSize.ShouldBe(5);
		options.VisibilityTimeout.ShouldBe(60);
		options.WaitTimeSeconds.ShouldBe(10);
		options.EnableBatching.ShouldBeFalse();
	}
}
