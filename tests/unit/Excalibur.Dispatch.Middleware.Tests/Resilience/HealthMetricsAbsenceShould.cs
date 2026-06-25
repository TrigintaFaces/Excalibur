// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Sprint 847 / Lane L (bead 2pgyzy) — structural regression lock for the advertised-but-unwired
/// <see cref="HealthMetrics"/> fake-metric REMOVE (MS-L; joint PdM+SA ruling = REMOVE).
/// </summary>
/// <remarks>
/// <para>
/// <b>Defect (true pre-fix HEAD <c>301b4aa62</c>):</b> <see cref="HealthMetrics"/> exposed public
/// documented <c>init</c> props <c>ResponseTimeMs</c> and <c>ActiveConnections</c> that the sole producer
/// (<c>GracefulDegradationService.CollectHealthMetrics</c>) hardcoded to <c>0</c> and that
/// <c>DetermineLevel</c> never read — fabricated always-zero signals a consumer could mistake for real
/// telemetry (ADR-336 advertised-but-unwired class). Microsoft-first: never ship a fabricated metric.
/// </para>
/// <para>
/// <b>Fix = REMOVE:</b> the two props are deleted from the type, the producer's <c>= 0</c> initializers
/// dropped, and the four <c>PublicAPI.Shipped.txt</c> entries removed. For a pure deletion the lock is the
/// structural <em>absence</em> assertion (no behavioral impl to verify independently).
/// </para>
/// <para>
/// <b>Non-vacuity:</b> these reflection assertions are RED on the pre-fix HEAD (the members exist) and
/// GREEN after removal. The retained real fields (<c>CpuUsagePercent</c>, <c>MemoryUsagePercent</c>,
/// <c>ErrorRate</c>, <c>Timestamp</c>) are asserted present so the deletion is surgical, not over-broad.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class HealthMetricsAbsenceShould : UnitTestBase
{
	[Fact]
	public void NotExposeResponseTimeMs_AdvertisedButUnwiredFakeMetricRemoved()
	{
		typeof(HealthMetrics).GetProperty("ResponseTimeMs").ShouldBeNull(
			"ResponseTimeMs was an advertised-but-unwired fake metric (hardcoded 0, never read) and must be removed (MS-L).");
	}

	[Fact]
	public void NotExposeActiveConnections_AdvertisedButUnwiredFakeMetricRemoved()
	{
		typeof(HealthMetrics).GetProperty("ActiveConnections").ShouldBeNull(
			"ActiveConnections was an advertised-but-unwired fake metric (hardcoded 0, never read) and must be removed (MS-L).");
	}

	[Theory]
	[InlineData("CpuUsagePercent")]
	[InlineData("MemoryUsagePercent")]
	[InlineData("ErrorRate")]
	[InlineData("Timestamp")]
	public void RetainTheRealMetricFields(string propertyName)
	{
		typeof(HealthMetrics).GetProperty(propertyName).ShouldNotBeNull(
			$"the real metric '{propertyName}' must be retained — the REMOVE is surgical, not over-broad.");
	}
}
