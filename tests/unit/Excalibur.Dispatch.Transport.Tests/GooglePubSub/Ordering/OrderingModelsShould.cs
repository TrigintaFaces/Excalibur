// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Ordering;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class OrderingModelsShould
{
	[Fact]
	public void CreateOrderingKeyInfoWithAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var info = new OrderingKeyInfo
		{
			OrderingKey = "order-123",
			MessageCount = 1000,
			LastSequence = 999,
			ExpectedSequence = 1000,
			IsFailed = false,
			FailureReason = null,
			LastActivity = now,
			OutOfSequenceCount = 5,
		};

		// Assert
		info.OrderingKey.ShouldBe("order-123");
		info.MessageCount.ShouldBe(1000);
		info.LastSequence.ShouldBe(999);
		info.ExpectedSequence.ShouldBe(1000);
		info.IsFailed.ShouldBeFalse();
		info.FailureReason.ShouldBeNull();
		info.LastActivity.ShouldBe(now);
		info.OutOfSequenceCount.ShouldBe(5);
	}

	[Fact]
	public void CreateOrderingKeyInfoInFailedState()
	{
		// Arrange & Act
		var info = new OrderingKeyInfo
		{
			OrderingKey = "order-456",
			IsFailed = true,
			FailureReason = "Persistent serialization failure",
		};

		// Assert
		info.IsFailed.ShouldBeTrue();
		info.FailureReason.ShouldBe("Persistent serialization failure");
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
		stats.AverageProcessingTime.ShouldBe(0.0);
		stats.AverageQueueDepth.ShouldBe(0.0);
		stats.QueueStatistics.ShouldNotBeNull();
		stats.QueueStatistics.ShouldBeEmpty();
	}

	[Fact]
	public void CreateOrderingKeyStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new OrderingKeyStatistics
		{
			TotalOrderingKeys = 50,
			ActiveOrderingKeys = 45,
			FailedOrderingKeys = 5,
			TotalMessagesProcessed = 100000,
			TotalOutOfSequenceMessages = 150,
			TotalProcessed = 99850,
			TotalErrors = 150,
			AverageProcessingTime = 12.5,
			AverageQueueDepth = 3.2,
		};

		// Assert
		stats.TotalOrderingKeys.ShouldBe(50);
		stats.ActiveOrderingKeys.ShouldBe(45);
		stats.FailedOrderingKeys.ShouldBe(5);
		stats.TotalMessagesProcessed.ShouldBe(100000);
		stats.TotalOutOfSequenceMessages.ShouldBe(150);
		stats.TotalProcessed.ShouldBe(99850);
		stats.TotalErrors.ShouldBe(150);
		stats.AverageProcessingTime.ShouldBe(12.5);
		stats.AverageQueueDepth.ShouldBe(3.2);
	}

	[Fact]
	public void CreateOrderingPerformanceStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new OrderingPerformanceStatistics();

		// Assert
		stats.TotalInSequence.ShouldBe(0);
		stats.TotalOutOfSequence.ShouldBe(0);
		stats.SequenceRatio.ShouldBe(0.0);
		stats.AverageGapSize.ShouldBe(0.0);
		stats.P95GapSize.ShouldBe(0.0);
		stats.MaxGapSize.ShouldBe(0.0);
	}

	[Fact]
	public void CreateOrderingPerformanceStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new OrderingPerformanceStatistics
		{
			TotalInSequence = 9500,
			TotalOutOfSequence = 500,
			SequenceRatio = 95.0,
			AverageGapSize = 2.3,
			P95GapSize = 10.0,
			MaxGapSize = 50.0,
		};

		// Assert
		stats.TotalInSequence.ShouldBe(9500);
		stats.TotalOutOfSequence.ShouldBe(500);
		stats.SequenceRatio.ShouldBe(95.0);
		stats.AverageGapSize.ShouldBe(2.3);
		stats.P95GapSize.ShouldBe(10.0);
		stats.MaxGapSize.ShouldBe(50.0);
	}

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
			WorkerId = 3,
			ProcessingTime = TimeSpan.FromMilliseconds(50),
			WasOrdered = true,
			QueueTime = TimeSpan.FromMilliseconds(10),
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.WorkerId.ShouldBe(3);
		result.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		result.WasOrdered.ShouldBeTrue();
		result.QueueTime.ShouldBe(TimeSpan.FromMilliseconds(10));
	}
}
