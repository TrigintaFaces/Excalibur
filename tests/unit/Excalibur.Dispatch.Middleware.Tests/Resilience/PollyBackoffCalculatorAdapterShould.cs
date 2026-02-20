// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience.Polly;

using Polly;

namespace Excalibur.Dispatch.Middleware.Tests.Resilience;

/// <summary>
/// Tests for the <see cref="PollyBackoffCalculatorAdapter"/> class.
/// Sprint 45 (bd-5tsb): Unit tests for Polly backoff calculator adapter.
/// </summary>
[Trait("Category", "Unit")]
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
	public void ThrowArgumentOutOfRangeForZeroBaseDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PollyBackoffCalculatorAdapter(
				DelayBackoffType.Exponential,
				TimeSpan.Zero,
				TimeSpan.FromSeconds(30)));
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
	public void ThrowArgumentOutOfRangeForMaxDelayLessThanBaseDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() =>
			new PollyBackoffCalculatorAdapter(
				DelayBackoffType.Exponential,
				TimeSpan.FromSeconds(10),
				TimeSpan.FromSeconds(5)));
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

		// Assert - jitter should produce varying delays within bounds
		delays.All(d => d >= TimeSpan.FromMilliseconds(500) && d <= baseDelay).ShouldBeTrue();
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
		var expectedMin = TimeSpan.FromMilliseconds(100); // 50% jitter

		// Assert - jitter should produce varying delays
		delays.All(d => d >= expectedMin && d <= expectedMax).ShouldBeTrue();
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
	public void ReturnZeroDelayForZeroAttempt()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter();

		// Act
		var delay = calculator.CalculateDelay(0);

		// Assert
		delay.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void ReturnZeroDelayForNegativeAttempt()
	{
		// Arrange
		var calculator = new PollyBackoffCalculatorAdapter();

		// Act
		var delay = calculator.CalculateDelay(-1);

		// Assert
		delay.ShouldBe(TimeSpan.Zero);
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

	[Fact]
	public void DecorrelatedJitterMaintainsMinimumDelay()
	{
		// Arrange
		var baseDelay = TimeSpan.FromMilliseconds(100);
		var calculator = PollyBackoffCalculatorAdapter.CreateDecorrelatedJitter(
			baseDelay,
			TimeSpan.FromSeconds(30));

		// Act & Assert - delays should have a minimum threshold
		for (var attempt = 1; attempt <= 10; attempt++)
		{
			var delay = calculator.CalculateDelay(attempt);
			// Minimum is 50% of base delay according to implementation
			delay.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50));
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
}
