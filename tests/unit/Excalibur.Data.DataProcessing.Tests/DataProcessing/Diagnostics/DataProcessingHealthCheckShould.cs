// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.DataProcessing.Diagnostics;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Data.Tests.DataProcessing.Diagnostics;

/// <summary>
/// Unit tests for <see cref="DataProcessingHealthCheck"/>.
/// </summary>
[UnitTest]
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DataProcessingHealthCheckShould : UnitTestBase
{
	private readonly DataProcessingHealthState _state = new();
	private readonly DataProcessingHealthCheckOptions _options = new();

	private DataProcessingHealthCheck CreateHealthCheck()
	{
		return new DataProcessingHealthCheck(
			_state,
			Options.Create(_options));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenStateIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingHealthCheck(null!, Options.Create(_options)));
	}

	[Fact]
	public void ThrowArgumentNullException_WhenOptionsIsNull()
	{
		Should.Throw<ArgumentNullException>(() =>
			new DataProcessingHealthCheck(_state, null!));
	}

	[Fact]
	public async Task ReturnHealthy_WhenServiceNotYetStarted()
	{
		// Arrange — state is fresh, no cycles run
		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("not been started");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenServiceStoppedAfterRunning()
	{
		// Arrange — simulate a service that ran and then stopped
		_state.MarkStarted();
		_state.RecordCycle(succeeded: true);
		_state.MarkStopped();

		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not running");
	}

	[Fact]
	public async Task ReturnHealthy_WhenRunningWithRecentActivity()
	{
		// Arrange
		_state.MarkStarted();
		_state.RecordCycle(succeeded: true);
		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("operating normally");
	}

	[Fact]
	public async Task IncludeMetrics_InHealthCheckData()
	{
		// Arrange
		_state.MarkStarted();
		_state.RecordCycle(succeeded: true);
		_state.RecordProcessed(42);
		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Data.ShouldContainKey("IsRunning");
		result.Data["IsRunning"].ShouldBe(true);
		result.Data.ShouldContainKey("TotalProcessed");
		result.Data["TotalProcessed"].ShouldBe(42L);
		result.Data.ShouldContainKey("TotalFailed");
		result.Data["TotalFailed"].ShouldBe(0L);
		result.Data.ShouldContainKey("TotalCycles");
		result.Data["TotalCycles"].ShouldBe(1L);
		result.Data.ShouldContainKey("LastActivityTime");
	}

	[Fact]
	public async Task ReturnUnhealthy_WhenInactivityExceedsUnhealthyTimeout()
	{
		// Arrange — simulate old activity by setting a very short timeout
		_state.MarkStarted();
		_state.RecordCycle(succeeded: true);

		// Use very short timeouts so the test doesn't need to wait
		_options.DegradedInactivityTimeout = TimeSpan.FromMilliseconds(1);
		_options.UnhealthyInactivityTimeout = TimeSpan.FromMilliseconds(1);

		// Wait just enough for the timeout to elapse
		await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);

		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("inactive");
		result.Data.ShouldContainKey("InactivitySeconds");
	}

	[Fact]
	public async Task ReturnDegraded_WhenInactivityExceedsDegradedButNotUnhealthy()
	{
		// Arrange
		_state.MarkStarted();
		_state.RecordCycle(succeeded: true);

		// Short degraded, long unhealthy
		_options.DegradedInactivityTimeout = TimeSpan.FromMilliseconds(1);
		_options.UnhealthyInactivityTimeout = TimeSpan.FromHours(1);

		await Task.Delay(10, CancellationToken.None).ConfigureAwait(false);

		var healthCheck = CreateHealthCheck();

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("inactive");
	}

	[Fact]
	public async Task NotIncludeLastActivityTime_WhenNoActivityHasOccurred()
	{
		// Arrange — running but no cycles recorded
		// (This state shouldn't normally occur but tests the branch)
		var state = new DataProcessingHealthState();
		var healthCheck = new DataProcessingHealthCheck(
			state, Options.Create(new DataProcessingHealthCheckOptions()));

		// Act
		var result = await healthCheck.CheckHealthAsync(
			new HealthCheckContext(), CancellationToken.None).ConfigureAwait(false);

		// Assert — not started, zero cycles → "not been started" (Healthy)
		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Data.ShouldNotContainKey("LastActivityTime");
	}
}
