// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait(TraitNames.Category, TestCategories.Unit)]
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
		options.ChannelAdapter.ConcurrentPollers.ShouldBe(10);
		options.ChannelAdapter.MaxConcurrentPollers.ShouldBe(20);
		options.ChannelAdapter.ReceiveChannelCapacity.ShouldBe(1000);
		options.ChannelAdapter.BatchIntervalMs.ShouldBe(100);
		options.Processing.ProcessorCount.ShouldBe(10);
		options.Processing.MaxConcurrentMessages.ShouldBe(100);
		options.Processing.DeleteBatchIntervalMs.ShouldBe(100);
		options.Batch.MaxConcurrentReceiveBatches.ShouldBe(10);
		options.Batch.MaxConcurrentSendBatches.ShouldBe(10);
		options.Batch.LongPollingSeconds.ShouldBe(20);
		options.Batch.BatchFlushIntervalMs.ShouldBe(100);
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
		};
		options.ChannelAdapter.ConcurrentPollers = 5;
		options.ChannelAdapter.MaxConcurrentPollers = 50;
		options.ChannelAdapter.ReceiveChannelCapacity = 2000;
		options.ChannelAdapter.BatchIntervalMs = 200;
		options.Processing.ProcessorCount = 20;
		options.Processing.MaxConcurrentMessages = 200;
		options.Processing.DeleteBatchIntervalMs = 50;
		options.Batch.MaxConcurrentReceiveBatches = 5;
		options.Batch.MaxConcurrentSendBatches = 20;
		options.Batch.LongPollingSeconds = 10;
		options.Batch.BatchFlushIntervalMs = 50;

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.ServiceUrl.ShouldBe(serviceUrl);
		options.VisibilityTimeout.ShouldBe(600);
		options.ChannelAdapter.ConcurrentPollers.ShouldBe(5);
		options.ChannelAdapter.MaxConcurrentPollers.ShouldBe(50);
		options.ChannelAdapter.ReceiveChannelCapacity.ShouldBe(2000);
		options.ChannelAdapter.BatchIntervalMs.ShouldBe(200);
		options.Processing.ProcessorCount.ShouldBe(20);
		options.Processing.MaxConcurrentMessages.ShouldBe(200);
		options.Processing.DeleteBatchIntervalMs.ShouldBe(50);
		options.Batch.MaxConcurrentReceiveBatches.ShouldBe(5);
		options.Batch.MaxConcurrentSendBatches.ShouldBe(20);
		options.Batch.LongPollingSeconds.ShouldBe(10);
		options.Batch.BatchFlushIntervalMs.ShouldBe(50);
	}
}
