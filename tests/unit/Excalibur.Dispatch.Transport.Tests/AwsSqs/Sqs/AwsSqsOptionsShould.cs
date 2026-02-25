// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class AwsSqsOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new AwsSqsOptions();

		// Assert
		options.QueueUrl.ShouldBeNull();
		options.MessageRetentionPeriod.ShouldBe(345600);
		options.UseFifoQueue.ShouldBeFalse();
		options.ContentBasedDeduplication.ShouldBeFalse();
		options.BatchConfig.ShouldBeNull();
		options.LongPollingConfig.ShouldBeNull();
		options.KmsMasterKeyId.ShouldBeNull();
		options.KmsDataKeyReusePeriodSeconds.ShouldBe(300);
		options.WaitTimeSeconds.ShouldBe(TimeSpan.FromSeconds(20));
		options.VisibilityTimeout.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void AllowSettingQueueUrl()
	{
		// Arrange & Act
		var options = new AwsSqsOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Assert
		options.QueueUrl.ShouldNotBeNull();
	}

	[Fact]
	public void AllowSettingFifoConfiguration()
	{
		// Arrange & Act
		var options = new AwsSqsOptions
		{
			UseFifoQueue = true,
			ContentBasedDeduplication = true,
		};

		// Assert
		options.UseFifoQueue.ShouldBeTrue();
		options.ContentBasedDeduplication.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingKmsEncryption()
	{
		// Arrange & Act
		var options = new AwsSqsOptions
		{
			KmsMasterKeyId = "alias/my-sqs-key",
			KmsDataKeyReusePeriodSeconds = 600,
		};

		// Assert
		options.KmsMasterKeyId.ShouldBe("alias/my-sqs-key");
		options.KmsDataKeyReusePeriodSeconds.ShouldBe(600);
	}

	[Fact]
	public void AllowSettingMessageRetentionPeriod()
	{
		// Arrange & Act
		var options = new AwsSqsOptions { MessageRetentionPeriod = 1209600 }; // 14 days

		// Assert
		options.MessageRetentionPeriod.ShouldBe(1209600);
	}
}
