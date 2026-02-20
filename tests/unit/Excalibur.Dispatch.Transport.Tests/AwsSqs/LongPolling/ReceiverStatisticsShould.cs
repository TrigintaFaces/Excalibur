// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using AwsPollingStatus = Excalibur.Dispatch.Transport.Aws.PollingStatus;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class ReceiverStatisticsShould
{
	[Fact]
	public void RequireAllProperties()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;

		// Act
		var stats = new ReceiverStatistics
		{
			TotalReceiveOperations = 100,
			TotalMessagesReceived = 500,
			TotalMessagesDeleted = 450,
			VisibilityTimeoutOptimizations = 25,
			LastReceiveTime = now,
			PollingStatus = AwsPollingStatus.Active,
		};

		// Assert
		stats.TotalReceiveOperations.ShouldBe(100);
		stats.TotalMessagesReceived.ShouldBe(500);
		stats.TotalMessagesDeleted.ShouldBe(450);
		stats.VisibilityTimeoutOptimizations.ShouldBe(25);
		stats.LastReceiveTime.ShouldBe(now);
		stats.PollingStatus.ShouldBe(AwsPollingStatus.Active);
	}

	[Theory]
	[InlineData(AwsPollingStatus.Inactive)]
	[InlineData(AwsPollingStatus.Active)]
	[InlineData(AwsPollingStatus.Stopping)]
	[InlineData(AwsPollingStatus.Error)]
	public void AcceptAllPollingStatuses(AwsPollingStatus status)
	{
		// Act
		var stats = new ReceiverStatistics
		{
			TotalReceiveOperations = 1,
			TotalMessagesReceived = 1,
			TotalMessagesDeleted = 1,
			VisibilityTimeoutOptimizations = 0,
			LastReceiveTime = DateTimeOffset.UtcNow,
			PollingStatus = status,
		};

		// Assert
		stats.PollingStatus.ShouldBe(status);
	}
}
