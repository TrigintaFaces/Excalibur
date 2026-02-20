// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Parallel;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ParallelModelsShould
{
	[Fact]
	public void CreateProcessingResultWithDefaults()
	{
		// Arrange & Act
		var result = new ProcessingResult();

		// Assert
		result.Success.ShouldBeFalse();
		result.WorkerId.ShouldBe(0);
		result.ProcessingTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreateProcessingResultWithValues()
	{
		// Arrange & Act
		var result = new ProcessingResult
		{
			Success = true,
			WorkerId = 5,
			ProcessingTime = TimeSpan.FromMilliseconds(100),
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.WorkerId.ShouldBe(5);
		result.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void CreateWorkDistributionContextWithDefaults()
	{
		// Arrange & Act
		var context = new WorkDistributionContext();

		// Assert
		context.OrderingKey.ShouldBeNull();
		context.MessageSize.ShouldBe(0);
		context.TotalWorkers.ShouldBe(0);
		context.PendingWorkCounts.ShouldNotBeNull();
		context.PendingWorkCounts.ShouldBeEmpty();
	}

	[Fact]
	public void CreateWorkDistributionContextWithValues()
	{
		// Arrange & Act
		var context = new WorkDistributionContext
		{
			OrderingKey = "order-123",
			MessageSize = 4096,
			TotalWorkers = 4,
			PendingWorkCounts = [10, 5, 8, 3],
		};

		// Assert
		context.OrderingKey.ShouldBe("order-123");
		context.MessageSize.ShouldBe(4096);
		context.TotalWorkers.ShouldBe(4);
		context.PendingWorkCounts.Length.ShouldBe(4);
		context.PendingWorkCounts[3].ShouldBe(3);
	}

	[Fact]
	public void CreateWorkerStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new WorkerStatistics();

		// Assert
		stats.TotalWorkers.ShouldBe(0);
		stats.ActiveWorkers.ShouldBe(0);
		stats.PendingWork.ShouldBe(0);
		stats.ProcessedCount.ShouldBe(0);
		stats.ErrorCount.ShouldBe(0);
		stats.AverageProcessingTime.ShouldBe(0.0);
	}

	[Fact]
	public void CreateWorkerStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new WorkerStatistics
		{
			TotalWorkers = 8,
			ActiveWorkers = 6,
			PendingWork = 25,
			ProcessedCount = 50000,
			ErrorCount = 100,
			AverageProcessingTime = 15.5,
		};

		// Assert
		stats.TotalWorkers.ShouldBe(8);
		stats.ActiveWorkers.ShouldBe(6);
		stats.PendingWork.ShouldBe(25);
		stats.ProcessedCount.ShouldBe(50000);
		stats.ErrorCount.ShouldBe(100);
		stats.AverageProcessingTime.ShouldBe(15.5);
	}

	[Fact]
	public void CreateUtilizationReportWithDefaults()
	{
		// Arrange & Act
		var report = new UtilizationReport();

		// Assert
		report.TotalThreads.ShouldBe(0);
		report.ActiveThreads.ShouldBe(0);
		report.MaxObservedThreads.ShouldBe(0);
		report.AverageUtilization.ShouldBe(0.0);
		report.AverageProcessingTime.ShouldBe(TimeSpan.Zero);
		report.ContextSwitchCount.ShouldBe(0);
		report.TotalTasksProcessed.ShouldBe(0);
	}

	[Fact]
	public void CreateUtilizationReportWithValues()
	{
		// Arrange & Act
		var report = new UtilizationReport
		{
			TotalThreads = 16,
			ActiveThreads = 12,
			MaxObservedThreads = 14,
			AverageUtilization = 75.5,
			AverageProcessingTime = TimeSpan.FromMilliseconds(25),
			ContextSwitchCount = 5000,
			TotalTasksProcessed = 100000,
		};

		// Assert
		report.TotalThreads.ShouldBe(16);
		report.ActiveThreads.ShouldBe(12);
		report.MaxObservedThreads.ShouldBe(14);
		report.AverageUtilization.ShouldBe(75.5);
		report.AverageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(25));
		report.ContextSwitchCount.ShouldBe(5000);
		report.TotalTasksProcessed.ShouldBe(100000);
	}
}
