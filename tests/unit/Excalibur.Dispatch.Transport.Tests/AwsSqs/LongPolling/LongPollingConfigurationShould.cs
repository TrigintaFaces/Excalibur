// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class LongPollingConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new LongPollingConfiguration();

		// Assert
		config.MaxWaitTimeSeconds.ShouldBe(20);
		config.MinWaitTimeSeconds.ShouldBe(1);
		config.MaxNumberOfMessages.ShouldBe(10);
		config.VisibilityTimeoutSeconds.ShouldBe(30);
		config.EnableAdaptivePolling.ShouldBeTrue();
		config.PollingIntervalMs.ShouldBe(1000);
		config.MaxRetryAttempts.ShouldBe(3);
		config.RetryDelayMs.ShouldBe(1000);
		config.QueueUrl.ShouldBeNull();
		config.DeleteAfterProcessing.ShouldBeTrue();
		config.BatchSize.ShouldBe(10);
		config.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		config.IsFifoQueue.ShouldBeFalse();
		config.ReceiveRequestAttemptId.ShouldBeNull();
		config.EnableRequestCoalescing.ShouldBeTrue();
		config.CoalescingWindow.ShouldBe(100);
		config.MaxMessagesPerReceive.ShouldBe(10);
		config.AdaptationWindow.ShouldBe(TimeSpan.FromMinutes(5));
		config.SmoothingFactor.ShouldBe(0.3);
		config.HighLoadThreshold.ShouldBe(0.8);
		config.LowLoadThreshold.ShouldBe(0.2);
		config.EnableVisibilityTimeoutOptimization.ShouldBeTrue();
		config.VisibilityTimeoutBufferFactor.ShouldBe(0.1);
	}

	[Fact]
	public void ComputeMaxWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingConfiguration { MaxWaitTimeSeconds = 15 };

		// Act & Assert
		config.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void ComputeMinWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingConfiguration { MinWaitTimeSeconds = 3 };

		// Act & Assert
		config.MinWaitTime.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void ValidateSuccessfullyWithValidConfig()
	{
		// Arrange
		var config = new LongPollingConfiguration
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Act & Assert — should not throw
		config.Validate();
	}

	[Fact]
	public void ValidateThrowsForMaxWaitTimeAbove20()
	{
		// Arrange
		var config = new LongPollingConfiguration
		{
			MaxWaitTimeSeconds = 21,
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
	}

	[Fact]
	public void ValidateThrowsForNegativeMinWaitTime()
	{
		// Arrange
		var config = new LongPollingConfiguration
		{
			MinWaitTimeSeconds = -1,
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
	}

	[Fact]
	public void ValidateThrowsForMaxNumberOfMessagesOutOfRange()
	{
		// Arrange
		var config = new LongPollingConfiguration
		{
			MaxNumberOfMessages = 11,
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
	}

	[Fact]
	public void ValidateThrowsForVisibilityTimeoutOutOfRange()
	{
		// Arrange
		var config = new LongPollingConfiguration
		{
			VisibilityTimeoutSeconds = 43201,
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => config.Validate());
	}

	[Fact]
	public void ValidateThrowsForNullQueueUrl()
	{
		// Arrange
		var config = new LongPollingConfiguration { QueueUrl = null };

		// Act & Assert
		Should.Throw<ArgumentException>(() => config.Validate());
	}
}
