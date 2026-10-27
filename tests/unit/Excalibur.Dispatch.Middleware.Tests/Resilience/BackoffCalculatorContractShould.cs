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

	/// <summary>Gets the rows that randomise, for the clauses that only bind a randomising implementation.</summary>
	/// <value>The theory rows.</value>
	public static TheoryData<string> JitteredImplementations => new("jittered", "polly-jittered");

	/// <summary>
	/// Builds the named implementation.
	/// </summary>
	/// <param name="name">The implementation row.</param>
	/// <param name="baseDelay">
	/// The base delay, defaulting to <see cref="Base" />. Taking it as a parameter is what lets the
	/// zero-base-delay clause run against EVERY row. It previously reconstructed one of only two
	/// calculators from a <c>StartsWith("polly")</c> test, so the <c>fixed</c>, <c>linear</c> and
	/// <c>jittered</c> rows all silently exercised <see cref="ExponentialBackoffCalculator" /> and the
	/// precondition was never checked on <see cref="FixedBackoffCalculator" /> or
	/// <see cref="LinearBackoffCalculator" /> at all. Three of eight rows reported PASS having tested a
	/// different implementation than the one they name -- subject substitution inside the conformance
	/// suite written to prevent exactly that.
	/// </param>
	/// <returns>The calculator.</returns>
	private static IBackoffCalculator Create(string name, TimeSpan? baseDelay = null)
	{
		var b = baseDelay ?? Base;

		return name switch
		{
			"fixed" => new FixedBackoffCalculator(b),
			"linear" => new LinearBackoffCalculator(b, Max),
			"exponential" => new ExponentialBackoffCalculator(b, Max),
			"jittered" => new JitteredBackoffCalculator(new ExponentialBackoffCalculator(b, Max)),
			"polly-constant" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Constant, b, Max, false),
			"polly-linear" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Linear, b, Max, false),
			"polly-exponential" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Exponential, b, Max, false),
			"polly-jittered" => new PollyBackoffCalculatorAdapter(DelayBackoffType.Exponential, b, Max, true),
			_ => throw new ArgumentOutOfRangeException(nameof(name), name, "unknown implementation"),
		};
	}

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
		// startup with it. Now built through Create, so the row name selects the implementation actually
		// under test -- see the remarks on Create for what this arm was measuring before.
		var calculator = Create(implementation, TimeSpan.Zero);

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

	[Theory]
	[MemberData(nameof(Implementations))]
	public void BoundEveryDelayByTheConfiguredMaximum(string implementation)
	{
		// REKEYED FROM nominal TO Max, and promoted from a Fact over one implementation to a Theory over
		// all of them. The old clause required the delay to lie within [0, nominal]. That bound is
		// UNOBSERVABLE through this interface -- a caller holds an IBackoffCalculator and has no operation
		// that yields the nominal for an attempt -- so the arm could only be written by reaching past the
		// contract to the concrete inner calculator, which is why it bound exactly one row and said so in
		// its own comment. Max is the only ceiling a caller can actually check.
		var calculator = Create(implementation);

		for (var attempt = 1; attempt <= 8; attempt++)
		{
			for (var sample = 0; sample < 50; sample++)
			{
				var delay = calculator.CalculateDelay(attempt);

				delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero, $"{implementation} returned a negative delay");
				delay.ShouldBeLessThanOrEqualTo(Max, $"{implementation} exceeded the configured maximum");
			}
		}
	}

	[Theory]
	[MemberData(nameof(JitteredImplementations))]
	public void SampleTheLowerHalfOfTheInterval(string implementation)
	{
		// THE LIVENESS HALF, now binding EVERY randomising implementation rather than just the in-box
		// decorator. Every bound assertion above is an upper bound, which an implementation that returned
		// the nominal unchanged -- i.e. applied no jitter at all -- satisfies completely. Reaching the low
		// end is the property jitter exists for, because sampling near zero is what decorrelates clients
		// that failed together. A symmetric band around the nominal fails this arm, which is the intent:
		// the contract says a band is not a valid implementation of this interface.
		var calculator = Create(implementation);
		var half = TimeSpan.FromTicks(Max.Ticks / 2);

		var sawLowerHalf = false;
		for (var i = 0; i < 400 && !sawLowerHalf; i++)
		{
			sawLowerHalf = calculator.CalculateDelay(8) < half;
		}

		sawLowerHalf.ShouldBeTrue(
			$"{implementation} never sampled below half the configured maximum over 400 draws at attempt 8; "
			+ "a band around the nominal, or no jitter at all, never does");
	}

	[Fact]
	public void KeepJitterAliveOnceTheNominalLadderPassesTheCap()
	{
		// THE RED-DETECTING ARM FOR A LIVE PRODUCTION DEFECT, and the reason the cap must be applied
		// BEFORE the randomisation rather than after it.
		//
		// These are Kafka's own shipped pacing values. The nominal ladder is 250, 500, 1000, 2000, 4000,
		// 8000 ... so it passes the 5s ceiling at attempt 6. Under the previous randomise-then-cap
		// ordering every sampled value from attempt 6 onward was already above the cap -- the band at
		// attempt 6 was [6400, 9600] -- so min(sample, 5000) returned EXACTLY 5000 with probability one.
		// Every partition, in every instance, in every pod, paced in lockstep, with the jitter gone
		// precisely during the sustained outage it exists to spread.
		//
		// The old suite could not catch this: its arm for the regime set base == max and asserted only
		// "<= max", which a constant satisfies perfectly.
		var calculator = BackoffCalculatorFactory.Create(
			BackoffStrategy.Exponential,
			new BackoffParameters
			{
				BaseDelay = TimeSpan.FromMilliseconds(250),
				MaxDelay = TimeSpan.FromSeconds(5),
				Multiplier = 2.0,
			},
			useJitter: true);

		var observed = new HashSet<double>();
		for (var i = 0; i < 200; i++)
		{
			observed.Add(calculator.CalculateDelay(8).TotalMilliseconds);
		}

		observed.Count.ShouldBeGreaterThan(
			10,
			"at an attempt whose nominal delay is far past the cap, a cap-then-jitter calculator still "
			+ "varies; a jitter-then-cap one returns the cap every single time");
		observed.Min().ShouldBeLessThan(
			2_500,
			"jitter must still reach the low end of the capped interval, not cluster at the ceiling");
	}

	[Fact]
	public void ExposeAPublicRouteToEveryStrategy()
	{
		// IBackoffCalculator has always been public while every implementation of it is internal, so until
		// the factory was made public a consumer could name the contract and never obtain one. That gap is
		// what pushed callers onto the pure ExponentialBackoff.Calculate and grew a jitter flag on
		// BackoffParameters -- the divergence this suite exists to prevent, caused by a missing member.
		foreach (var strategy in Enum.GetValues<BackoffStrategy>())
		{
			var parameters = new BackoffParameters { BaseDelay = Base, MaxDelay = Max, Multiplier = 2.0 };

			foreach (var useJitter in new[] { false, true })
			{
				var calculator = BackoffCalculatorFactory.Create(strategy, in parameters, useJitter);

				calculator.ShouldNotBeNull($"{strategy} with useJitter={useJitter} must be constructible");
				calculator.CalculateDelay(1).ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
				calculator.CalculateDelay(1).ShouldBeLessThanOrEqualTo(Max);
			}
		}
	}
}
