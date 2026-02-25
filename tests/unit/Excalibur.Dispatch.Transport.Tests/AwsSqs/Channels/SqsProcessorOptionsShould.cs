// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsProcessorOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new SqsProcessorOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.ProcessorCount.ShouldBe(10);
		options.MaxConcurrentMessages.ShouldBe(100);
		options.DeleteBatchIntervalMs.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var queueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/processor-q");
		var options = new SqsProcessorOptions
		{
			QueueUrl = queueUrl,
			ProcessorCount = 20,
			MaxConcurrentMessages = 500,
			DeleteBatchIntervalMs = 50,
		};

		// Assert
		options.QueueUrl.ShouldBe(queueUrl);
		options.ProcessorCount.ShouldBe(20);
		options.MaxConcurrentMessages.ShouldBe(500);
		options.DeleteBatchIntervalMs.ShouldBe(50);
	}
}
