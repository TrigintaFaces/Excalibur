using Excalibur.Jobs;

using FakeItEasy;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

using Shouldly;

namespace Excalibur.Tests.Unit.Jobs;

public class JobHealthCheckShould
{
	[Fact]
	public void ConstructWithValidParameters()
	{
		// Arrange
		var jobName = "TestJob";
		var jobConfig = new JobConfig
		{
			Disabled = false,
			DegradedThreshold = TimeSpan.FromMinutes(3),
			UnhealthyThreshold = TimeSpan.FromMinutes(5)
		};
		var logger = A.Fake<ILogger<JobHealthCheck>>();

		// Act
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Assert
		_ = healthCheck.ShouldNotBeNull();
	}

	[Fact]
	public async Task CheckHealthAsyncReturnsHealthyWhenDisabled()
	{
		// Arrange
		var jobName = "TestJob";
		var jobConfig = new JobConfig { Disabled = true };
		var logger = A.Fake<ILogger<JobHealthCheck>>();
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldBe($"Job {jobName} is disabled.");
	}

	[Fact]
	public async Task CheckHealthAsyncReturnsHealthyWhenHeartbeatIsActive()
	{
		// Arrange
		var jobName = "TestJob";
		var jobConfig = new JobConfig
		{
			Disabled = false,
			DegradedThreshold = TimeSpan.FromMinutes(3),
			UnhealthyThreshold = TimeSpan.FromMinutes(5)
		};
		var logger = A.Fake<ILogger<JobHealthCheck>>();
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Signal a heartbeat
		JobHealthCheck.Heartbeat(jobName);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("Last heartbeat was");
	}

	[Fact]
	public async Task CheckHealthAsyncReturnsDegradedWhenHeartbeatExceedsThreshold()
	{
		// Arrange
		var jobName = "DegradedJob";
		var jobConfig = new JobConfig
		{
			Disabled = false,
			DegradedThreshold = TimeSpan.FromMilliseconds(1), // Set very low for testing
			UnhealthyThreshold = TimeSpan.FromSeconds(10) // Set high enough so we don't hit unhealthy
		};
		var logger = A.Fake<ILogger<JobHealthCheck>>();
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Signal a heartbeat and wait for it to exceed degraded threshold
		JobHealthCheck.Heartbeat(jobName);
		await Task.Delay(10).ConfigureAwait(true); // Ensure we exceed the degraded threshold

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("is degraded");
	}

	[Fact]
	public async Task CheckHealthAsyncReturnsUnhealthyWhenHeartbeatExceedsThreshold()
	{
		// Arrange
		var jobName = "UnhealthyJob";
		var jobConfig = new JobConfig
		{
			Disabled = false,
			DegradedThreshold = TimeSpan.FromMilliseconds(1),
			UnhealthyThreshold = TimeSpan.FromMilliseconds(5) // Very short threshold to ensure it becomes unhealthy
		};
		var logger = A.Fake<ILogger<JobHealthCheck>>();
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Signal a heartbeat and wait for it to exceed unhealthy threshold
		JobHealthCheck.Heartbeat(jobName);
		await Task.Delay(10).ConfigureAwait(true); // Ensure we exceed the unhealthy threshold

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("is unhealthy");
	}

	[Fact]
	public async Task CheckHealthAsyncReturnsUnhealthyWhenNoHeartbeatRecorded()
	{
		// Arrange
		var jobName = "NoHeartbeatJob";
		var jobConfig = new JobConfig
		{
			Disabled = false,
			DegradedThreshold = TimeSpan.FromMinutes(3),
			UnhealthyThreshold = TimeSpan.FromMinutes(5)
		};
		var logger = A.Fake<ILogger<JobHealthCheck>>();
		var healthCheck = new JobHealthCheck(jobName, jobConfig, logger);

		// Act - no heartbeat signaled for this job
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext()).ConfigureAwait(true);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("is unhealthy");
	}

	[Fact]
	public void HeartbeatUpdatesLastHeartbeatTime()
	{
		// Arrange
		var jobName = "HeartbeatTestJob";

		// Act
		JobHealthCheck.Heartbeat(jobName);

		// Assert - We can't directly test the static heartbeats dictionary Instead, verify through GetLastHeartbeat method
		var lastHeartbeat = JobHealthCheck.GetLastHeartbeat(jobName);

		// The heartbeat should be recent (within the last second)
		(DateTime.UtcNow - lastHeartbeat).TotalSeconds.ShouldBeLessThan(1);
	}

	[Fact]
	public void GetLastHeartbeatReturnsMinValueWhenNoHeartbeatExists()
	{
		// Arrange
		var nonExistentJob = "NonExistentJob";

		// Act
		var result = JobHealthCheck.GetLastHeartbeat(nonExistentJob);

		// Assert
		result.ShouldBe(DateTime.MinValue);
	}
}
