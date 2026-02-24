// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Health;
using System.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Outbox.Tests.Health;

/// <summary>
/// Unit tests for <see cref="InboxHealthCheck"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Outbox")]
public sealed class InboxHealthCheckShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void ThrowOnNullState()
	{
		// Arrange
		var options = Options.Create(new InboxHealthCheckOptions());

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InboxHealthCheck(null!, options));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() => new InboxHealthCheck(state, null!));
	}

	[Fact]
	public void CreateWithValidParameters()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		var options = Options.Create(new InboxHealthCheckOptions());

		// Act & Assert
		Should.NotThrow(() => new InboxHealthCheck(state, options));
	}

	#endregion Constructor Tests

	#region Healthy Status Tests

	[Fact]
	public async Task ReturnHealthyWhenRunningAndActive()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("processing normally");
	}

	[Fact]
	public async Task IncludeIsRunningInData()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("IsRunning");
		result.Data["IsRunning"].ShouldBe(true);
	}

	[Fact]
	public async Task IncludeTotalProcessedInData()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(100, 5);

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("TotalProcessed");
		result.Data["TotalProcessed"].ShouldBe(100L);
	}

	[Fact]
	public async Task IncludeTotalFailedInData()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(90, 10);

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("TotalFailed");
		result.Data["TotalFailed"].ShouldBe(10L);
	}

	[Fact]
	public async Task IncludeTotalCyclesInData()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);
		state.RecordCycle(20, 0);

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("TotalCycles");
		result.Data["TotalCycles"].ShouldBe(2L);
	}

	[Fact]
	public async Task IncludeLastActivityTimeWhenPresent()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("LastActivityTime");
	}

	#endregion Healthy Status Tests

	#region Unhealthy Status Tests

	[Fact]
	public async Task ReturnUnhealthyWhenNotRunning()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		// Not started

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not running");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenStoppedAfterRunning()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);
		state.MarkStopped();

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not running");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenInactiveAboveUnhealthyThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		// Simulate old activity by setting a very short timeout
		state.RecordCycle(10, 0);

		var options = Options.Create(new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.Zero, // Zero timeout means immediately unhealthy
			DegradedInactivityTimeout = TimeSpan.Zero,
		});

		WaitForClockTick();

		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("inactive");
	}

	#endregion Unhealthy Status Tests

	#region Degraded Status Tests

	[Fact]
	public async Task ReturnDegradedWhenInactiveAboveDegradedThreshold()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new InboxHealthCheckOptions
		{
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(10), // High unhealthy threshold
			DegradedInactivityTimeout = TimeSpan.Zero, // Zero degraded threshold
		});

		WaitForClockTick();

		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("inactive");
	}

	[Fact]
	public async Task IncludeInactivitySecondsInDataWhenInactive()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		state.RecordCycle(10, 0);

		var options = Options.Create(new InboxHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.Zero,
			UnhealthyInactivityTimeout = TimeSpan.FromHours(1),
		});

		WaitForClockTick();

		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert
		result.Data.ShouldContainKey("InactivitySeconds");
	}

	#endregion Degraded Status Tests

	#region Edge Cases

	[Fact]
	public async Task HandleNoActivityTimeGracefully()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();
		// No RecordCycle call, so no LastActivityTime

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		// Act
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);

		// Assert - Should return healthy since we can't determine inactivity
		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	[Fact]
	public async Task SupportCancellation()
	{
		// Arrange
		var state = new BackgroundServiceHealthState();
		state.MarkStarted();

		var options = Options.Create(new InboxHealthCheckOptions());
		var healthCheck = new InboxHealthCheck(state, options);

		using var cts = new CancellationTokenSource();

		// Act & Assert - Should complete without throwing
		var result = await healthCheck.CheckHealthAsync(new HealthCheckContext(), cts.Token);
		result.Status.ShouldBeOneOf(HealthStatus.Healthy, HealthStatus.Degraded, HealthStatus.Unhealthy);
	}

	#endregion Edge Cases

	private static void WaitForClockTick()
	{
		var start = Stopwatch.StartNew();
		_ = SpinWait.SpinUntil(() => start.ElapsedMilliseconds >= 2, TimeSpan.FromSeconds(1));
	}
}
