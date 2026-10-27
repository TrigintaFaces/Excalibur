// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class ExponentialBackoffCalculatorShould
{
	[Fact]
	public void DefaultConstructor_CreateWithDefaultOptions()
	{
		// Act
		var calculator = new ExponentialBackoffCalculator();

		// Assert — first attempt should use base delay (1s default)
		var delay = calculator.CalculateDelay(1);
		delay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void OptionsConstructor_UseOptionsValues()
	{
		// Arrange
		var options = new RetryOptions
		{
			BaseDelay = TimeSpan.FromMilliseconds(500),
			MaxDelay = TimeSpan.FromSeconds(10),
			BackoffMultiplier = 3.0,
			UseJitter = false,
		};

		// Act
		var calculator = new ExponentialBackoffCalculator(options);
		var delay = calculator.CalculateDelay(1);

		// Assert — first attempt, no jitter, should be exactly base delay
		delay.ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void CalculateDelay_IncreaseExponentially()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0);

		// Act
		var delay1 = calculator.CalculateDelay(1);
		var delay2 = calculator.CalculateDelay(2);
		var delay3 = calculator.CalculateDelay(3);

		// Assert — 100, 200, 400 ms
		delay1.ShouldBe(TimeSpan.FromMilliseconds(100));
		delay2.ShouldBe(TimeSpan.FromMilliseconds(200));
		delay3.ShouldBe(TimeSpan.FromMilliseconds(400));
	}

	[Fact]
	public void CalculateDelay_ClampToMaxDelay()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromSeconds(5),
			multiplier: 2.0);

		// Act — attempt 10 would be 1s * 2^9 = 512s, should clamp
		var delay = calculator.CalculateDelay(10);

		// Assert
		delay.ShouldBe(TimeSpan.FromSeconds(5));
	}


	[Fact]
	public void CalculateDelay_ThrowOnZeroAttempt()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
	}

	[Fact]
	public void CalculateDelay_ThrowOnNegativeAttempt()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator();

		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(-1));
	}

	[Fact]
	public void Constructor_AcceptZeroBaseDelay()
	{
		// Zero means "retry immediately" -- a legal configuration, not an error.
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.Zero,
			maxDelay: TimeSpan.FromSeconds(10));

		calculator.CalculateDelay(1).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Constructor_ThrowOnNegativeBaseDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromMilliseconds(-1),
				maxDelay: TimeSpan.FromSeconds(10)));
	}

	[Fact]
	public void Constructor_ClampToZeroWhenMaxDelayIsZero()
	{
		// Zero means "retry immediately" -- a legal configuration, not an error.
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.Zero);

		calculator.CalculateDelay(1).ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void Constructor_ThrowOnMultiplierLessThanOne()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromSeconds(1),
				maxDelay: TimeSpan.FromSeconds(10),
				multiplier: 0.5));
	}




	[Fact]
	public void CreateForMessageQueue_AppliesFullJitterBelowTheNominalDelay()
	{
		// The assertion CHANGED with the behaviour, rather than being relaxed to pass. This used to expect
		// 750-1250ms: a symmetric +/-25% band around the nominal second, which could exceed the nominal
		// delay. Jitter is now full jitter -- uniform over [0, nominal) -- so the delay is at most the
		// nominal second and may be far below it, which is what actually decorrelates competing consumers.
		// Sampling the low end is the property, so a lower bound above zero would be wrong here.
		var calculator = ExponentialBackoffCalculator.CreateForMessageQueue();

		for (var i = 0; i < 100; i++)
		{
			var delay = calculator.CalculateDelay(1);

			delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
			delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(1), "full jitter never exceeds the nominal delay");
		}
	}


	[Fact]
	public void ImplementIBackoffCalculator()
	{
		// Assert
		var calculator = new ExponentialBackoffCalculator();
		calculator.ShouldBeAssignableTo<IBackoffCalculator>();
	}

	[Fact]
	public void CalculateDelay_WithMultiplierOne_ReturnConstantDelay()
	{
		// Arrange — multiplier of 1.0 means no exponential growth
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(200),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 1.0);

		// Act
		var delay1 = calculator.CalculateDelay(1);
		var delay2 = calculator.CalculateDelay(5);
		var delay3 = calculator.CalculateDelay(10);

		// Assert — all should be the same since 1^n = 1
		delay1.ShouldBe(TimeSpan.FromMilliseconds(200));
		delay2.ShouldBe(TimeSpan.FromMilliseconds(200));
		delay3.ShouldBe(TimeSpan.FromMilliseconds(200));
	}

	[Fact]
	public void ComputeADelayWithoutAllocating()
	{
		// WHAT THIS REPLACES, and the replacement is in a different file on purpose. RetryPolicyShould
		// carried an arm asserting 1,000 CalculateDelay calls finished inside 25ms. Two things were wrong
		// with it: a wall clock in a unit suite measures the runner as much as the code, and -- far worse
		// -- that file declares its OWN private ExponentialBackoffCalculator stub with a different
		// algorithm, so the arm exercised no production code at all. It could not be moved to a benchmark
		// either: this type is internal, the benchmarks assembly has no internals access, and widening
		// visibility to suit a benchmark is not allowed.
		//
		// So the claim is made falsifiable instead of fast, against the REAL type. CalculateDelay returns
		// a TimeSpan and does arithmetic; it has no reason to touch the heap. Attempts start at 1 --
		// the real type rejects 0, which the stub this replaced happily accepted. Byte counts are exact
		// integers from the runtime's own accounting, identical on every machine and in every build
		// configuration, so this cannot flake -- and it catches what actually threatens this method: a
		// Random allocated per call for jitter, a boxed value, an array in a delay table.
		var calculator = new ExponentialBackoffCalculator();

		for (var i = 0; i < 2_000; i++)
		{
			_ = calculator.CalculateDelay((i % 10) + 1);
		}

		// The minimum over several windows: interference is one-sided, since a tiering recompilation or a
		// finalizer landing on this thread can only ADD bytes.
		var best = long.MaxValue;
		for (var window = 0; window < 5; window++)
		{
			var before = GC.GetAllocatedBytesForCurrentThread();
			for (var i = 0; i < 1_000; i++)
			{
				_ = calculator.CalculateDelay((i % 10) + 1);
			}

			best = Math.Min(best, GC.GetAllocatedBytesForCurrentThread() - before);
		}

		best.ShouldBe(
			0,
			$"CalculateDelay allocated {best} bytes over 1,000 calls. Backoff is computed on every retry "
			+ "of every failed message, so anything here is per-retry GC pressure");
	}
}
