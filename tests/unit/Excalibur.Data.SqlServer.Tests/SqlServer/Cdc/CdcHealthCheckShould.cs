// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Data.SqlServer.Cdc;

using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

using HealthStatus = Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus;

namespace Excalibur.Data.Tests.SqlServer.Cdc;

public sealed class CdcHealthCheckShould
{
	private readonly CdcHealthState _state;
	private readonly CdcHealthCheck _sut;

	public CdcHealthCheckShould()
	{
		_state = new CdcHealthState();
		var options = Microsoft.Extensions.Options.Options.Create(new CdcHealthCheckOptions());
		_sut = new CdcHealthCheck(_state, options);
	}

	[Fact]
	public void ThrowOnNullState()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CdcHealthCheckOptions());

		Should.Throw<ArgumentNullException>(() => new CdcHealthCheck(null!, options));
	}

	[Fact]
	public void ThrowOnNullOptions()
	{
		var state = new CdcHealthState();

		Should.Throw<ArgumentNullException>(() => new CdcHealthCheck(state, null!));
	}

	[Fact]
	public async Task ReturnHealthyWhenNotStartedAndNoCycles()
	{
		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("not been started");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenStoppedAfterRunning()
	{
		_state.MarkStarted();
		_state.RecordCycle(10, 0);
		_state.MarkStopped();

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("not running");
	}

	[Fact]
	public async Task ReturnHealthyWhenRunningAndActive()
	{
		_state.MarkStarted();
		_state.RecordCycle(5, 0);

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Healthy);
		result.Description.ShouldContain("operating normally");
	}

	[Fact]
	public async Task IncludeProcessingDataInResult()
	{
		_state.MarkStarted();
		_state.RecordCycle(10, 2);
		_state.RecordCycle(5, 1);

		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Data["IsRunning"].ShouldBe(true);
		result.Data["TotalProcessed"].ShouldBe(15L);
		result.Data["TotalFailed"].ShouldBe(3L);
		result.Data["TotalCycles"].ShouldBe(2L);
		result.Data.ShouldContainKey("LastActivityTime");
	}

	[Fact]
	public async Task ReturnDegradedWhenInactivityExceedsDegradedThreshold()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CdcHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromMilliseconds(1),
			UnhealthyInactivityTimeout = TimeSpan.FromMinutes(10),
		});
		var sut = new CdcHealthCheck(_state, options);

		_state.MarkStarted();
		_state.RecordCycle(1, 0);

		// Wait just past the degraded threshold
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Degraded);
		result.Description.ShouldContain("inactive");
	}

	[Fact]
	public async Task ReturnUnhealthyWhenInactivityExceedsUnhealthyThreshold()
	{
		var options = Microsoft.Extensions.Options.Options.Create(new CdcHealthCheckOptions
		{
			DegradedInactivityTimeout = TimeSpan.FromMilliseconds(1),
			UnhealthyInactivityTimeout = TimeSpan.FromMilliseconds(1),
		});
		var sut = new CdcHealthCheck(_state, options);

		_state.MarkStarted();
		_state.RecordCycle(1, 0);

		// Wait past the unhealthy threshold
		await global::Tests.Shared.Infrastructure.TestTiming.PauseAsync(50);

		var result = await sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Description.ShouldContain("inactive");
	}

	[Fact]
	public async Task NotIncludeLastActivityTimeWhenNeverActive()
	{
		// Not started, no cycles — LastActivityTime is null
		var result = await _sut.CheckHealthAsync(null!, CancellationToken.None);

		result.Data.ShouldNotContainKey("LastActivityTime");
	}
}

