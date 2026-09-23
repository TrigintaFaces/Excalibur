// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Cdc.Diagnostics;
using Excalibur.Data.CloudNative;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Excalibur.Cdc.Tests;

/// <summary>
/// The CDC health check reports a processor that keeps failing to reconnect, whether or not it ever marked
/// itself started.
/// </summary>
/// <remarks>
/// Streaming processors report their reconnect failures to the health state without recording a start or
/// any activity, so the check has to evaluate the failure count before its "not started" early return.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Cdc")]
public sealed class CdcHealthCheckReconnectFailureShould
{
	/// <summary>
	/// SAFETY. At the threshold the check is Unhealthy.
	/// </summary>
	/// <remarks>RED against a check that returns early for a processor that never recorded a start.</remarks>
	[Fact]
	public async Task ReportUnhealthy_AtTheConsecutiveFailureThreshold()
	{
		var state = new CdcHealthState();
		state.RecordConsecutiveTransientFailures(3);

		var result = await Check(state, threshold: 3);

		result.Status.ShouldBe(HealthStatus.Unhealthy);
		result.Data["ConsecutiveTransientFailures"].ShouldBe(3);
	}

	/// <summary>
	/// LIVENESS. Below the threshold, and after recovery, the failure count alone does not fail the check.
	/// </summary>
	[Theory]
	[InlineData(0)]
	[InlineData(2)]
	public async Task NotReportUnhealthy_BelowTheThreshold(int failures)
	{
		var state = new CdcHealthState();
		state.RecordConsecutiveTransientFailures(failures);

		var result = await Check(state, threshold: 3);

		result.Status.ShouldBe(HealthStatus.Healthy);
	}

	private static Task<HealthCheckResult> Check(CdcHealthState state, int threshold)
	{
		var options = Options.Create(new CdcHealthCheckOptions { UnhealthyConsecutiveTransientFailures = threshold });
		return new CdcHealthCheck(state, options).CheckHealthAsync(new HealthCheckContext(), CancellationToken.None);
	}
}
