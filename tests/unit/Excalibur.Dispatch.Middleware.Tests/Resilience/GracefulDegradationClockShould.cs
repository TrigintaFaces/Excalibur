// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

using MsOptions = Microsoft.Extensions.Options.Options;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Binds the degradation decision to an injected clock: the rolling error window, the health-check
/// cadence and the level-change stamp must all read the same controllable time source.
/// </summary>
/// <remarks>
/// <para>
/// Every input to the auto-adjust decision is time-relative. The error rate is measured over a rolling
/// window keyed on ticks, the health check runs on a timer, and the CPU figure is a delta divided by
/// elapsed time. If any of those reads the ambient system clock, a test cannot reach a level change at
/// all -- it can only sleep for the health-check interval and hope, which is why the interval in the
/// existing tests is set to an hour purely to switch the behaviour off.
/// </para>
/// <para>
/// Both arms are needed. The change arm alone is satisfied by a service that degrades on any tick
/// whatsoever; the no-change arm alone is satisfied by one that never degrades. Together they say the
/// advance, and only the advance, produced the level change.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class GracefulDegradationClockShould
{
	private static readonly TimeSpan HealthCheckInterval = TimeSpan.FromSeconds(30);

	[Fact]
	public async Task DegradeOnAHealthCheckTickDrivenByTheInjectedClock()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateService(clock);

		// Fail every call, so the rolling error window fills with failures.
		for (var i = 0; i < 10; i++)
		{
			_ = await Should.ThrowAsync<InvalidOperationException>(() => sut.ExecuteWithDegradationAsync(FailingOperation(), TestContext.Current.CancellationToken));
		}

		sut.CurrentLevel.ShouldBe(
			DegradationLevel.Normal,
			"the failures are recorded but no health check has been driven, so nothing has evaluated them "
			+ "yet -- if the level has already moved, the check is running off the ambient system clock");

		clock.Advance(HealthCheckInterval);

		sut.CurrentLevel.ShouldNotBe(
			DegradationLevel.Normal,
			"advancing the injected clock by one health-check interval must run the check, which reads the "
			+ "error window off the same clock and auto-adjusts");
	}

	[Fact]
	public async Task StampTheLevelChangeFromTheInjectedClock()
	{
		var clock = new FakeTimeProvider();
		await using var sut = CreateService(clock);

		clock.Advance(TimeSpan.FromDays(3));
		sut.SetLevel(DegradationLevel.Minor, "driven by the test");

		sut.GetMetrics().LastLevelChange.ShouldBe(
			clock.GetUtcNow(),
			"the level-change stamp is reported to consumers and must come from the same clock the "
			+ "decision was made on, not from the ambient one");

		await Task.CompletedTask;
	}

	private static GracefulDegradationService CreateService(TimeProvider clock) =>
		new(
			MsOptions.Create(new GracefulDegradationOptions
			{
				HealthCheckInterval = HealthCheckInterval,
				EnableAutoAdjustment = true,
				ErrorRateWindow = TimeSpan.FromMinutes(1),
				ErrorRateWindowBuckets = 6,

				// Only the error rate may trigger a level here. CPU and memory are real process readings,
				// so leaving their thresholds reachable would let this pass for a reason unrelated to the
				// clock under test.
				Levels =
				[
					new DegradationLevelConfig("Minor", 0, 0.5, 1000, 1000),
				],
			}),
			NullLogger<GracefulDegradationService>.Instance,
			clock);

	private static DegradationContext<int> FailingOperation() => new()
	{
		OperationName = "clock-driven-operation",
		PrimaryOperation = () => throw new InvalidOperationException("primary fails"),
	};
}
