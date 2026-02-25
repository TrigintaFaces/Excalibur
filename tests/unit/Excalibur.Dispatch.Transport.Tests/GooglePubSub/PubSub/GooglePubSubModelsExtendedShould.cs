// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.PubSub;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class GooglePubSubModelsExtendedShould
{
	[Fact]
	public void CreateOrderedProcessingResultWithDefaults()
	{
		// Arrange & Act
		var result = new OrderedProcessingResult();

		// Assert
		result.Success.ShouldBeFalse();
		result.WorkerId.ShouldBe(0);
		result.ProcessingTime.ShouldBe(TimeSpan.Zero);
		result.WasOrdered.ShouldBeFalse();
		result.QueueTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateOrderedProcessingResultWithValues()
	{
		// Arrange & Act
		var result = new OrderedProcessingResult
		{
			Success = true,
			WorkerId = 5,
			ProcessingTime = TimeSpan.FromMilliseconds(120),
			WasOrdered = true,
			QueueTime = TimeSpan.FromMilliseconds(30),
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.WorkerId.ShouldBe(5);
		result.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(120));
		result.WasOrdered.ShouldBeTrue();
		result.QueueTime.ShouldBe(TimeSpan.FromMilliseconds(30));
	}

	[Fact]
	public void CreateOrderingKeyStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new OrderingKeyStatistics();

		// Assert
		stats.TotalOrderingKeys.ShouldBe(0);
		stats.ActiveOrderingKeys.ShouldBe(0);
		stats.FailedOrderingKeys.ShouldBe(0);
		stats.TotalMessagesProcessed.ShouldBe(0);
		stats.TotalOutOfSequenceMessages.ShouldBe(0);
		stats.TotalProcessed.ShouldBe(0);
		stats.TotalErrors.ShouldBe(0);
		stats.AverageProcessingTime.ShouldBe(0);
		stats.AverageQueueDepth.ShouldBe(0);
		stats.QueueStatistics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateOrderingKeyStatisticsWithValues()
	{
		// Arrange
		var queueStats = new List<QueueStatistics>
		{
			new() { OrderingKey = "key-1", QueueDepth = 5, IsProcessing = true, ProcessedCount = 100, ErrorCount = 2 },
		};

		// Act
		var stats = new OrderingKeyStatistics
		{
			TotalOrderingKeys = 10,
			ActiveOrderingKeys = 7,
			FailedOrderingKeys = 1,
			TotalMessagesProcessed = 5000,
			TotalOutOfSequenceMessages = 3,
			TotalProcessed = 4997,
			TotalErrors = 3,
			AverageProcessingTime = 15.5,
			AverageQueueDepth = 2.3,
			QueueStatistics = queueStats,
		};

		// Assert
		stats.TotalOrderingKeys.ShouldBe(10);
		stats.ActiveOrderingKeys.ShouldBe(7);
		stats.FailedOrderingKeys.ShouldBe(1);
		stats.TotalMessagesProcessed.ShouldBe(5000);
		stats.TotalOutOfSequenceMessages.ShouldBe(3);
		stats.TotalProcessed.ShouldBe(4997);
		stats.TotalErrors.ShouldBe(3);
		stats.AverageProcessingTime.ShouldBe(15.5);
		stats.AverageQueueDepth.ShouldBe(2.3);
		stats.QueueStatistics.Count.ShouldBe(1);
	}

	[Fact]
	public void CreateQueueStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new QueueStatistics();

		// Assert
		stats.OrderingKey.ShouldBe(string.Empty);
		stats.QueueDepth.ShouldBe(0);
		stats.IsProcessing.ShouldBeFalse();
		stats.ProcessedCount.ShouldBe(0);
		stats.ErrorCount.ShouldBe(0);
	}

	[Fact]
	public void CreateQueueStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new QueueStatistics
		{
			OrderingKey = "order-key-42",
			QueueDepth = 15,
			IsProcessing = true,
			ProcessedCount = 500,
			ErrorCount = 3,
		};

		// Assert
		stats.OrderingKey.ShouldBe("order-key-42");
		stats.QueueDepth.ShouldBe(15);
		stats.IsProcessing.ShouldBeTrue();
		stats.ProcessedCount.ShouldBe(500);
		stats.ErrorCount.ShouldBe(3);
	}

	[Fact]
	public void CreateDeadLetterMetadataRecord()
	{
		// Arrange
		var firstFail = DateTimeOffset.UtcNow.AddMinutes(-10);
		var lastFail = DateTimeOffset.UtcNow;

		// Act
		var metadata = new DeadLetterMetadata(3, "Timeout", firstFail, lastFail, "projects/p/topics/t");

		// Assert
		metadata.DeliveryAttempts.ShouldBe(3);
		metadata.LastErrorReason.ShouldBe("Timeout");
		metadata.FirstFailureTime.ShouldBe(firstFail);
		metadata.LastFailureTime.ShouldBe(lastFail);
		metadata.OriginalTopic.ShouldBe("projects/p/topics/t");
	}

	[Fact]
	public void CreateDeadLetterMetadataWithoutOriginalTopic()
	{
		// Arrange & Act
		var metadata = new DeadLetterMetadata(1, "Error", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);

		// Assert
		metadata.OriginalTopic.ShouldBeNull();
	}

	[Fact]
	public void SupportDeadLetterMetadataRecordEquality()
	{
		// Arrange
		var time = DateTimeOffset.UtcNow;

		// Act
		var m1 = new DeadLetterMetadata(1, "err", time, time, "topic");
		var m2 = new DeadLetterMetadata(1, "err", time, time, "topic");

		// Assert
		m1.ShouldBe(m2);
	}

	[Fact]
	public void CreateFlowControlStateRecord()
	{
		// Arrange & Act
		var state = new FlowControlState(100, 1048576L, 50, 524288L, true);

		// Assert
		state.MaxOutstandingMessages.ShouldBe(100);
		state.MaxOutstandingBytes.ShouldBe(1048576L);
		state.CurrentOutstandingMessages.ShouldBe(50);
		state.CurrentOutstandingBytes.ShouldBe(524288L);
		state.IsFlowControlActive.ShouldBeTrue();
	}

	[Fact]
	public void SupportFlowControlStateRecordEquality()
	{
		// Arrange & Act
		var s1 = new FlowControlState(10, 100L, 5, 50L, false);
		var s2 = new FlowControlState(10, 100L, 5, 50L, false);

		// Assert
		s1.ShouldBe(s2);
	}
}
