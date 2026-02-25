// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Google;

namespace Excalibur.Dispatch.Transport.Tests.GooglePubSub.Performance;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class PerformanceModelsShould
{
	[Fact]
	public void CreateHistogramSnapshotWithDefaults()
	{
		// Arrange & Act
		var snapshot = new HistogramSnapshot();

		// Assert
		snapshot.Count.ShouldBe(0);
		snapshot.Sum.ShouldBe(0);
		snapshot.Min.ShouldBe(0);
		snapshot.Max.ShouldBe(0);
		snapshot.Mean.ShouldBe(0);
		snapshot.P95.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingHistogramSnapshotProperties()
	{
		// Arrange & Act
		var snapshot = new HistogramSnapshot
		{
			Count = 1000,
			Sum = 50000,
			Min = 1,
			Max = 500,
			Mean = 50,
			P95 = 200,
		};

		// Assert
		snapshot.Count.ShouldBe(1000);
		snapshot.Sum.ShouldBe(50000);
		snapshot.Min.ShouldBe(1);
		snapshot.Max.ShouldBe(500);
		snapshot.Mean.ShouldBe(50);
		snapshot.P95.ShouldBe(200);
	}

	[Fact]
	public void CreatePerformanceStatisticsWithDefaults()
	{
		// Arrange & Act
		var stats = new PerformanceStatistics();

		// Assert
		stats.MessagesEnqueued.ShouldBe(0);
		stats.MessagesProcessed.ShouldBe(0);
		stats.MessagesFailed.ShouldBe(0);
		stats.AverageQueueTime.ShouldBe(TimeSpan.Zero);
		stats.P95QueueTime.ShouldBe(TimeSpan.Zero);
		stats.AverageProcessingTime.ShouldBe(TimeSpan.Zero);
		stats.P95ProcessingTime.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void CreatePerformanceStatisticsWithValues()
	{
		// Arrange & Act
		var stats = new PerformanceStatistics
		{
			MessagesEnqueued = 10000,
			MessagesProcessed = 9900,
			MessagesFailed = 100,
			AverageQueueTime = TimeSpan.FromMilliseconds(5),
			P95QueueTime = TimeSpan.FromMilliseconds(20),
			AverageProcessingTime = TimeSpan.FromMilliseconds(50),
			P95ProcessingTime = TimeSpan.FromMilliseconds(200),
		};

		// Assert
		stats.MessagesEnqueued.ShouldBe(10000);
		stats.MessagesProcessed.ShouldBe(9900);
		stats.MessagesFailed.ShouldBe(100);
		stats.AverageQueueTime.ShouldBe(TimeSpan.FromMilliseconds(5));
		stats.P95QueueTime.ShouldBe(TimeSpan.FromMilliseconds(20));
		stats.AverageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(50));
		stats.P95ProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(200));
	}
}
