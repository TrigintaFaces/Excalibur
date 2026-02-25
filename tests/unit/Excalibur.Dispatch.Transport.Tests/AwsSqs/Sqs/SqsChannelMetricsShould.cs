// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Sqs;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class SqsChannelMetricsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var metrics = new SqsChannelMetrics();

		// Assert
		metrics.TotalMessagesProcessed.ShouldBe(0);
		metrics.SuccessfulMessages.ShouldBe(0);
		metrics.FailedMessages.ShouldBe(0);
		metrics.AverageProcessingTimeMs.ShouldBe(0.0);
		metrics.CurrentThroughput.ShouldBe(0.0);
		metrics.PeakThroughput.ShouldBe(0.0);
		metrics.CurrentBatchSize.ShouldBe(0);
		metrics.ActiveWorkers.ShouldBe(0);
		metrics.QueueDepth.ShouldBe(0);
		metrics.LastUpdated.ShouldNotBe(default);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var metrics = new SqsChannelMetrics
		{
			TotalMessagesProcessed = 50000,
			SuccessfulMessages = 49500,
			FailedMessages = 500,
			AverageProcessingTimeMs = 12.5,
			CurrentThroughput = 1000.0,
			PeakThroughput = 1500.0,
			CurrentBatchSize = 10,
			ActiveWorkers = 5,
			QueueDepth = 100,
			LastUpdated = now,
		};

		// Assert
		metrics.TotalMessagesProcessed.ShouldBe(50000);
		metrics.SuccessfulMessages.ShouldBe(49500);
		metrics.FailedMessages.ShouldBe(500);
		metrics.AverageProcessingTimeMs.ShouldBe(12.5);
		metrics.CurrentThroughput.ShouldBe(1000.0);
		metrics.PeakThroughput.ShouldBe(1500.0);
		metrics.CurrentBatchSize.ShouldBe(10);
		metrics.ActiveWorkers.ShouldBe(5);
		metrics.QueueDepth.ShouldBe(100);
		metrics.LastUpdated.ShouldBe(now);
	}
}
