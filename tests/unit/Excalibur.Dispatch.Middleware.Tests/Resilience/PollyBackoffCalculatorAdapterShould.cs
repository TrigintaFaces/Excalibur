// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.1 OR AGPL-3.0-or-later OR SSPL-1.0

using Excalibur.Dispatch.Resilience.Polly;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Tests for the <see cref="PollyBackoffCalculatorAdapter"/> class.
/// Sprint 45 (bd-5tsb): Unit tests for Polly backoff calculator adapter.
/// </summary>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Dispatch.Core")]
public sealed class PollyBackoffCalculatorAdapterShould
{
	#region Constructor Tests

	[Fact]
	public void CreateWithDefaultValues()
	{
		// Act
		var calculator = new PollyBackoffCalculatorAdapter();

		// Assert - should not throw
		_ = calculator.ShouldNotBeNull();
	}

	[Fact]
	public void AcceptAZeroBaseDelayAsTheInBoxCalculatorsDo()
	{
		// INVERTED DELIBERATELY. This arm used to assert that a zero base delay THROWS. No in-box
		// calculator rejects zero -- they reject only a negative -- so the adapter was strengthening a
		// precondition, and a configuration that started fine without the Polly package threw at startup
		// with it. Strengthening a precondition breaks substitutability, which is the purpose of the seam.
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential, TimeSpan.Zero, TimeSpan.FromSeconds(30));

		calculator.CalculateDelay(1).ShouldBe(TimeSpan.Zero, "a zero base delay yields a zero delay, not an exception");
	}

	[Fact]
	public void ThrowArgumentOutOfRangeForNegativeBaseDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PollyBackoffCalculatorAdapter(
				DelayBackoffType.Exponential,
				TimeSpan.FromMilliseconds(-100),
				TimeSpan.FromSeconds(30)));
	}

	[Fact]
	public void AcceptAMaxDelayBelowTheBaseDelayAsTheInBoxCalculatorsDo()
	{
		// Also inverted. No in-box calculator compares maxDelay against baseDelay, so refusing the
		// combination here was a precondition only this implementation had. The cap simply wins, which is
		// what the contract promises: the result never exceeds the configured maximum.
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential, TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5), useJitter: false);

		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(5), "the maximum caps a larger base delay");
	}

	[Fact]
	public void ThrowArgumentOutOfRangeForZeroFactor()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PollyBackoffCalculatorAdapter(
				DelayBackoffType.Exponential,
				TimeSpan.FromMilliseconds(100),
				TimeSpan.FromSeconds(30),
				true,
				0));
	}

	[Fact]
	public void ThrowArgumentOutOfRangeForNegativeFactor()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PollyBackoffCalculatorAdapter(
				DelayBackoffType.Exponential,
				TimeSpan.FromMilliseconds(100),
				TimeSpan.FromSeconds(30),
				true,
				-1));
	}

	[Fact]
	public void AllowMaxDelayEqualToBaseDelay()
	{
		// Act - should not throw
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Constant,
			TimeSpan.FromSeconds(1),
			TimeSpan.FromSeconds(1));

		// Assert
		_ = calculator.ShouldNotBeNull();
	}

	#endregion Constructor Tests

	#region CreateDecorrelatedJitter Factory Tests

	[Fact]
	public void CreateDecorrelatedJitterWithValidParameters()
	{
		// Act
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30));

		// Assert
		_ = calculator.ShouldNotBeNull();
	}

	[Fact]
	public void DecorrelatedJitterProducePositiveDelays()
	{
		// Arrange
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30));

		// Act & Assert
		for (var attempt = 1; attempt <= 10; attempt++)
		{
			var delay = calculator.CalculateDelay(attempt);
			delay.ShouldBeGreaterThan(TimeSpan.Zero);
		}
	}

	#endregion CreateDecorrelatedJitter Factory Tests

	#region Constant Backoff Tests

	[Fact]
	public void ReturnConstantDelayForAllAttempts()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(500);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Constant,
			baseDelay,
			TimeSpan.FromSeconds(30),
			false);

		// Act & Assert
		for (var attempt = 1; attempt <= 5; attempt++)
		{
			var delay = calculator.CalculateDelay(attempt);
			delay.ShouldBe(baseDelay);
		}
	}

	[Fact]
	public void ApplyJitterToConstantBackoff()
	{
		// Arrange
		var baseDelay = TimeSpan.FromSeconds(1);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Constant,
			baseDelay,
			TimeSpan.FromSeconds(30),
			true);

		var delays = new List<TimeSpan>();

		// Act
		for (var i = 0; i < 100; i++)
		{
			delays.Add(calculator.CalculateDelay(1));
		}

		// FULL jitter: uniform over [0, nominal). The old lower bound of 500ms encoded half jitter --
		// [0.5 x delay, delay] -- which the implementation's own comment mislabelled "full jitter (AWS
		// recommended)". It is not: AWS full jitter is uniform over [0, delay], and a band that can never
		// sample the low end does not decorrelate clients that failed together. Asserting a lower bound
		// above zero here would re-pin the defect.
		delays.All(d => d >= TimeSpan.Zero && d <= baseDelay).ShouldBeTrue("full jitter stays within [0, nominal]");
		delays.Any(d => d < TimeSpan.FromMilliseconds(500)).ShouldBeTrue(
			"over 100 samples at least one must fall in the lower half, which is the half that decorrelates");
	}

	#endregion Constant Backoff Tests

	#region Linear Backoff Tests

	[Fact]
	public void CalculateLinearDelayWithoutJitter()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Linear,
			baseDelay,
			TimeSpan.FromSeconds(30),
			false);

		// Act & Assert
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(200));
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(300));
		calculator.CalculateDelay(4).ShouldBe(TimeSpan.FromMilliseconds(400));
		calculator.CalculateDelay(5).ShouldBe(TimeSpan.FromMilliseconds(500));
	}

	[Fact]
	public void ApplyJitterToLinearBackoff()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Linear,
			baseDelay,
			TimeSpan.FromSeconds(30),
			true);

		var delays = new List<TimeSpan>();

		// Act
		for (var i = 0; i < 100; i++)
		{
			delays.Add(calculator.CalculateDelay(2));
		}

		var expectedMax = TimeSpan.FromMilliseconds(200);

		// Full jitter over the linear nominal: [0, baseDelay x attempt]. See the constant-backoff arm for
		// why the lower bound is zero rather than half the nominal.
		delays.All(d => d >= TimeSpan.Zero && d <= expectedMax).ShouldBeTrue("full jitter stays within [0, nominal]");
	}

	#endregion Linear Backoff Tests

	#region Exponential Backoff Tests

	[Fact]
	public void CalculateExponentialDelayWithoutJitter()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			baseDelay,
			TimeSpan.FromSeconds(30),
			false,
			2.0);

		// Act & Assert - exponential: 100 * 2^(n-1)
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100)); // 100 * 1
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(200)); // 100 * 2
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(400)); // 100 * 4
		calculator.CalculateDelay(4).ShouldBe(TimeSpan.FromMilliseconds(800)); // 100 * 8
		calculator.CalculateDelay(5).ShouldBe(TimeSpan.FromMilliseconds(1600)); // 100 * 16
	}

	[Fact]
	public void RespectCustomFactor()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			baseDelay,
			TimeSpan.FromSeconds(30),
			false,
			3.0);

		// Act & Assert - exponential with factor 3: 100 * 3^(n-1)
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromMilliseconds(100)); // 100 * 1
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromMilliseconds(300)); // 100 * 3
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromMilliseconds(900)); // 100 * 9
	}

	#endregion Exponential Backoff Tests

	#region Max Delay Capping Tests

	[Fact]
	public void CapDelayAtMaximum()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var maxDelay = TimeSpan.FromMilliseconds(500);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			baseDelay,
			maxDelay,
			false,
			2.0);

		// Act - attempt 4 would give 800ms without cap
		var delay = calculator.CalculateDelay(4);

		// Assert
		delay.ShouldBe(maxDelay);
	}

	[Fact]
	public void CapLinearDelayAtMaximum()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var maxDelay = TimeSpan.FromMilliseconds(250);
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Linear,
			baseDelay,
			maxDelay,
			false);

		// Act - attempt 5 would give 500ms without cap
		var delay = calculator.CalculateDelay(5);

		// Assert
		delay.ShouldBe(maxDelay);
	}

	#endregion Max Delay Capping Tests

	#region Edge Case Tests

	[Fact]
	public void RefuseAZeroAttempt()
	{
		// Was ReturnZeroDelayForZeroAttempt, asserting TimeSpan.Zero. IBackoffCalculator now requires a
		// throw: a zero delay and an invalid argument are different facts, and returning the former for
		// the latter turns a caller's off-by-one into an immediate retry with no backoff at all.
		var calculator = new PollyBackoffCalculatorAdapter();

		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
	}

	[Fact]
	public void RefuseANegativeAttempt()
	{
		var calculator = new PollyBackoffCalculatorAdapter();

		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(-1));
	}

	[Fact]
	public void HandleVeryLargeAttemptNumber()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false);

		// Act - very large attempt should be capped at max delay
		var delay = calculator.CalculateDelay(100);

		// Assert
		delay.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
	}

	#endregion Edge Case Tests

	#region GenerateDelays Tests

	[Fact]
	public void GenerateCorrectNumberOfDelays()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false);

		// Act
		var delays = calculator.GenerateDelays(5).ToList();

		// Assert
		delays.Count.ShouldBe(5);
	}

	[Fact]
	public void GenerateEmptySequenceForZeroRetries()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter();

		// Act
		var delays = calculator.GenerateDelays(0).ToList();

		// Assert
		delays.ShouldBeEmpty();
	}

	[Fact]
	public void GenerateEmptySequenceForNegativeRetries()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter();

		// Act
		var delays = calculator.GenerateDelays(-1).ToList();

		// Assert
		delays.ShouldBeEmpty();
	}

	[Fact]
	public void GenerateIncreasingDelaysForExponentialBackoff()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter(
			DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			false);

		// Act
		var delays = calculator.GenerateDelays(5).ToList();

		// Assert - each delay should be larger than the previous
		for (var i = 1; i < delays.Count; i++)
		{
			delays[i].ShouldBeGreaterThan(delays[i - 1]);
		}
	}

	[Fact]
	public void ResetPreviousDelayOnNewSequence()
	{
		// Arrange
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30));

		// Act - generate two sequences
		var delays1 = calculator.GenerateDelays(3).ToList();
		var delays2 = calculator.GenerateDelays(3).ToList();

		// Assert - both sequences should start fresh (delays[0] should be similar)
		// Due to jitter, we can't assert exact equality, but both should be in similar range
		delays1[0].ShouldBeGreaterThan(TimeSpan.Zero);
		delays2[0].ShouldBeGreaterThan(TimeSpan.Zero);
	}

	#endregion GenerateDelays Tests

	#region Decorrelated Jitter V2 Tests

	[Fact]
	public void DecorrelatedJitterProducesVariedDelays()
	{
		// Arrange
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30));

		var delays = new HashSet<TimeSpan>();

		// Act - collect multiple delays
		for (var i = 0; i < 50; i++)
		{
			// Reset by generating new sequence
			var sequence = calculator.GenerateDelays(3).ToList();
			_ = delays.Add(sequence[1]); // Use second element for more variance
		}

		// Assert - should have variation (not all same values)
		delays.Count.ShouldBeGreaterThan(10); // Should have significant variety
	}

	[Fact]
	public void DecorrelatedJitterRespectsMaxDelay()
	{
		// Arrange
		var maxDelay = TimeSpan.FromMilliseconds(500);
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			maxDelay);

		// Act & Assert - even large attempts should be capped
		for (var attempt = 1; attempt <= 20; attempt++)
		{
			var delay = calculator.CalculateDelay(attempt);
			delay.ShouldBeLessThanOrEqualTo(maxDelay);
		}
	}


	#endregion Decorrelated Jitter V2 Tests

	#region Thread Safety Tests

	[Fact]
	public async Task HandleConcurrentCalculateDelayCalls()
	{
		// Arrange
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30));

		var tasks = new List<Task<TimeSpan>>();

		// Act - simulate concurrent delay calculations
		for (var i = 0; i < 100; i++)
		{
			var attempt = (i % 10) + 1;
			tasks.Add(Task.Run(() => calculator.CalculateDelay(attempt)));
		}

		var delays = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - all delays should be positive
		delays.All(d => d > TimeSpan.Zero).ShouldBeTrue();
	}

	#endregion Thread Safety Tests

	#region Sequence Isolation

	// The adapter is registered AddSingleton, so one instance serves every retry sequence in the
	// process. attempt == 1 must start from base rather than from whatever an unrelated earlier
	// sequence happened to leave in the threaded state, or one message's backoff depends on
	// another's. This is now the only decorrelated implementation in the framework.
	[Fact]
	public void StartEachRetrySequenceFromBaseRatherThanTheLastSequencesDelay()
	{
		var calculator = new PollyBackoffCalculatorAdapter(
			Polly.DelayBackoffType.Exponential,
			TimeSpan.FromMilliseconds(100),
			TimeSpan.FromSeconds(30),
			useJitter: true);

		// A long sequence drives the threaded state far above the base delay.
		for (var attempt = 1; attempt <= 8; attempt++)
		{
			_ = calculator.CalculateDelay(attempt);
		}

		// A fresh sequence on the SAME instance must not inherit that.
		var firstOfNewSequence = calculator.CalculateDelay(1);

		// A first attempt is bounded by the target for attempt 1, which is the base delay.
		// Without the reset this returns a value derived from the previous sequence's last delay.
		firstOfNewSequence.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(100));
	}

	#endregion Sequence Isolation
}
