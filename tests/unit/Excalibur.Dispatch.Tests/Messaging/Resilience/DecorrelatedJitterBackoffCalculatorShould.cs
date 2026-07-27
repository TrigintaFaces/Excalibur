// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Regression lock (bead xh4jru) for <see cref="DecorrelatedJitterBackoffCalculator"/> — AWS
/// "Decorrelated Jitter": <c>delay = min(maxDelay, random(baseDelay, previousDelay · 3))</c>.
/// </summary>
/// <remarks>
/// <para>
/// The lock injects a deterministic <c>jitterSource</c> (values in <c>[0,1)</c>) so the distribution is
/// asserted without RNG/wall-clock flakiness. Two properties distinguish decorrelated jitter from every
/// attempt-derived strategy (Full/Exponential jitter) and make binding this arm to anything else RED:
/// </para>
/// <list type="number">
/// <item><b>Floors at <c>baseDelay</c>, never zero</b> — a jitter draw of <c>0.0</c> yields exactly
/// <c>baseDelay</c> (Full Jitter yields zero). </item>
/// <item><b>Threads the previous ACTUAL delay forward</b> — the attempt-N upper bound is
/// <c>3 · (the attempt-(N-1) sampled delay)</c>, so attempt-N depends on the attempt-(N-1) DRAW, not on
/// the attempt number alone. An attempt-derived calculator produces the same attempt-N distribution
/// regardless of the earlier draw; this one does not. </item>
/// </list>
/// <para>The calculator is <c>internal</c>; reached via <c>InternalsVisibleTo</c>.</para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Resilience")]
public sealed class DecorrelatedJitterBackoffCalculatorShould
{
	private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(100);
	private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(30);
	private const double Growth = 3.0;

	private static DecorrelatedJitterBackoffCalculator Create(Func<double> jitterSource) =>
		new(BaseDelay, MaxDelay, jitterSource);

	/// <summary>A jitter source that dispenses a fixed sequence of draws, one per CalculateDelay call.</summary>
	private static Func<double> Sequence(params double[] draws)
	{
		var queue = new Queue<double>(draws);
		return () => queue.Dequeue();
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void FloorAtBaseDelay_WhenJitterDrawsZero(int attempt)
	{
		// The defining decorrelated-jitter property vs Full Jitter: the lower bound is baseDelay, never 0.
		// With every draw 0.0, each sampled delay collapses to the floor (baseDelay), for any attempt.
		var calculator = Create(() => 0.0);

		TimeSpan delay = default;
		for (var i = 1; i <= attempt; i++)
		{
			delay = calculator.CalculateDelay(i);
		}

		delay.ShouldBe(BaseDelay);
	}

	[Fact]
	public void SampleUpToThreeTimesBaseDelay_OnTheFirstAttempt()
	{
		// attempt 1 resets previous→base, so the window is [base, base·3]. A near-1 draw ≈ base·3.
		var calculator = Create(() => 0.9999);

		var delayMs = calculator.CalculateDelay(1).TotalMilliseconds;

		delayMs.ShouldBeInRange(BaseDelay.TotalMilliseconds * Growth * 0.99, BaseDelay.TotalMilliseconds * Growth);
	}

	[Fact]
	public void ThreadPreviousDelayForward_GrowingGeometricallyUnderConstantHighJitter()
	{
		// Constant near-1 jitter: each attempt's upper bound is 3× the PREVIOUS actual delay, so the
		// sequence grows ≈geometrically (×3) — the decorrelation/threading property.
		var calculator = Create(() => 0.9999);

		var d1 = calculator.CalculateDelay(1).TotalMilliseconds; // ≈ base·3
		var d2 = calculator.CalculateDelay(2).TotalMilliseconds; // ≈ d1·3
		var d3 = calculator.CalculateDelay(3).TotalMilliseconds; // ≈ d2·3

		(d2 / d1).ShouldBeInRange(Growth * 0.98, Growth * 1.02);
		(d3 / d2).ShouldBeInRange(Growth * 0.98, Growth * 1.02);
	}

	[Fact]
	public void MakeAttemptTwoDependOnAttemptOneActualDraw_NotOnTheAttemptNumberAlone()
	{
		// The killer non-vacuity: attempt-2's upper bound = 3 × attempt-1's ACTUAL sampled delay. Feed the
		// SAME attempt-2 draw (near 1) to two instances that drew DIFFERENTLY at attempt 1 → attempt-2
		// delays differ. An attempt-derived calculator would produce identical attempt-2 delays.
		var small = Create(Sequence(0.0, 0.9999));    // attempt1 → base; attempt2 upper = base·3
		var large = Create(Sequence(0.9999, 0.9999)); // attempt1 → base·3; attempt2 upper = base·9

		_ = small.CalculateDelay(1);
		var smallAttempt2 = small.CalculateDelay(2).TotalMilliseconds;

		_ = large.CalculateDelay(1);
		var largeAttempt2 = large.CalculateDelay(2).TotalMilliseconds;

		// ≈ base·3 vs ≈ base·9 → the larger prior draw yields a ~3× larger attempt-2 delay.
		(largeAttempt2 / smallAttempt2).ShouldBeInRange(Growth * 0.95, Growth * 1.05);
	}

	[Fact]
	public void NeverExceedMaxDelay_UnderSustainedHighJitter()
	{
		// A tiny cap with constant near-1 growth must clamp: no attempt may exceed MaxDelay.
		var calculator = new DecorrelatedJitterBackoffCalculator(
			TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(500), () => 0.9999);

		for (var attempt = 1; attempt <= 20; attempt++)
		{
			calculator.CalculateDelay(attempt).TotalMilliseconds
				.ShouldBeLessThanOrEqualTo(500.0);
		}
	}

	[Fact]
	public void NeverFallBelowBaseDelay_AcrossTheJitterRange()
	{
		foreach (var draw in new[] { 0.0, 0.1, 0.5, 0.9, 0.9999 })
		{
			var calculator = Create(() => draw);

			calculator.CalculateDelay(1).TotalMilliseconds
				.ShouldBeGreaterThanOrEqualTo(BaseDelay.TotalMilliseconds);
		}
	}

	[Fact]
	public void ResetThreadedStateAtAttemptOne_SoAReusedInstanceStartsFromBase()
	{
		// Grow the threaded state across a sequence, then start a fresh sequence (attempt 1): the window
		// must reset to [base, base·3] rather than continue from the grown previous delay.
		var calculator = Create(() => 0.9999);

		_ = calculator.CalculateDelay(1);
		_ = calculator.CalculateDelay(2);
		var grownAttempt3 = calculator.CalculateDelay(3).TotalMilliseconds; // large (≈ base·27)

		var resetAttempt1 = calculator.CalculateDelay(1).TotalMilliseconds;  // reset → ≈ base·3

		resetAttempt1.ShouldBeInRange(BaseDelay.TotalMilliseconds * Growth * 0.99, BaseDelay.TotalMilliseconds * Growth);
		resetAttempt1.ShouldBeLessThan(grownAttempt3);
	}

	[Fact]
	public void DecorrelateSamples_UnderRealRandomness_WithinBounds()
	{
		// With the real RNG, repeated draws at attempt 1 must vary (not a constant) and stay within
		// [base, base·3] — the decorrelation guarantee that spreads concurrent clients.
		var calculator = Create(new Random(12345).NextDouble);

		var samples = Enumerable.Range(0, 200)
			.Select(_ => calculator.CalculateDelay(1).TotalMilliseconds)
			.ToList();

		samples.ShouldAllBe(ms =>
			ms >= BaseDelay.TotalMilliseconds && ms <= BaseDelay.TotalMilliseconds * Growth + 0.001);
		samples.Distinct().Count().ShouldBeGreaterThan(1, "decorrelated jitter must vary its delays, not emit a constant");
	}

	[Fact]
	public void Throw_WhenAttemptIsLessThanOne()
	{
		var calculator = Create(() => 0.5);

		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
	}
}
