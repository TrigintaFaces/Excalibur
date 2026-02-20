// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Ordering;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class OrderingStatisticsShould
{
	[Fact]
	public void CreateOrderingKeyStatisticsWithDefaults()
	{
		// Act
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
		stats.QueueStatistics.ShouldNotBeNull();
		stats.QueueStatistics.Count.ShouldBe(0);
	}

	[Fact]
	public void SetOrderingKeyStatisticsProperties()
	{
		// Arrange
		var queueStats = new List<QueueStatistics>
		{
			new()
			{
				OrderingKey = "key-1",
				QueueDepth = 5,
				IsProcessing = true,
				ProcessedCount = 100,
				ErrorCount = 2,
			},
			new()
			{
				OrderingKey = "key-2",
				QueueDepth = 3,
				IsProcessing = false,
				ProcessedCount = 50,
				ErrorCount = 0,
			},
		};

		// Act
		var stats = new OrderingKeyStatistics
		{
			TotalOrderingKeys = 10,
			ActiveOrderingKeys = 7,
			FailedOrderingKeys = 3,
			TotalMessagesProcessed = 1000,
			TotalOutOfSequenceMessages = 15,
			TotalProcessed = 985,
			TotalErrors = 5,
			AverageProcessingTime = 12.5,
			AverageQueueDepth = 4.0,
			QueueStatistics = queueStats,
		};

		// Assert
		stats.TotalOrderingKeys.ShouldBe(10);
		stats.ActiveOrderingKeys.ShouldBe(7);
		stats.FailedOrderingKeys.ShouldBe(3);
		stats.TotalMessagesProcessed.ShouldBe(1000);
		stats.TotalOutOfSequenceMessages.ShouldBe(15);
		stats.TotalProcessed.ShouldBe(985);
		stats.TotalErrors.ShouldBe(5);
		stats.AverageProcessingTime.ShouldBe(12.5);
		stats.AverageQueueDepth.ShouldBe(4.0);
		stats.QueueStatistics.Count.ShouldBe(2);
	}

	[Fact]
	public void CreateQueueStatisticsWithDefaults()
	{
		// Act
		var stats = new QueueStatistics();

		// Assert
		stats.OrderingKey.ShouldBe(string.Empty);
		stats.QueueDepth.ShouldBe(0);
		stats.IsProcessing.ShouldBeFalse();
		stats.ProcessedCount.ShouldBe(0);
		stats.ErrorCount.ShouldBe(0);
	}

	[Fact]
	public void SetQueueStatisticsProperties()
	{
		// Act
		var stats = new QueueStatistics
		{
			OrderingKey = "partition-key-abc",
			QueueDepth = 15,
			IsProcessing = true,
			ProcessedCount = 5000,
			ErrorCount = 42,
		};

		// Assert
		stats.OrderingKey.ShouldBe("partition-key-abc");
		stats.QueueDepth.ShouldBe(15);
		stats.IsProcessing.ShouldBeTrue();
		stats.ProcessedCount.ShouldBe(5000);
		stats.ErrorCount.ShouldBe(42);
	}

	[Fact]
	public void CreateOrderingPerformanceStatisticsWithDefaults()
	{
		// Act
		var stats = new OrderingPerformanceStatistics();

		// Assert
		stats.TotalInSequence.ShouldBe(0);
		stats.TotalOutOfSequence.ShouldBe(0);
		stats.SequenceRatio.ShouldBe(0);
		stats.AverageGapSize.ShouldBe(0);
		stats.P95GapSize.ShouldBe(0);
		stats.MaxGapSize.ShouldBe(0);
	}

	[Fact]
	public void SetOrderingPerformanceStatisticsProperties()
	{
		// Act
		var stats = new OrderingPerformanceStatistics
		{
			TotalInSequence = 950,
			TotalOutOfSequence = 50,
			SequenceRatio = 95.0,
			AverageGapSize = 2.5,
			P95GapSize = 10.0,
			MaxGapSize = 25.0,
		};

		// Assert
		stats.TotalInSequence.ShouldBe(950);
		stats.TotalOutOfSequence.ShouldBe(50);
		stats.SequenceRatio.ShouldBe(95.0);
		stats.AverageGapSize.ShouldBe(2.5);
		stats.P95GapSize.ShouldBe(10.0);
		stats.MaxGapSize.ShouldBe(25.0);
	}
}
