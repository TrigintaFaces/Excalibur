// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class PollingIntervalCalculatorShould
{
	[Fact]
	public void ReturnDefaultInterval_WhenAdaptivePollingDisabled()
	{
		var interval = PollingIntervalCalculator.GetInitialInterval(
			enableAdaptivePolling: false,
			minInterval: TimeSpan.FromSeconds(1),
			defaultInterval: TimeSpan.FromSeconds(30));

		interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ReturnMinInterval_WhenAdaptivePollingEnabled()
	{
		var interval = PollingIntervalCalculator.GetInitialInterval(
			enableAdaptivePolling: true,
			minInterval: TimeSpan.FromSeconds(1),
			defaultInterval: TimeSpan.FromSeconds(30));

		interval.ShouldBe(TimeSpan.FromSeconds(1));
	}

	[Fact]
	public void HoldTheConfiguredInterval_WhenAdaptivePollingDisabled()
	{
		// The branch this locks had NO coverage, which is how it came to be read as a defect: on its own the
		// method looks like it discards the configured interval for an unrelated maximum after the first
		// poll. The caller passes its configured poll interval as maxInterval, so the steady state IS that
		// interval -- and it must equal what GetInitialInterval returns for the same configuration, which is
		// the property actually worth locking. A regression that made the two disagree would silently slow
		// every non-adaptive scheduler after its first poll.
		var initial = PollingIntervalCalculator.GetInitialInterval(
			enableAdaptivePolling: false,
			minInterval: TimeSpan.FromSeconds(1),
			defaultInterval: TimeSpan.FromSeconds(30));

		var next = PollingIntervalCalculator.GetNextInterval(
			currentInterval: initial,
			hadWork: false,
			enableAdaptivePolling: false,
			minInterval: TimeSpan.FromSeconds(1),
			maxInterval: TimeSpan.FromSeconds(30),
			backoffMultiplier: 2.0);

		next.ShouldBe(TimeSpan.FromSeconds(30));
		next.ShouldBe(initial, "a non-adaptive scheduler must poll at one steady interval, not shift after the first poll");
	}

	[Fact]
	public void HoldTheConfiguredInterval_WhenAdaptivePollingDisabledAndWorkWasFound()
	{
		// hadWork must not move the interval either when adaptive polling is off -- otherwise 'disabled'
		// would still be adapting, just on a different trigger.
		var next = PollingIntervalCalculator.GetNextInterval(
			currentInterval: TimeSpan.FromSeconds(30),
			hadWork: true,
			enableAdaptivePolling: false,
			minInterval: TimeSpan.FromSeconds(1),
			maxInterval: TimeSpan.FromSeconds(30),
			backoffMultiplier: 2.0);

		next.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void BackOffInterval_WhenIdleInAdaptiveMode()
	{
		var interval = PollingIntervalCalculator.GetNextInterval(
			currentInterval: TimeSpan.FromSeconds(2),
			hadWork: false,
			enableAdaptivePolling: true,
			minInterval: TimeSpan.FromSeconds(1),
			maxInterval: TimeSpan.FromSeconds(30),
			backoffMultiplier: 2.0);

		interval.ShouldBe(TimeSpan.FromSeconds(4));
	}

	[Fact]
	public void CapBackoffAtMaxInterval_WhenIdleInAdaptiveMode()
	{
		var interval = PollingIntervalCalculator.GetNextInterval(
			currentInterval: TimeSpan.FromSeconds(20),
			hadWork: false,
			enableAdaptivePolling: true,
			minInterval: TimeSpan.FromSeconds(1),
			maxInterval: TimeSpan.FromSeconds(30),
			backoffMultiplier: 2.0);

		interval.ShouldBe(TimeSpan.FromSeconds(30));
	}

	[Fact]
	public void ApplyJitter_ReturnsValueWithinExpectedBounds()
	{
		var interval = TimeSpan.FromSeconds(10);
		var jittered = PollingIntervalCalculator.ApplyJitter(interval, jitterRatio: 0.1);

		jittered.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromSeconds(9));
		jittered.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(11));
	}
}
