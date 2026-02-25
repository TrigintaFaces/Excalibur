// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

using PubSubBatchMetadata = Excalibur.Dispatch.Transport.Google.BatchMetadata;
using PubSubProcessedMessage = Excalibur.Dispatch.Transport.Google.ProcessedMessage;
using PubSubFailedMessage = Excalibur.Dispatch.Transport.Google.FailedMessage;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.BatchReceiving;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class BatchModelsShould
{
	[Fact]
	public void CreateBatchMetadataWithDefaults()
	{
		// Arrange & Act
		var metadata = new PubSubBatchMetadata();

		// Assert
		metadata.PullDurationMs.ShouldBe(0.0);
		metadata.FlowControlApplied.ShouldBeFalse();
		metadata.EffectiveBatchSize.ShouldBe(0);
		metadata.Properties.ShouldNotBeNull();
		metadata.Properties.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingBatchMetadataProperties()
	{
		// Arrange & Act
		var metadata = new PubSubBatchMetadata
		{
			PullDurationMs = 125.5,
			FlowControlApplied = true,
			EffectiveBatchSize = 50,
		};
		metadata.Properties["key"] = "value";

		// Assert
		metadata.PullDurationMs.ShouldBe(125.5);
		metadata.FlowControlApplied.ShouldBeTrue();
		metadata.EffectiveBatchSize.ShouldBe(50);
		metadata.Properties["key"].ShouldBe("value");
	}

	[Fact]
	public void CreateProcessedMessageWithAllProperties()
	{
		// Arrange & Act
		var result = new object();
		var message = new PubSubProcessedMessage("msg-1", "ack-1", result, TimeSpan.FromMilliseconds(50));

		// Assert
		message.MessageId.ShouldBe("msg-1");
		message.AckId.ShouldBe("ack-1");
		message.Result.ShouldBeSameAs(result);
		message.Duration.ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void ThrowWhenProcessedMessageIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubProcessedMessage(null!, "ack-1", new object(), TimeSpan.Zero));
	}

	[Fact]
	public void ThrowWhenProcessedMessageAckIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubProcessedMessage("msg-1", null!, new object(), TimeSpan.Zero));
	}

	[Fact]
	public void CreateFailedMessageWithAllProperties()
	{
		// Arrange
		var error = new InvalidOperationException("Test error");

		// Act
		var message = new PubSubFailedMessage("msg-1", "ack-1", error, shouldRetry: false, retryDelay: TimeSpan.FromSeconds(5));

		// Assert
		message.MessageId.ShouldBe("msg-1");
		message.AckId.ShouldBe("ack-1");
		message.Error.ShouldBeSameAs(error);
		message.ShouldRetry.ShouldBeFalse();
		message.RetryDelay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void CreateFailedMessageWithDefaults()
	{
		// Arrange
		var error = new InvalidOperationException("Test");

		// Act
		var message = new PubSubFailedMessage("msg-1", "ack-1", error);

		// Assert
		message.ShouldRetry.ShouldBeTrue();
		message.RetryDelay.ShouldBeNull();
	}

	[Fact]
	public void ThrowWhenFailedMessageIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubFailedMessage(null!, "ack-1", new InvalidOperationException()));
	}

	[Fact]
	public void ThrowWhenFailedMessageAckIdIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubFailedMessage("msg-1", null!, new InvalidOperationException()));
	}

	[Fact]
	public void ThrowWhenFailedMessageErrorIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new PubSubFailedMessage("msg-1", "ack-1", null!));
	}

	[Fact]
	public void CreateBatchResultWithDefaults()
	{
		// Arrange & Act
		var result = new BatchResult();

		// Assert
		result.BatchSize.ShouldBe(0);
		result.ProcessingDuration.ShouldBe(TimeSpan.Zero);
		result.SuccessCount.ShouldBe(0);
		result.FailureCount.ShouldBe(0);
		result.TotalBytes.ShouldBe(0);
		result.WasFlowControlled.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingBatchResultProperties()
	{
		// Arrange & Act
		var result = new BatchResult
		{
			BatchSize = 100,
			ProcessingDuration = TimeSpan.FromMilliseconds(500),
			SuccessCount = 95,
			FailureCount = 5,
			TotalBytes = 1024000,
			WasFlowControlled = true,
		};

		// Assert
		result.BatchSize.ShouldBe(100);
		result.ProcessingDuration.ShouldBe(TimeSpan.FromMilliseconds(500));
		result.SuccessCount.ShouldBe(95);
		result.FailureCount.ShouldBe(5);
		result.TotalBytes.ShouldBe(1024000);
		result.WasFlowControlled.ShouldBeTrue();
	}

	[Fact]
	public void CreateBatchingContextWithDefaults()
	{
		// Arrange & Act
		var context = new BatchingContext();

		// Assert
		context.QueueDepth.ShouldBe(0);
		context.ProcessingRate.ShouldBe(0.0);
		context.MemoryPressure.ShouldBe(0.0);
		context.AverageMessageSize.ShouldBe(0.0);
		context.FlowControlQuota.ShouldBe(0);
		context.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
	}

	[Fact]
	public void AllowSettingBatchingContextProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var context = new BatchingContext
		{
			QueueDepth = 500,
			ProcessingRate = 1000.5,
			MemoryPressure = 0.75,
			AverageMessageSize = 2048.0,
			FlowControlQuota = 200,
			Timestamp = now,
		};

		// Assert
		context.QueueDepth.ShouldBe(500);
		context.ProcessingRate.ShouldBe(1000.5);
		context.MemoryPressure.ShouldBe(0.75);
		context.AverageMessageSize.ShouldBe(2048.0);
		context.FlowControlQuota.ShouldBe(200);
		context.Timestamp.ShouldBe(now);
	}

	[Fact]
	public void CreateAdaptiveStrategyStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new AdaptiveStrategyStatistics();

		// Assert
		stats.CurrentBatchSize.ShouldBe(0);
		stats.AverageProcessingTime.ShouldBe(0.0);
		stats.AverageThroughput.ShouldBe(0.0);
		stats.Aggressiveness.ShouldBe(0.0);
		stats.StableIterations.ShouldBe(0);
		stats.RecentResults.ShouldNotBeNull();
		stats.RecentResults.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAdaptiveStrategyStatisticsProperties()
	{
		// Arrange & Act
		var stats = new AdaptiveStrategyStatistics
		{
			CurrentBatchSize = 200,
			AverageProcessingTime = 50.5,
			AverageThroughput = 3000.0,
			Aggressiveness = 0.8,
			StableIterations = 10,
			RecentResults = [new BatchResult { BatchSize = 100 }],
		};

		// Assert
		stats.CurrentBatchSize.ShouldBe(200);
		stats.AverageProcessingTime.ShouldBe(50.5);
		stats.AverageThroughput.ShouldBe(3000.0);
		stats.Aggressiveness.ShouldBe(0.8);
		stats.StableIterations.ShouldBe(10);
		stats.RecentResults.Count.ShouldBe(1);
		stats.RecentResults[0].BatchSize.ShouldBe(100);
	}
}
