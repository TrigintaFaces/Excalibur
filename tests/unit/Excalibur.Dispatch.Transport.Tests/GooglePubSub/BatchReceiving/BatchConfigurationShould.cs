// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class BatchConfigurationShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var config = new BatchConfiguration();

		// Assert
		config.MaxMessagesPerBatch.ShouldBe(1000);
		config.MinMessagesPerBatch.ShouldBe(1);
		config.MaxBatchWaitTime.ShouldBe(TimeSpan.FromMilliseconds(100));
		config.MaxBatchSizeBytes.ShouldBe(10 * 1024 * 1024);
		config.EnableAdaptiveBatching.ShouldBeTrue();
		config.TargetBatchProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		config.ConcurrentBatchProcessors.ShouldBe(Environment.ProcessorCount);
		config.PreserveMessageOrder.ShouldBeFalse();
		config.AckDeadlineSeconds.ShouldBe(600);
		config.AckStrategy.ShouldBe(BatchAckStrategy.OnSuccess);
		config.EnableMetricsCompression.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var config = new BatchConfiguration
		{
			MaxMessagesPerBatch = 500,
			MinMessagesPerBatch = 10,
			MaxBatchWaitTime = TimeSpan.FromMilliseconds(200),
			MaxBatchSizeBytes = 5 * 1024 * 1024,
			EnableAdaptiveBatching = false,
			TargetBatchProcessingTime = TimeSpan.FromMilliseconds(100),
			ConcurrentBatchProcessors = 4,
			PreserveMessageOrder = true,
			AckDeadlineSeconds = 300,
			AckStrategy = BatchAckStrategy.BatchComplete,
			EnableMetricsCompression = false,
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
		config.AckDeadlineSeconds.ShouldBe(300);
		config.AckStrategy.ShouldBe(BatchAckStrategy.BatchComplete);
		config.EnableMetricsCompression.ShouldBeFalse();
	}

	[Fact]
	public void ValidateThrowWhenMaxMessagesOutOfRange()
	{
		// Arrange
		var config = new BatchConfiguration { MaxMessagesPerBatch = 0 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("MaxMessagesPerBatch");
	}

	[Fact]
	public void ValidateThrowWhenMaxMessagesExceedLimit()
	{
		// Arrange
		var config = new BatchConfiguration { MaxMessagesPerBatch = 1001 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("MaxMessagesPerBatch");
	}

	[Fact]
	public void ValidateThrowWhenMinMessagesExceedsMax()
	{
		// Arrange
		var config = new BatchConfiguration
		{
			MaxMessagesPerBatch = 10,
			MinMessagesPerBatch = 20,
		};

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("MinMessagesPerBatch");
	}

	[Fact]
	public void ValidateThrowWhenBatchWaitTimeZero()
	{
		// Arrange
		var config = new BatchConfiguration { MaxBatchWaitTime = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("MaxBatchWaitTime");
	}

	[Fact]
	public void ValidateThrowWhenBatchSizeBytesZero()
	{
		// Arrange
		var config = new BatchConfiguration { MaxBatchSizeBytes = 0 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("MaxBatchSizeBytes");
	}

	[Fact]
	public void ValidateThrowWhenTargetProcessingTimeZero()
	{
		// Arrange
		var config = new BatchConfiguration { TargetBatchProcessingTime = TimeSpan.Zero };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("TargetBatchProcessingTime");
	}

	[Fact]
	public void ValidateThrowWhenConcurrentProcessorsZero()
	{
		// Arrange
		var config = new BatchConfiguration { ConcurrentBatchProcessors = 0 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("ConcurrentBatchProcessors");
	}

	[Fact]
	public void ValidateThrowWhenAckDeadlineTooLow()
	{
		// Arrange
		var config = new BatchConfiguration { AckDeadlineSeconds = 5 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("AckDeadlineSeconds");
	}

	[Fact]
	public void ValidateThrowWhenAckDeadlineTooHigh()
	{
		// Arrange
		var config = new BatchConfiguration { AckDeadlineSeconds = 601 };

		// Act & Assert
		Should.Throw<InvalidOperationException>(() => config.Validate())
			.Message.ShouldContain("AckDeadlineSeconds");
	}

	[Fact]
	public void ValidateSucceedWithValidConfig()
	{
		// Arrange
		var config = new BatchConfiguration();

		// Act & Assert — should not throw
		config.Validate();
	}

	[Fact]
	public void CloneProduceIdenticalCopy()
	{
		// Arrange
		var original = new BatchConfiguration
		{
			MaxMessagesPerBatch = 200,
			MinMessagesPerBatch = 5,
			MaxBatchWaitTime = TimeSpan.FromMilliseconds(500),
			AckStrategy = BatchAckStrategy.Manual,
			PreserveMessageOrder = true,
		};

		// Act
		var clone = original.Clone();

		// Assert
		clone.MaxMessagesPerBatch.ShouldBe(200);
		clone.MinMessagesPerBatch.ShouldBe(5);
		clone.MaxBatchWaitTime.ShouldBe(TimeSpan.FromMilliseconds(500));
		clone.AckStrategy.ShouldBe(BatchAckStrategy.Manual);
		clone.PreserveMessageOrder.ShouldBeTrue();
	}

	[Fact]
	public void CloneNotReturnSameReference()
	{
		// Arrange
		var original = new BatchConfiguration();

		// Act
		var clone = original.Clone();

		// Assert
		clone.ShouldNotBeSameAs(original);
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
}
