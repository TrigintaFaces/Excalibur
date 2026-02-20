// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsChannelInfrastructureOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqsChannelInfrastructureOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.ServiceUrl.ShouldBeNull();
		options.VisibilityTimeout.ShouldBe(300);
		options.ConcurrentPollers.ShouldBe(10);
		options.MaxConcurrentPollers.ShouldBe(20);
		options.ReceiveChannelCapacity.ShouldBe(1000);
		options.BatchIntervalMs.ShouldBe(100);
		options.ProcessorCount.ShouldBe(10);
		options.MaxConcurrentMessages.ShouldBe(100);
		options.DeleteBatchIntervalMs.ShouldBe(100);
		options.MaxConcurrentReceiveBatches.ShouldBe(10);
		options.MaxConcurrentSendBatches.ShouldBe(10);
		options.LongPollingSeconds.ShouldBe(20);
		options.BatchFlushIntervalMs.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/infra-q");
		var serviceUrl = new Uri("http://localhost:4566");
		var options = new SqsChannelInfrastructureOptions
		{
			QueueUrl = queueUrl,
			ServiceUrl = serviceUrl,
			VisibilityTimeout = 600,
			ConcurrentPollers = 5,
			MaxConcurrentPollers = 50,
			ReceiveChannelCapacity = 2000,
			BatchIntervalMs = 200,
			ProcessorCount = 20,
			MaxConcurrentMessages = 200,
			DeleteBatchIntervalMs = 50,
			MaxConcurrentReceiveBatches = 5,
			MaxConcurrentSendBatches = 20,
			LongPollingSeconds = 10,
			BatchFlushIntervalMs = 50,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.ServiceUrl.ShouldBe(serviceUrl);
		options.VisibilityTimeout.ShouldBe(600);
		options.ConcurrentPollers.ShouldBe(5);
		options.MaxConcurrentPollers.ShouldBe(50);
		options.ReceiveChannelCapacity.ShouldBe(2000);
		options.BatchIntervalMs.ShouldBe(200);
		options.ProcessorCount.ShouldBe(20);
		options.MaxConcurrentMessages.ShouldBe(200);
		options.DeleteBatchIntervalMs.ShouldBe(50);
		options.MaxConcurrentReceiveBatches.ShouldBe(5);
		options.MaxConcurrentSendBatches.ShouldBe(20);
		options.LongPollingSeconds.ShouldBe(10);
		options.BatchFlushIntervalMs.ShouldBe(50);
	}
}
