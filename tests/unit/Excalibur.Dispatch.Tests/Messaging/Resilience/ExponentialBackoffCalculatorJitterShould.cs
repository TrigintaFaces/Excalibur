// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

// Sprint 849 / Lane R3 (gejhft+ffxglb), EC-R3.2 — seedable jitter. Pre-fix ApplyJitter used Random.Shared with
// no injection point, so a jittered delay could not be made deterministic for a lock. The fix adds an optional
// `Func<double>? jitterSource` ctor parameter (default Random.Shared). RED pre-fix: the parameter did not exist
// (the seeded-source overload does not compile). With a controllable source the delay is a pure function of the
// attempt and the source sequence — the determinism EC-R3.2 requires for the backoff-apply locks.
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ExponentialBackoffCalculatorJitterShould
{
	[Fact]
	public void ProduceTheUpperJitterBound_WhenSourceReturnsOne()
	{
		// jitter = (source*2 - 1) * (delay * jitterFactor); source=1.0 => +full jitter.
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5,
			jitterSource: () => 1.0);

		// attempt 1: 100ms +/- (100*0.5); source=1.0 => 100 + 50 = 150ms (exact, deterministic).
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(150));
	}

	[Fact]
	public void ProduceTheLowerJitterBound_WhenSourceReturnsZero()
	{
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5,
			jitterSource: () => 0.0);

		// source=0.0 => 100 - 50 = 50ms.
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(50));
	}

	[Fact]
	public void ProduceTheUnjitteredDelay_WhenSourceReturnsMidpoint()
	{
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5,
			jitterSource: () => 0.5);

		// source=0.5 => (1-1)*range = 0 => exactly the base exponential delay.
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100));
	}

	[Fact]
	public void BeReproducible_WhenSeededWithTheSameSequence()
	{
		// Two independent calculators seeded identically must yield identical jittered delays — the property
		// that lets a lock assert an exact value despite jitter being enabled.
		var calcA = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5,
			jitterSource: new Random(12345).NextDouble);

		var calcB = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5,
			jitterSource: new Random(12345).NextDouble);

		calcA.CalculateDelay(3).ShouldBe(calcB.CalculateDelay(3));
	}
}
