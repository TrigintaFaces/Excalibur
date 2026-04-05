// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Platform")]
public sealed class BatchOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new BatchOptions();

		// Assert
		config.MaxMessagesPerBatch.ShouldBe(1000);
		config.MinMessagesPerBatch.ShouldBe(1);
		config.MaxBatchWaitTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		config.MaxBatchSizeBytes.ShouldBe(10 * 1024 * 1024);
		config.EnableAdaptiveBatching.ShouldBeTrue();
		config.TargetBatchProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		config.ConcurrentBatchProcessors.ShouldBe(Environment.ProcessorCount);
		config.PreserveMessageOrder.ShouldBeFalse();
		config.EnableMetricsCompression.ShouldBeTrue();
		config.Acknowledgment.ShouldNotBeNull();
		config.Acknowledgment.AckDeadlineSeconds.ShouldBe(600);
		config.Acknowledgment.AckStrategy.ShouldBe(BatchAckStrategy.OnSuccess);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var config = new BatchOptions
		{
			MaxMessagesPerBatch = 500,
			MinMessagesPerBatch = 10,
			MaxBatchWaitTime = TimeSpan.FromMilliseconds(200),
			MaxBatchSizeBytes = 5 * 1024 * 1024,
			EnableAdaptiveBatching = false,
			TargetBatchProcessingTime = TimeSpan.FromMilliseconds(100),
			ConcurrentBatchProcessors = 4,
			PreserveMessageOrder = true,
			EnableMetricsCompression = false,
			Acknowledgment = new BatchAcknowledgmentOptions
			{
				AckDeadlineSeconds = 300,
				AckStrategy = BatchAckStrategy.BatchComplete,
			},
		};

		// Assert
		config.MaxMessagesPerBatch.ShouldBe(500);
		config.MinMessagesPerBatch.ShouldBe(10);
		config.MaxBatchWaitTime.ShouldBe(TimeSpan.FromMilliseconds(200));
		config.MaxBatchSizeBytes.ShouldBe(5 * 1024 * 1024);
		config.EnableAdaptiveBatching.ShouldBeFalse();
		config.TargetBatchProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		config.ConcurrentBatchProcessors.ShouldBe(4);
		config.PreserveMessageOrder.ShouldBeTrue();
		config.EnableMetricsCompression.ShouldBeFalse();
		config.Acknowledgment.AckDeadlineSeconds.ShouldBe(300);
		config.Acknowledgment.AckStrategy.ShouldBe(BatchAckStrategy.BatchComplete);
	}

	[Fact]
	public void CloneProduceIdenticalCopy()
	{
		// Arrange
		var original = new BatchOptions
		{
			MaxMessagesPerBatch = 200,
			MinMessagesPerBatch = 5,
			MaxBatchWaitTime = TimeSpan.FromMilliseconds(500),
			PreserveMessageOrder = true,
			Acknowledgment = new BatchAcknowledgmentOptions
			{
				AckStrategy = BatchAckStrategy.Manual,
			},
		};

		// Act
		var clone = original.Clone();

		// Assert
		clone.MaxMessagesPerBatch.ShouldBe(200);
		clone.MinMessagesPerBatch.ShouldBe(5);
		clone.MaxBatchWaitTime.ShouldBe(TimeSpan.FromMilliseconds(500));
		clone.PreserveMessageOrder.ShouldBeTrue();
		clone.Acknowledgment.AckStrategy.ShouldBe(BatchAckStrategy.Manual);
	}

	[Fact]
	public void CloneNotReturnSameReference()
	{
		// Arrange
		var original = new BatchOptions();

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
		clone.Acknowledgment.ShouldNotBeSameAs(original.Acknowledgment);
	}

	[Theory]
	[InlineData(BatchAckStrategy.OnSuccess, 0)]
	[InlineData(BatchAckStrategy.BatchComplete, 1)]
	[InlineData(BatchAckStrategy.Individual, 2)]
	[InlineData(BatchAckStrategy.Manual, 3)]
	public void BatchAckStrategyHaveCorrectValues(BatchAckStrategy strategy, int expectedValue)
	{
		((int)strategy).ShouldBe(expectedValue);
	}

	[Fact]
	public void AcknowledgmentOptionsHaveCorrectDefaults()
	{
		var ack = new BatchAcknowledgmentOptions();

		ack.AckDeadlineSeconds.ShouldBe(600);
		ack.AckStrategy.ShouldBe(BatchAckStrategy.OnSuccess);
	}
}
