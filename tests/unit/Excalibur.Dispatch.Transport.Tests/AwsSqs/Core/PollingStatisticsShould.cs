// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.Core;

[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
public sealed class PollingStatisticsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		// Arrange & Act
		var stats = new PollingStatistics();

		// Assert
		stats.TotalAttempts.ShouldBe(0);
		stats.SuccessfulAttempts.ShouldBe(0);
		stats.FailedAttempts.ShouldBe(0);
		stats.TotalMessagesReceived.ShouldBe(0);
		stats.AverageMessagesPerPoll.ShouldBe(0.0);
		stats.AveragePollDuration.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var stats = new PollingStatistics
		{
			TotalAttempts = 1000,
			SuccessfulAttempts = 980,
			FailedAttempts = 20,
			TotalMessagesReceived = 5000,
			AverageMessagesPerPoll = 5.1,
			AveragePollDuration = TimeSpan.FromMilliseconds(250),
		};

		// Assert
		stats.TotalAttempts.ShouldBe(1000);
		stats.SuccessfulAttempts.ShouldBe(980);
		stats.FailedAttempts.ShouldBe(20);
		stats.TotalMessagesReceived.ShouldBe(5000);
		stats.AverageMessagesPerPoll.ShouldBe(5.1);
		stats.AveragePollDuration.ShouldBe(TimeSpan.FromMilliseconds(250));
	}
}
