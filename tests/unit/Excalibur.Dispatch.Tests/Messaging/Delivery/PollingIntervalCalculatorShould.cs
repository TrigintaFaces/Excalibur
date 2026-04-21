// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

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
