// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Regression lock (bead ufbn29) for <see cref="FullJitterBackoffCalculator"/> — the AWS "Full Jitter"
/// strategy: <c>delay = random(0, min(maxDelay, baseDelay·multiplier^(attempt-1)))</c>.
/// </summary>
/// <remarks>
/// <para>
/// Full Jitter samples the WHOLE <c>[0, ceiling]</c> window, unlike <see cref="ExponentialBackoffCalculator"/>'s
/// symmetric <c>±(delay × jitterFactor)</c> band. The lock injects a deterministic <c>jitterSource</c> (the
/// calculator is a pure function of <c>attempt</c> + the source) so the distribution is asserted without
/// wall-clock or RNG flakiness.
/// </para>
/// <para>
/// <b>Non-vacuity:</b> the distinguishing property is that a jitter draw of <c>0.0</c> yields <b>exactly zero</b>
/// delay and a draw approaching <c>1.0</c> yields ≈ceiling — i.e. the full <c>[0, ceiling]</c> span. A symmetric
/// ±jitter calculator (or a constant/no-jitter fallback) cannot produce a zero delay at attempt&gt;1, so binding
/// the FullJitter arm to anything else is RED. The calculator is <c>internal</c>; reached via
/// <c>InternalsVisibleTo</c>.
/// </para>
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
[Trait("Feature", "Resilience")]
public sealed class FullJitterBackoffCalculatorShould
{
	private static readonly TimeSpan BaseDelay = TimeSpan.FromMilliseconds(100);
	private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);
	private const double Multiplier = 2.0;

	private static FullJitterBackoffCalculator Create(Func<double> jitterSource) =>
		new(BaseDelay, MaxDelay, Multiplier, jitterSource);

	// ceiling(attempt) = base · multiplier^(attempt-1), capped at MaxDelay.
	private static double CeilingMs(int attempt) =>
		Math.Min(BaseDelay.TotalMilliseconds * Math.Pow(Multiplier, attempt - 1), MaxDelay.TotalMilliseconds);

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	[InlineData(5)]
	public void ProduceZeroDelay_WhenJitterDrawsZero(int attempt)
	{
		// The defining Full-Jitter property: the lower bound of the window is a real zero — a symmetric
		// ±jitter band (ExponentialWithJitter) or a constant fallback can never reach 0 at attempt > 1.
		var calculator = Create(() => 0.0);

		calculator.CalculateDelay(attempt).ShouldBe(TimeSpan.Zero);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(3)]
	public void ApproachTheExponentialCeiling_WhenJitterDrawsNearOne(int attempt)
	{
		var calculator = Create(() => 0.9999);

		var delayMs = calculator.CalculateDelay(attempt).TotalMilliseconds;
		var ceiling = CeilingMs(attempt);

		// ≈ceiling from below (0.9999 · ceiling), strictly within (0.99·ceiling, ceiling].
		delayMs.ShouldBeInRange(ceiling * 0.99, ceiling);
	}

	[Theory]
	[InlineData(1)]
	[InlineData(2)]
	[InlineData(4)]
	[InlineData(8)]
	public void StayWithinZeroToCeiling_AcrossTheJitterRange(int attempt)
	{
		// Drive a deterministic spread of jitter draws in [0,1) and assert every delay is in [0, ceiling].
		var draws = new[] { 0.0, 0.1, 0.25, 0.5, 0.75, 0.9, 0.99 };
		var ceiling = CeilingMs(attempt);

		foreach (var draw in draws)
		{
			var calculator = Create(() => draw);
			var delayMs = calculator.CalculateDelay(attempt).TotalMilliseconds;

			delayMs.ShouldBeInRange(0.0, ceiling);
			delayMs.ShouldBe(draw * ceiling, 0.0001);
		}
	}

	[Fact]
	public void CapTheCeilingAtMaxDelay_ForLargeAttempts()
	{
		// attempt 20 → base·2^19 ≫ MaxDelay, so the ceiling is MaxDelay; a near-1 draw ≈ MaxDelay.
		var calculator = Create(() => 0.9999);

		var delayMs = calculator.CalculateDelay(20).TotalMilliseconds;

		delayMs.ShouldBeInRange(MaxDelay.TotalMilliseconds * 0.99, MaxDelay.TotalMilliseconds);
	}

	[Fact]
	public void Throw_WhenAttemptIsLessThanOne()
	{
		var calculator = Create(() => 0.5);

		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
	}
}
