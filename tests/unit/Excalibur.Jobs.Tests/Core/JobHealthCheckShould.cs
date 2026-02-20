// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Unit tests for <see cref="JobHealthCheck"/> and <see cref="JobHeartbeatTracker"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "HealthChecks")]
public sealed class JobHealthCheckShould
{
	private readonly JobHeartbeatTracker _tracker = new();

	[Fact]
	public async Task ReturnUnhealthy_WhenNoHeartbeatRecorded()
	{
		// Arrange
		var config = new TestJobConfig();
		var healthCheck = new JobHealthCheck("test-job-" + Guid.NewGuid(), config, _tracker);
		var context = new HealthCheckContext();

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("has not reported a heartbeat recently");
	}

	[Fact]
	public async Task ReturnHealthy_WhenRecentHeartbeatRecorded()
	{
		// Arrange
		var jobName = "healthy-job-" + Guid.NewGuid();
		var config = new TestJobConfig();
		var healthCheck = new JobHealthCheck(jobName, config, _tracker);
		var context = new HealthCheckContext();

		// Record a heartbeat
		_tracker.RecordHeartbeat(jobName);

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("is healthy");
		result.Description.ShouldContain("Last heartbeat");
	}

	[Fact]
	public async Task ReturnDegraded_WhenHeartbeatExceedsDegradedThreshold()
	{
		// Arrange
		var jobName = "degraded-job-" + Guid.NewGuid();
		var config = new TestJobConfig
		{
			DegradedThreshold = TimeSpan.FromMilliseconds(1),
			UnhealthyThreshold = TimeSpan.FromHours(1),
		};
		var healthCheck = new JobHealthCheck(jobName, config, _tracker);
		var context = new HealthCheckContext();

		_tracker.RecordHeartbeat(jobName);

		// Wait past the degraded threshold
		await Task.Delay(50);

		// Act
		var result = await healthCheck.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("is degraded");
	}

	[Fact]
	public void RecordHeartbeat_UpdatesExistingEntry()
	{
		// Arrange
		var jobName = "update-test-" + Guid.NewGuid();

		// Act
		_tracker.RecordHeartbeat(jobName);
		var first = _tracker.GetLastHeartbeat(jobName);
		_tracker.RecordHeartbeat(jobName);
		var second = _tracker.GetLastHeartbeat(jobName);

		// Assert
		first.ShouldNotBeNull();
		second.ShouldNotBeNull();
		second.Value.ShouldBeGreaterThanOrEqualTo(first.Value);
	}

	[Fact]
	public void GetLastHeartbeat_ReturnsNull_WhenNoHeartbeatRecorded()
	{
		// Act
		var result = _tracker.GetLastHeartbeat("nonexistent-job");

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task SupportMultipleJobs()
	{
		// Arrange
		var jobName1 = "multi-job-1-" + Guid.NewGuid();
		var jobName2 = "multi-job-2-" + Guid.NewGuid();
		var config = new TestJobConfig();

		var healthCheck1 = new JobHealthCheck(jobName1, config, _tracker);
		var healthCheck2 = new JobHealthCheck(jobName2, config, _tracker);
		var context = new HealthCheckContext();

		// Record heartbeat only for job1
		_tracker.RecordHeartbeat(jobName1);

		// Act
		var result1 = await healthCheck1.CheckHealthAsync(context, CancellationToken.None);
		var result2 = await healthCheck2.CheckHealthAsync(context, CancellationToken.None);

		// Assert
		result1.Status.ShouldBe(HealthStatus.Healthy);
		result2.Status.ShouldBe(HealthStatus.Unhealthy);
	}

	private sealed class TestJobConfig : JobConfig
	{
	}
}
