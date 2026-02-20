// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Channels;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SqsChannelMetricsSummaryShould
{
	[Fact]
	public void HaveEmptyQueueMetricsByDefault()
	{
		// Arrange & Act
		var summary = new SqsChannelMetricsSummary();

		// Assert
		summary.QueueMetrics.ShouldNotBeNull();
		summary.QueueMetrics.ShouldBeEmpty();
	}

	[Fact]
	public void AllowInitializingWithQueueMetrics()
	{
		// Arrange
		var snapshot = new QueueMetricsSnapshot
		{
			MessagesReceived = 100,
			MessagesSent = 50,
			Errors = 5,
			AverageReceiveTime = 12.5,
			AverageSendTime = 8.3,
		};

		// Act
		var summary = new SqsChannelMetricsSummary
		{
			QueueMetrics = new Dictionary<string, QueueMetricsSnapshot>
			{
				["test-queue"] = snapshot,
			},
		};

		// Assert
		summary.QueueMetrics.Count.ShouldBe(1);
		summary.QueueMetrics["test-queue"].MessagesReceived.ShouldBe(100);
	}
}
