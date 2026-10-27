// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Resilience.Polly;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Binds the <see cref="IBackoffCalculator" /> contract against EVERY implementation, in-box and Polly.
/// </summary>
/// <remarks>
/// <para>
/// <b>This suite exists because per-implementation tests cannot catch divergence.</b> Each calculator had
/// its own test file and each one passed, while the Polly adapter threw where the in-box calculators
/// returned a value and refused configuration the in-box calculators accepted. Nothing compared them, so
/// nothing failed. A suite that runs the same assertions over every implementation is the only shape that
/// can detect two valid-looking answers to one question.
/// </para>
/// <para>
/// <b>Add a row when you add an implementation.</b> The theory data is the list of implementations; an
/// implementation absent from it is unbound by this contract, which is the state that produced the
/// divergence in the first place.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Resilience")]
public sealed class BackoffCalculatorContractShould
{
	private static readonly TimeSpan Base = TimeSpan.FromMilliseconds(100);
	private static readonly TimeSpan Max = TimeSpan.FromSeconds(5);

	/// <summary>Gets every implementation of the contract, by name.</summary>
	/// <value>The theory rows.</value>
	public static TheoryData<string> Implementations =>
		new("fixed", "linear", "exponential", "jittered", "polly-constant", "polly-linear", "polly-exponential", "polly-jittered");

	private static IBackoffCalculator Create(string name) => name switch
	{
		"fixed" => new FixedBackoffCalculator(Base),
		"linear" => new LinearBackoffCalculator(Base, Max),
		"exponential" => new ExponentialBackoffCalculator(Base, Max),
		"jittered" => new JitteredBackoffCalculator(new ExponentialBackoffCalculator(Base, Max)),
		"polly-constant" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Constant, Base, Max, false),
		"polly-linear" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Linear, Base, Max, false),
		"polly-exponential" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Exponential, Base, Max, false),
		"polly-jittered" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Exponential, Base, Max, true),
		_ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown implementation"),
	};

	[Theory]
	[MemberData(nameof(Implementations))]
	public void RefuseANonPositiveAttempt(string implementation)
	{
		// THE CLAUSE THAT WAS VIOLATED. The Polly adapter returned TimeSpan.Zero here while every in-box
		// calculator threw, for the same interface -- so a caller's off-by-one became an immediate retry
		// with no backoff through one implementation and an exception through the others.
		var calculator = Create(implementation);

		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(-1));
	}

	[Theory]
	[MemberData(nameof(Implementations))]
	public void ReturnANonNegativeDelayWithinTheConfiguredMaximum(string implementation)
	{
		// Covers the ordinary range and the overflow tail in one sweep. Attempt 1000 is far past where a
		// geometric ladder exceeds any cap, so this is where an unguarded accumulator would wrap negative
		// -- which is how a delay reached Task.Delay as a negative TimeSpan and threw there instead.
		var calculator = Create(implementation);

		foreach (var attempt in new[] { 1, 2, 3, 10, 64, 100, 1000, int.MaxValue })
		{
			var delay = calculator.CalculateDelay(attempt);

			delay.ShouldBeGreaterThanOrEqualTo(
				TimeSpan.Zero, $"{implementation} returned a negative delay for attempt {attempt}");
			delay.ShouldBeLessThanOrEqualTo(
				Max, $"{implementation} exceeded the configured maximum for attempt {attempt}");
		}
	}

	[Theory]
	[MemberData(nameof(Implementations))]
	public void AcceptAZeroBaseDelay(string implementation)
	{
		// The precondition the Polly adapter used to strengthen: it rejected a zero base delay that no
		// in-box calculator rejects, so a configuration that started fine without the package threw at
		// startup with it.
		IBackoffCalculator calculator = implementation.StartsWith("polly", StringComparison.Ordinal)
			? new PollyBackoffCalculatorAdapter(DelayBackoffType.Exponential, TimeSpan.Zero, Max, false)
			: new ExponentialBackoffCalculator(TimeSpan.Zero, Max);

		calculator.CalculateDelay(1).ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	[Theory]
	[MemberData(nameof(Implementations))]
	public void BeStatelessAcrossCalls(string implementation)
	{
		// A calculator whose output depends on its own history cannot be shared across messages -- one
		// message's first retry would be computed from another's last delay -- and cannot survive the
		// restart a durable retry path assumes. For a jittered implementation the VALUE varies by design,
		// so what is asserted is that the bound still holds however many times it is called, rather than
		// that the value repeats.
		var calculator = Create(implementation);
		var jittered = implementation is "jittered" or "polly-jittered";

		var first = calculator.CalculateDelay(3);
		_ = calculator.CalculateDelay(50);
		var again = calculator.CalculateDelay(3);

		if (jittered)
		{
			again.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
			again.ShouldBeLessThanOrEqualTo(Max);
		}
		else
		{
			again.ShouldBe(first, $"{implementation} is not stateless: attempt 3 changed after an intervening call");
		}
	}

	[Fact]
	public void BoundJitterWithinTheNominalDelay()
	{
		// The jitter clause: within [0, nominal], distribution implementation-defined. Asserted against
		// the in-box decorator, where the nominal is directly available from the inner calculator -- the
		// Polly adapter computes its own nominal internally and is covered by the maximum bound above.
		var nominalCalculator = new ExponentialBackoffCalculator(Base, Max);
		var jittered = new JitteredBackoffCalculator(nominalCalculator);

		for (var attempt = 1; attempt <= 8; attempt++)
		{
			var nominal = nominalCalculator.CalculateDelay(attempt);

			for (var sample = 0; sample < 50; sample++)
			{
				var delay = jittered.CalculateDelay(attempt);

				delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
				delay.ShouldBeLessThanOrEqualTo(nominal, "jitter must not exceed the nominal delay");
			}
		}
	}

	[Fact]
	public void SampleTheLowerHalfOfTheJitterInterval()
	{
		// THE LIVENESS HALF. Every jitter assertion above is an upper bound, which a calculator that
		// returned the nominal delay unchanged -- i.e. applied no jitter at all -- would satisfy
		// completely. Full jitter must reach the low end, because sampling near zero is the property that
		// decorrelates clients which failed together. A half-jitter implementation, which is what this
		// framework shipped, fails this arm.
		var jittered = new JitteredBackoffCalculator(new ExponentialBackoffCalculator(Base, Max));
		var nominal = new ExponentialBackoffCalculator(Base, Max).CalculateDelay(5);

		var sawLowerHalf = false;
		for (var i = 0; i < 200 && !sawLowerHalf; i++)
		{
			sawLowerHalf = jittered.CalculateDelay(5) < (nominal / 2);
		}

		sawLowerHalf.ShouldBeTrue(
			"over 200 samples full jitter must land below half the nominal delay at least once; a band "
			+ "around the nominal, or no jitter at all, never does");
	}
}
