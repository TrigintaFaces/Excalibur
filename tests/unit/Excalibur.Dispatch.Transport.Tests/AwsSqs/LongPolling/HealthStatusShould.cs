// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Transport.Aws;

using AwsHealthStatus = Excalibur.Dispatch.Transport.Aws.HealthStatus;

namespace Excalibur.Dispatch.Transport.Tests.AwsSqs.LongPolling;

[Trait("Category", "Unit")]
[Trait("Component", "Transport")]
public sealed class HealthStatusShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var status = new AwsHealthStatus();

		// Assert
		status.IsHealthy.ShouldBeFalse();
		status.Status.ShouldBe("Initialized");
		status.ActiveQueues.ShouldBe(0);
		status.TotalMessagesProcessed.ShouldBe(0);
		status.EfficiencyScore.ShouldBe(0.0);
		status.LastActivityTime.ShouldBe(default);
		status.Details.ShouldNotBeNull();
		status.Details.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		// Arrange
		var lastActivity = DateTimeOffset.UtcNow;

		// Act
		var status = new AwsHealthStatus
		{
			IsHealthy = true,
			Status = "Running",
			ActiveQueues = 5,
			TotalMessagesProcessed = 150000,
			EfficiencyScore = 0.95,
			LastActivityTime = lastActivity,
		};
		status.Details["avgLatencyMs"] = 25.5;
		status.Details["strategy"] = "adaptive";

		// Assert
		status.IsHealthy.ShouldBeTrue();
		status.Status.ShouldBe("Running");
		status.ActiveQueues.ShouldBe(5);
		status.TotalMessagesProcessed.ShouldBe(150000);
		status.EfficiencyScore.ShouldBe(0.95);
		status.LastActivityTime.ShouldBe(lastActivity);
		status.Details.Count.ShouldBe(2);
		status.Details["avgLatencyMs"].ShouldBe(25.5);
		status.Details["strategy"].ShouldBe("adaptive");
	}
}
