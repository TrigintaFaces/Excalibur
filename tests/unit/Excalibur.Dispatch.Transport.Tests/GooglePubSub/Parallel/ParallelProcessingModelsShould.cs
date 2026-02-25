// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Parallel;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ParallelProcessingModelsShould
{
	[Fact]
	public void CreateProcessingResultWithDefaults()
	{
		// Act
		var result = new ProcessingResult();

		// Assert
		result.Success.ShouldBeFalse();
		result.WorkerId.ShouldBe(0);
		result.ProcessingTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void SetProcessingResultProperties()
	{
		// Act
		var result = new ProcessingResult
		{
			Success = true,
			WorkerId = 3,
			ProcessingTime = TimeSpan.FromMilliseconds(150),
		};

		// Assert
		result.Success.ShouldBeTrue();
		result.WorkerId.ShouldBe(3);
		result.ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(150));
	}

	[Fact]
	public void CreateUtilizationReportWithDefaults()
	{
		// Act
		var report = new UtilizationReport();

		// Assert
		report.TotalThreads.ShouldBe(0);
		report.ActiveThreads.ShouldBe(0);
		report.MaxObservedThreads.ShouldBe(0);
		report.AverageUtilization.ShouldBe(0);
		report.AverageProcessingTime.ShouldBe(TimeSpan.Zero);
		report.ContextSwitchCount.ShouldBe(0);
		report.TotalTasksProcessed.ShouldBe(0);
	}

	[Fact]
	public void SetUtilizationReportProperties()
	{
		// Act
		var report = new UtilizationReport
		{
			TotalThreads = 8,
			ActiveThreads = 5,
			MaxObservedThreads = 7,
			AverageUtilization = 72.5,
			AverageProcessingTime = TimeSpan.FromMilliseconds(25),
			ContextSwitchCount = 1500,
			TotalTasksProcessed = 10000,
		};

		// Assert
		report.TotalThreads.ShouldBe(8);
		report.ActiveThreads.ShouldBe(5);
		report.MaxObservedThreads.ShouldBe(7);
		report.AverageUtilization.ShouldBe(72.5);
		report.AverageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(25));
		report.ContextSwitchCount.ShouldBe(1500);
		report.TotalTasksProcessed.ShouldBe(10000);
	}

	[Fact]
	public void CreateWorkerStatisticsWithDefaults()
	{
		// Act
		var stats = new WorkerStatistics();

		// Assert
		stats.TotalWorkers.ShouldBe(0);
		stats.ActiveWorkers.ShouldBe(0);
		stats.PendingWork.ShouldBe(0);
		stats.ProcessedCount.ShouldBe(0);
		stats.ErrorCount.ShouldBe(0);
		stats.AverageProcessingTime.ShouldBe(0);
	}

	[Fact]
	public void SetWorkerStatisticsProperties()
	{
		// Act
		var stats = new WorkerStatistics
		{
			TotalWorkers = 4,
			ActiveWorkers = 3,
			PendingWork = 12,
			ProcessedCount = 5000,
			ErrorCount = 25,
			AverageProcessingTime = 8.75,
		};

		// Assert
		stats.TotalWorkers.ShouldBe(4);
		stats.ActiveWorkers.ShouldBe(3);
		stats.PendingWork.ShouldBe(12);
		stats.ProcessedCount.ShouldBe(5000);
		stats.ErrorCount.ShouldBe(25);
		stats.AverageProcessingTime.ShouldBe(8.75);
	}

	[Fact]
	public void CreateWorkDistributionContextWithDefaults()
	{
		// Act
		var context = new WorkDistributionContext();

		// Assert
		context.OrderingKey.ShouldBeNull();
		context.MessageSize.ShouldBe(0);
		context.TotalWorkers.ShouldBe(0);
		context.PendingWorkCounts.ShouldNotBeNull();
		context.PendingWorkCounts.Length.ShouldBe(0);
	}

	[Fact]
	public void SetWorkDistributionContextProperties()
	{
		// Act
		var context = new WorkDistributionContext
		{
			OrderingKey = "partition-key",
			MessageSize = 4096,
			TotalWorkers = 4,
			PendingWorkCounts = [3, 5, 1, 7],
		};

		// Assert
		context.OrderingKey.ShouldBe("partition-key");
		context.MessageSize.ShouldBe(4096);
		context.TotalWorkers.ShouldBe(4);
		context.PendingWorkCounts.Length.ShouldBe(4);
		context.PendingWorkCounts[0].ShouldBe(3);
		context.PendingWorkCounts[3].ShouldBe(7);
	}
}
