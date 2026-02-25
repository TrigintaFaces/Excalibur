// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.SessionManagement;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class SessionMetricsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var metrics = new SessionMetrics();

		// Assert
		metrics.TotalProcessingTime.ShouldBe(TimeSpan.Zero);
		metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.Zero);
		metrics.SuccessfulMessages.ShouldBe(0);
		metrics.FailedMessages.ShouldBe(0);
		metrics.RetriedMessages.ShouldBe(0);
		metrics.LockRenewals.ShouldBe(0);
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange & Act
		var metrics = new SessionMetrics
		{
			TotalProcessingTime = TimeSpan.FromMinutes(5),
			AverageMessageProcessingTime = TimeSpan.FromMilliseconds(250),
			SuccessfulMessages = 100,
			FailedMessages = 5,
			RetriedMessages = 10,
			LockRenewals = 3,
		};

		// Assert
		metrics.TotalProcessingTime.ShouldBe(TimeSpan.FromMinutes(5));
		metrics.AverageMessageProcessingTime.ShouldBe(TimeSpan.FromMilliseconds(250));
		metrics.SuccessfulMessages.ShouldBe(100);
		metrics.FailedMessages.ShouldBe(5);
		metrics.RetriedMessages.ShouldBe(10);
		metrics.LockRenewals.ShouldBe(3);
	}
}
