// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Parallel;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Transport)]
public sealed class ParallelModelsShould
{
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
