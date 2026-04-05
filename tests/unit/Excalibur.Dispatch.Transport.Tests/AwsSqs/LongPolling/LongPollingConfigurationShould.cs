// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class LongPollingConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new LongPollingOptions();

		// Assert -- top-level properties
		config.QueueUrl.ShouldBeNull();
		config.DeleteAfterProcessing.ShouldBeTrue();
		config.IsFifoQueue.ShouldBeFalse();
		config.ReceiveRequestAttemptId.ShouldBeNull();

		// Assert -- Polling sub-options
		config.Polling.MaxWaitTimeSeconds.ShouldBe(20);
		config.Polling.MinWaitTimeSeconds.ShouldBe(1);
		config.Polling.MaxNumberOfMessages.ShouldBe(10);
		config.Polling.PollingIntervalMs.ShouldBe(1000);
		config.Polling.MaxMessagesPerReceive.ShouldBe(10);

		// Assert -- Visibility sub-options
		config.Visibility.VisibilityTimeoutSeconds.ShouldBe(30);
		config.Visibility.EnableOptimization.ShouldBeTrue();
		config.Visibility.BufferFactor.ShouldBe(0.1);

		// Assert -- Processing sub-options
		config.Processing.BatchSize.ShouldBe(10);
		config.Processing.ProcessingTimeout.ShouldBe(TimeSpan.FromMinutes(5));
		config.Processing.EnableRequestCoalescing.ShouldBeTrue();
		config.Processing.CoalescingWindowMs.ShouldBe(100);

		// Assert -- Retry sub-options
		config.Retry.MaxAttempts.ShouldBe(3);
		config.Retry.DelayMs.ShouldBe(1000);

		// Assert -- Adaptive sub-options
		config.Adaptive.Enabled.ShouldBeTrue();
		config.Adaptive.AdaptationWindow.ShouldBe(TimeSpan.FromMinutes(5));
		config.Adaptive.SmoothingFactor.ShouldBe(0.3);
		config.Adaptive.HighLoadThreshold.ShouldBe(0.8);
		config.Adaptive.LowLoadThreshold.ShouldBe(0.2);
	}

	[Fact]
	public void ComputeMaxWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingOptions();
		config.Polling.MaxWaitTimeSeconds = 15;

		// Act & Assert
		config.Polling.MaxWaitTime.ShouldBe(TimeSpan.FromSeconds(15));
	}

	[Fact]
	public void ComputeMinWaitTimeFromSeconds()
	{
		// Arrange
		var config = new LongPollingOptions();
		config.Polling.MinWaitTimeSeconds = 3;

		// Act & Assert
		config.Polling.MinWaitTime.ShouldBe(TimeSpan.FromSeconds(3));
	}

	[Fact]
	public void AllowSettingQueueUrl()
	{
		// Arrange & Act
		var config = new LongPollingOptions
		{
			QueueUrl = new Uri("https://sqs.us-east-1.amazonaws.com/123456789/my-queue"),
		};

		// Assert
		config.QueueUrl.ShouldNotBeNull();
		config.QueueUrl!.ToString().ShouldContain("my-queue");
	}

	[Fact]
	public void AllowSettingPollingTimingOptions()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Act
		config.Polling.MaxWaitTimeSeconds = 15;
		config.Polling.MinWaitTimeSeconds = 2;
		config.Polling.MaxNumberOfMessages = 5;

		// Assert
		config.Polling.MaxWaitTimeSeconds.ShouldBe(15);
		config.Polling.MinWaitTimeSeconds.ShouldBe(2);
		config.Polling.MaxNumberOfMessages.ShouldBe(5);
	}

	[Fact]
	public void AllowSettingVisibilityOptions()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Act
		config.Visibility.VisibilityTimeoutSeconds = 60;
		config.Visibility.EnableOptimization = false;
		config.Visibility.BufferFactor = 0.2;

		// Assert
		config.Visibility.VisibilityTimeoutSeconds.ShouldBe(60);
		config.Visibility.EnableOptimization.ShouldBeFalse();
		config.Visibility.BufferFactor.ShouldBe(0.2);
	}

	[Fact]
	public void AllowSettingAdaptiveOptions()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Act
		config.Adaptive.Enabled = false;
		config.Adaptive.SmoothingFactor = 0.5;
		config.Adaptive.HighLoadThreshold = 0.9;
		config.Adaptive.LowLoadThreshold = 0.1;

		// Assert
		config.Adaptive.Enabled.ShouldBeFalse();
		config.Adaptive.SmoothingFactor.ShouldBe(0.5);
		config.Adaptive.HighLoadThreshold.ShouldBe(0.9);
		config.Adaptive.LowLoadThreshold.ShouldBe(0.1);
	}

	[Fact]
	public void AllowSettingProcessingOptions()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Act
		config.Processing.BatchSize = 5;
		config.Processing.EnableRequestCoalescing = false;
		config.Processing.CoalescingWindowMs = 200;

		// Assert
		config.Processing.BatchSize.ShouldBe(5);
		config.Processing.EnableRequestCoalescing.ShouldBeFalse();
		config.Processing.CoalescingWindowMs.ShouldBe(200);
	}

	[Fact]
	public void HaveNullQueueUrlByDefault()
	{
		// Arrange
		var config = new LongPollingOptions();

		// Assert
		config.QueueUrl.ShouldBeNull();
	}
}
