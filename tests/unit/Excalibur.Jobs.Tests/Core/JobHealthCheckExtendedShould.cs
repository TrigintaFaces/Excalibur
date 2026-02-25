// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;
using Excalibur.Jobs.Outbox;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Extended unit tests for <see cref="JobHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Platform")]
[Trait("Feature", "Jobs")]
public sealed class JobHealthCheckExtendedShould
{
	[Fact]
	public async Task ReportHealthyWhenRecentHeartbeat()
	{
		// Arrange
		var tracker = new JobHeartbeatTracker();
		tracker.RecordHeartbeat("test-job");

		var config = new OutboxJobConfig
		{
			JobName = "test-job",
			DegradedThreshold = TimeSpan.FromMinutes(5),
			UnhealthyThreshold = TimeSpan.FromMinutes(10),
		};
		var healthCheck = new JobHealthCheck("test-job", config, tracker);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task ReportUnhealthyWhenNoHeartbeat()
	{
		// Arrange
		var tracker = new JobHeartbeatTracker();
		var config = new OutboxJobConfig
		{
			JobName = "test-job",
			DegradedThreshold = TimeSpan.FromMinutes(5),
			UnhealthyThreshold = TimeSpan.FromMinutes(10),
		};
		var healthCheck = new JobHealthCheck("test-job", config, tracker);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	[Fact]
	public void ThrowWhenConfigIsNull()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new JobHealthCheck("test-job", null!, new JobHeartbeatTracker()));
	}

	[Fact]
	public void ThrowWhenTrackerIsNull()
	{
		// Arrange
		var config = new OutboxJobConfig { JobName = "test-job" };

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			new JobHealthCheck("test-job", config, null!));
	}
}
