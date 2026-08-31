// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Core;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Excalibur.Jobs.Tests.Core;

/// <summary>
/// Binds the job health decision to an injected clock: whether a job reads healthy, degraded or
/// unhealthy is decided by how old its heartbeat is, and that age must be measurable without waiting.
/// </summary>
/// <remarks>
/// <para>
/// The heartbeat is stamped by one component and aged by another, so both sides have to take the same
/// injected clock. If either reads the ambient system clock, the only way to reach the degraded or
/// unhealthy branch in a test is to sleep for the threshold — which makes the suite slow, makes it
/// non-deterministic on a loaded machine, and in practice pushes the thresholds under test down to
/// unrealistic millisecond values that no consumer would configure.
/// </para>
/// <para>
/// All three arms are needed together. Any one of them passes on its own for the wrong reason: a health
/// check hardwired to return Healthy passes the first, one hardwired to Unhealthy passes the third, and
/// a clock that ignores the advance entirely passes the first alone. Driving one clock across the three
/// thresholds and getting three different answers is what says the decision follows the injected time.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "HealthChecks")]
public sealed class JobHealthCheckClockShould
{
	private static readonly TimeSpan DegradedThreshold = TimeSpan.FromMinutes(5);
	private static readonly TimeSpan UnhealthyThreshold = TimeSpan.FromMinutes(30);

	[Fact]
	public async Task CrossHealthyDegradedAndUnhealthyAsTheInjectedClockAdvances()
	{
		var clock = new FakeTimeProvider();
		var tracker = new JobHeartbeatTracker(clock);
		var jobName = "clock-driven-job";
		var config = new TestJobOptions
		{
			DegradedThreshold = DegradedThreshold,
			UnhealthyThreshold = UnhealthyThreshold,
		};
		var healthCheck = new JobHealthCheck(jobName, config, tracker, clock);
		var context = new HealthCheckContext();

		tracker.RecordHeartbeat(jobName);

		var whileFresh = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);
		whileFresh.Status.ShouldBe(
			HealthStatus.Healthy,
			"no time has been driven since the heartbeat, so the job is inside the degraded threshold");

		// Past the degraded threshold, still inside the unhealthy one.
		clock.Advance(DegradedThreshold + TimeSpan.FromSeconds(1));

		var whenStale = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);
		whenStale.Status.ShouldBe(
			HealthStatus.Degraded,
			"the heartbeat is now older than the degraded threshold and younger than the unhealthy one -- "
			+ "reaching this state took no wall-clock time at all");
		whenStale.Description.ShouldContain("is degraded");

		// Past the unhealthy threshold.
		clock.Advance(UnhealthyThreshold);

		var whenStopped = await healthCheck.CheckHealthAsync(context, TestContext.Current.CancellationToken);
		whenStopped.Status.ShouldBe(
			HealthStatus.Unhealthy,
			"the heartbeat is now older than the unhealthy threshold");
	}

	[Fact]
	public async Task StampHeartbeatsFromTheInjectedClockRatherThanTheSystemClock()
	{
		var clock = new FakeTimeProvider(new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero));
		var tracker = new JobHeartbeatTracker(clock);
		var jobName = "stamped-job";

		tracker.RecordHeartbeat(jobName);

		tracker.GetLastHeartbeat(jobName).ShouldBe(
			clock.GetUtcNow(),
			"the stamp must come from the injected clock -- if it comes from the system clock the health "
			+ "check ages it against a different timeline and the two can never be driven together");

		await Task.CompletedTask;
	}

	private sealed class TestJobOptions : JobOptions
	{
	}
}
