// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Author≠impl regression lock for the stateless <see cref="ExponentialBackoff.Calculate(int, in BackoffParameters)" />
/// single-source-of-truth introduced by bd-pmzyk9 and consumed by all four transports
/// (AwsSqs / AzureServiceBus / GooglePubSub / Kafka).
/// </summary>
/// <remarks>
/// Asserts the SA/Frontend-pinned contract (msg 18077/18078): for given <see cref="BackoffParameters" /> the delay
/// grows geometrically by <c>Multiplier</c>, caps at <c>MaxDelay</c>, is deterministic when <c>UseJitter == false</c>,
/// and varies strictly within <c>±(delay × JitterFactor)</c> bounds when <c>UseJitter == true</c>. The function is
/// pure and total — it never throws and never returns a negative delay (inputs are clamped, not rejected).
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class ExponentialBackoffShould
{
	private static BackoffParameters Params(
		double baseMs = 100,
		double maxMs = 30_000,
		double multiplier = 2.0,
		bool useJitter = false,
		double jitterFactor = 0.2) =>
		new()
		{
			BaseDelay = TimeSpan.FromMilliseconds(baseMs),
			MaxDelay = TimeSpan.FromMilliseconds(maxMs),
			Multiplier = multiplier,
			UseJitter = useJitter,
			JitterFactor = jitterFactor,
		};

	// ---- Happy path: geometric growth (deterministic, UseJitter = false) ----

	[Fact]
	public void ReturnBaseDelay_OnFirstAttempt()
	{
		var result = ExponentialBackoff.Calculate(1, Params(baseMs: 100, multiplier: 2.0));

		result.TotalMilliseconds.ShouldBe(100, tolerance: 0.0001);
	}

	[Theory]
	[InlineData(1, 100)]   // base * 2^0
	[InlineData(2, 200)]   // base * 2^1
	[InlineData(3, 400)]   // base * 2^2
	[InlineData(4, 800)]   // base * 2^3
	[InlineData(5, 1600)]  // base * 2^4
	public void GrowGeometricallyByMultiplier_PerAttempt(int attempt, double expectedMs)
	{
		var result = ExponentialBackoff.Calculate(attempt, Params(baseMs: 100, maxMs: 1_000_000, multiplier: 2.0));

		result.TotalMilliseconds.ShouldBe(expectedMs, tolerance: 0.0001);
	}

	[Fact]
	public void HonorANonIntegerMultiplier()
	{
		// base 50, multiplier 1.5, attempt 3 => 50 * 1.5^2 = 112.5
		var result = ExponentialBackoff.Calculate(3, Params(baseMs: 50, maxMs: 1_000_000, multiplier: 1.5));

		result.TotalMilliseconds.ShouldBe(112.5, tolerance: 0.0001);
	}

	// ---- Cap at MaxDelay ----

	[Fact]
	public void CapAtMaxDelay_WhenExponentialExceedsIt()
	{
		// base 100, multiplier 2, attempt 20 would be enormous; cap at 5000.
		var result = ExponentialBackoff.Calculate(20, Params(baseMs: 100, maxMs: 5_000, multiplier: 2.0));

		result.TotalMilliseconds.ShouldBe(5_000, tolerance: 0.0001);
	}

	[Fact]
	public void NeverExceedMaxDelay_AcrossManyAttempts()
	{
		var parameters = Params(baseMs: 100, maxMs: 2_500, multiplier: 2.0);

		for (var attempt = 1; attempt <= 50; attempt++)
		{
			ExponentialBackoff.Calculate(attempt, parameters).TotalMilliseconds.ShouldBeLessThanOrEqualTo(2_500);
		}
	}

	// ---- Edge cases: input clamping (pure & total, never throws) ----

	[Theory]
	[InlineData(0)]
	[InlineData(-1)]
	[InlineData(int.MinValue)]
	public void TreatAttemptsBelowOne_AsTheFirstAttempt(int attempt)
	{
		var parameters = Params(baseMs: 100, multiplier: 2.0);

		var clamped = ExponentialBackoff.Calculate(attempt, parameters);
		var first = ExponentialBackoff.Calculate(1, parameters);

		clamped.ShouldBe(first);
		clamped.TotalMilliseconds.ShouldBe(100, tolerance: 0.0001);
	}

	[Fact]
	public void ClampMultiplierUpToOne_SoDelayNeverShrinksBelowBase()
	{
		// Multiplier 0.5 would shrink the delay; the calculator clamps it to 1.0 → constant base delay.
		var parameters = Params(baseMs: 100, maxMs: 1_000_000, multiplier: 0.5);

		ExponentialBackoff.Calculate(1, parameters).TotalMilliseconds.ShouldBe(100, tolerance: 0.0001);
		ExponentialBackoff.Calculate(5, parameters).TotalMilliseconds.ShouldBe(100, tolerance: 0.0001);
	}

	[Fact]
	public void ReturnNonNegativeDelay_ForZeroBaseDelay()
	{
		var result = ExponentialBackoff.Calculate(3, Params(baseMs: 0, multiplier: 2.0));

		result.TotalMilliseconds.ShouldBe(0, tolerance: 0.0001);
		result.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Fact]
	public void NotThrow_OnExtremeInputs()
	{
		// Pure & total: extreme attempt + large multiplier must clamp to MaxDelay, never overflow/throw.
		var parameters = Params(baseMs: 1_000, maxMs: 60_000, multiplier: 10.0);

		var result = Should.NotThrow(() => ExponentialBackoff.Calculate(int.MaxValue, parameters));

		result.TotalMilliseconds.ShouldBe(60_000, tolerance: 0.0001);
	}

	// ---- Determinism (UseJitter = false) ----

	[Fact]
	public void BeDeterministic_WhenJitterDisabled()
	{
		var parameters = Params(baseMs: 100, multiplier: 2.0, useJitter: false);

		var first = ExponentialBackoff.Calculate(4, parameters);

		for (var i = 0; i < 100; i++)
		{
			ExponentialBackoff.Calculate(4, parameters).ShouldBe(first);
		}
	}

	[Fact]
	public void IgnoreJitterFactor_WhenJitterDisabled()
	{
		// Same schedule, jitter off, but a large JitterFactor — must have zero effect.
		var noJitter = Params(baseMs: 100, multiplier: 2.0, useJitter: false, jitterFactor: 0.0);
		var withFactorButDisabled = Params(baseMs: 100, multiplier: 2.0, useJitter: false, jitterFactor: 0.9);

		ExponentialBackoff.Calculate(3, withFactorButDisabled).ShouldBe(ExponentialBackoff.Calculate(3, noJitter));
	}

	// ---- Jitter (UseJitter = true): bounded + varies ----

	[Fact]
	public void KeepJitteredDelayWithinSymmetricBounds()
	{
		// attempt 3, base 100, multiplier 2 => nominal 400ms; jitter ±20% => [320, 480], capped at MaxDelay.
		var parameters = Params(baseMs: 100, maxMs: 1_000_000, multiplier: 2.0, useJitter: true, jitterFactor: 0.2);
		const double nominal = 400;
		const double lower = nominal * 0.8;
		const double upper = nominal * 1.2;

		for (var i = 0; i < 1_000; i++)
		{
			var ms = ExponentialBackoff.Calculate(3, parameters).TotalMilliseconds;

			ms.ShouldBeGreaterThanOrEqualTo(lower - 0.0001);
			ms.ShouldBeLessThanOrEqualTo(upper + 0.0001);
		}
	}

	[Fact]
	public void NeverExceedMaxDelay_EvenWithPositiveJitter()
	{
		// Nominal delay sits exactly at the cap; positive jitter must still be clamped to MaxDelay.
		var parameters = Params(baseMs: 5_000, maxMs: 5_000, multiplier: 2.0, useJitter: true, jitterFactor: 0.5);

		for (var i = 0; i < 1_000; i++)
		{
			ExponentialBackoff.Calculate(2, parameters).TotalMilliseconds.ShouldBeLessThanOrEqualTo(5_000 + 0.0001);
		}
	}

	[Fact]
	public void NeverReturnNegativeDelay_WithAggressiveJitter()
	{
		var parameters = Params(baseMs: 100, maxMs: 1_000_000, multiplier: 2.0, useJitter: true, jitterFactor: 1.0);

		for (var i = 0; i < 1_000; i++)
		{
			ExponentialBackoff.Calculate(2, parameters).ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
		}
	}

	[Fact]
	public void ProduceVaryingDelays_WhenJitterEnabled()
	{
		var parameters = Params(baseMs: 1_000, maxMs: 1_000_000, multiplier: 2.0, useJitter: true, jitterFactor: 0.5);

		var observed = new HashSet<double>();
		for (var i = 0; i < 200; i++)
		{
			_ = observed.Add(ExponentialBackoff.Calculate(3, parameters).TotalMilliseconds);
		}

		// A non-degenerate random jitter must yield many distinct values, not a single constant.
		observed.Count.ShouldBeGreaterThan(10);
	}
}
