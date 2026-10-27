// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Resilience;

/// <summary>
/// Author≠impl regression lock for the stateless <see cref="ExponentialBackoff.Calculate(int, in BackoffParameters)" />
/// single-source-of-truth introduced by bd-pmzyk9 and consumed by all four transports
/// (AwsSqs / AzureServiceBus / GooglePubSub / Kafka).
/// </summary>
/// <remarks>
/// Asserts the contract: for given <see cref="BackoffParameters" /> the delay grows geometrically by
/// <c>Multiplier</c>, caps at <c>MaxDelay</c>, and is ALWAYS deterministic. The function is pure and total —
/// same inputs, same output, it never throws and never returns a negative delay (inputs are clamped, not
/// rejected).
/// </remarks>
/// <remarks>
/// <para>
/// <b>Every jitter arm that used to live here has been deleted, and the deletions are the point.</b> This
/// function no longer randomises at all. The arms that went, and why none of them could simply be relaxed:
/// </para>
/// <list type="bullet">
/// <item><c>KeepJitteredDelayWithinSymmetricBounds</c> asserted <c>[320, 480]</c> for a nominal of 400 — it
/// encoded the symmetric band as the REQUIREMENT, so correct behaviour failed it. It could not be widened
/// into correctness; the widened version is <c>NeverExceedMaxDelay_AcrossManyAttempts</c>, already here.</item>
/// <item><c>NeverExceedMaxDelay_EvenWithPositiveJitter</c> set base == max == 5000 and asserted only
/// <c>&lt;= 5000</c> — which a constant 5000 satisfies perfectly. It certified the clip-after-jitter
/// degeneracy it looked like it was guarding against.</item>
/// <item><c>IgnoreJitterFactor_WhenJitterDisabled</c> built both of its operands with identical arguments
/// and neither passed a jitter factor, so it asserted <c>f(x) == f(x)</c> and could never fail.</item>
/// <item><c>ProduceVaryingDelays_WhenJitterEnabled</c> required more than ten distinct values, which the
/// band satisfied too, so it never discriminated between the band and anything else.</item>
/// <item><c>NeverReturnNegativeDelay_WithAggressiveJitter</c> is subsumed by
/// <c>ReturnNonNegativeDelay_ForZeroBaseDelay</c> and the unconditional determinism arm.</item>
/// </list>
/// <para>
/// Jitter is now the calculator's job and is bound by the conformance suite over
/// <c>IBackoffCalculator</c>, which asserts the property that actually matters — that the delay reaches the
/// LOW end of its interval — rather than only an upper bound a no-op would satisfy.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Resilience)]
public sealed class ExponentialBackoffShould
{
	private static BackoffParameters Params(
		double baseMs = 100,
		double maxMs = 30_000,
		double multiplier = 2.0) =>
		new()
		{
			BaseDelay = TimeSpan.FromMilliseconds(baseMs),
			MaxDelay = TimeSpan.FromMilliseconds(maxMs),
			Multiplier = multiplier,
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

	// ---- Determinism: now UNCONDITIONAL ----

	[Fact]
	public void BeDeterministic_Always()
	{
		// STRENGTHENED, not merely kept. This used to be BeDeterministic_WhenJitterDisabled and was
		// conditional on a UseJitter flag that no longer exists -- so a caller had to know which flag
		// state they were in before they could rely on the result. Determinism is now a property of the
		// function, which is what lets its documentation claim purity honestly: the method called
		// Random.Shared while the docs said "pure and total", and a caller who believed the docs and
		// cached a delay got a value the next call would not reproduce.
		var parameters = Params(baseMs: 100, multiplier: 2.0);

		var first = ExponentialBackoff.Calculate(4, parameters);

		for (var i = 0; i < 100; i++)
		{
			ExponentialBackoff.Calculate(4, parameters).ShouldBe(first);
		}
	}

	[Fact]
	public void BeDeterministic_EvenWhereTheNominalLadderExceedsTheCap()
	{
		// The regime the deleted NeverExceedMaxDelay_EvenWithPositiveJitter was blind to. It set
		// base == max and asserted only "<= max", which a constant satisfies -- so it passed while the
		// band it was guarding had collapsed to exactly max with probability one. Here the constant IS
		// the correct answer, because this function does not randomise; the arm that proves randomisation
		// survives the cap lives on the calculator, where the randomisation now is.
		var parameters = Params(baseMs: 250, maxMs: 5_000, multiplier: 2.0);

		for (var attempt = 6; attempt <= 12; attempt++)
		{
			ExponentialBackoff.Calculate(attempt, parameters).TotalMilliseconds.ShouldBe(5_000, tolerance: 0.0001);
		}
	}
}
