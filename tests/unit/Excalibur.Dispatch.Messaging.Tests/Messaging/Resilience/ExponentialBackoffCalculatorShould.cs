// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

/// <summary>
/// Tests for the <see cref="ExponentialBackoffCalculator"/> class.
/// Epic 6 (bd-rj9o): Integration tests for exponential backoff with jitter.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ExponentialBackoffCalculatorShould
{
	#region Exponential Growth Tests

	[Fact]
	public void CalculateExponentialGrowthCorrectly()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(30),
			multiplier: 2.0,
			enableJitter: false);

		// Act & Assert - Without jitter, delays should be exactly: 1s, 2s, 4s, 8s, 16s
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(1));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(2));
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(4));
		calculator.CalculateDelay(4).ShouldBe(TimeSpan.FromSeconds(8));
		calculator.CalculateDelay(5).ShouldBe(TimeSpan.FromSeconds(16));
	}

	[Fact]
	public void ApplyCustomMultiplierCorrectly()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(30),
			multiplier: 3.0,
			enableJitter: false);

		// Act & Assert - With multiplier 3: 1s, 3s, 9s, 27s
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(1));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(3));
		calculator.CalculateDelay(3).ShouldBe(TimeSpan.FromSeconds(9));
		calculator.CalculateDelay(4).ShouldBe(TimeSpan.FromSeconds(27));
	}

	[Fact]
	public void HandleLargeAttemptNumbersWithoutOverflow()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromMinutes(5),
			multiplier: 2.0,
			enableJitter: false);

		// Act - Large attempt should be capped at maxDelay
		var delay = calculator.CalculateDelay(100);

		// Assert - Should be capped at max, not overflow
		delay.ShouldBe(TimeSpan.FromMinutes(5));
	}

	#endregion Exponential Growth Tests

	#region Max Delay Capping Tests

	[Fact]
	public void CapDelayAtMaximumValue()
	{
		// Arrange
		var maxDelay = TimeSpan.FromSeconds(10);
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(5),
			maxDelay: maxDelay,
			multiplier: 2.0,
			enableJitter: false);

		// Act - attempt 3 would be 5 * 4 = 20s, but should be capped at 10s
		var delay = calculator.CalculateDelay(3);

		// Assert
		delay.ShouldBe(maxDelay);
	}

	[Fact]
	public void NeverExceedMaxDelayEvenWithJitter()
	{
		// Arrange
		var maxDelay = TimeSpan.FromSeconds(10);
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(9),
			maxDelay: maxDelay,
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5);

		// Act - Run multiple times to ensure jitter never exceeds max
		for (var i = 0; i < 100; i++)
		{
			var delay = calculator.CalculateDelay(2);

			// Assert - Should never exceed max delay
			delay.ShouldBeLessThanOrEqualTo(maxDelay);
		}
	}

	#endregion Max Delay Capping Tests

	#region Jitter Tests

	[Fact]
	public void ApplyJitterWithinExpectedBounds()
	{
		// Arrange
		var baseDelay = TimeSpan.FromSeconds(10);
		var jitterFactor = 0.1; // 10% jitter
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: baseDelay,
			maxDelay: TimeSpan.FromMinutes(30),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: jitterFactor);

		// Act - Calculate delay multiple times for attempt 1 (base delay = 10s)
		var delays = new List<TimeSpan>();
		for (var i = 0; i < 100; i++)
		{
			delays.Add(calculator.CalculateDelay(1));
		}

		// Assert - All delays should be within ±10% of 10s (9s to 11s)
		var minExpected = baseDelay.TotalMilliseconds * (1 - jitterFactor);
		var maxExpected = baseDelay.TotalMilliseconds * (1 + jitterFactor);

		foreach (var delay in delays)
		{
			delay.TotalMilliseconds.ShouldBeGreaterThanOrEqualTo(minExpected);
			delay.TotalMilliseconds.ShouldBeLessThanOrEqualTo(maxExpected);
		}
	}

	[Fact]
	public void ProduceVariedDelaysWithJitterEnabled()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(30),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5);

		// Act - Calculate delays multiple times
		var delays = new HashSet<double>();
		for (var i = 0; i < 50; i++)
		{
			_ = delays.Add(calculator.CalculateDelay(1).TotalMilliseconds);
		}

		// Assert - With 50% jitter, we should see variation (not all the same)
		delays.Count.ShouldBeGreaterThan(1);
	}

	[Fact]
	public void ProduceConsistentDelaysWithJitterDisabled()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromMinutes(30),
			multiplier: 2.0,
			enableJitter: false);

		// Act - Calculate delays multiple times
		var delays = new HashSet<double>();
		for (var i = 0; i < 10; i++)
		{
			_ = delays.Add(calculator.CalculateDelay(1).TotalMilliseconds);
		}

		// Assert - Without jitter, all delays should be identical
		delays.Count.ShouldBe(1);
	}

	#endregion Jitter Tests

	#region Thread Safety Tests

	[Fact]
	public async Task BeThreadSafeForConcurrentCalculations()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.3);

		var tasks = new List<Task<TimeSpan>>();
		const int concurrentCalls = 100;

		// Act - Make concurrent calls
		for (var i = 0; i < concurrentCalls; i++)
		{
			var attempt = (i % 5) + 1; // Vary attempts 1-5
			tasks.Add(Task.Run(() => calculator.CalculateDelay(attempt)));
		}

		var results = await Task.WhenAll(tasks).ConfigureAwait(false);

		// Assert - All results should be valid (no exceptions, positive values)
		foreach (var result in results)
		{
			result.ShouldBeGreaterThan(TimeSpan.Zero);
			result.ShouldBeLessThanOrEqualTo(TimeSpan.FromSeconds(30));
		}
	}

	#endregion Thread Safety Tests

	#region Input Validation Tests

	[Fact]
	public void ThrowOnZeroAttempt()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(0));
	}

	[Fact]
	public void ThrowOnNegativeAttempt()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => calculator.CalculateDelay(-1));
	}

	[Fact]
	public void ThrowOnZeroBaseDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.Zero,
			maxDelay: TimeSpan.FromSeconds(30)));
	}

	[Fact]
	public void ThrowOnNegativeBaseDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(-1),
			maxDelay: TimeSpan.FromSeconds(30)));
	}

	[Fact]
	public void ThrowOnZeroMaxDelay()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.Zero));
	}

	[Fact]
	public void ThrowOnMultiplierLessThanOne()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 0.5));
	}

	[Fact]
	public void ThrowOnNegativeJitterFactor()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: -0.1));
	}

	[Fact]
	public void ThrowOnJitterFactorGreaterThanOne()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromSeconds(1),
			maxDelay: TimeSpan.FromSeconds(30),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 1.1));
	}

	#endregion Input Validation Tests

	#region Factory Methods Tests

	[Fact]
	public void CreateHighThroughputCalculatorWithCorrectSettings()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForHighThroughput();

		// Assert - verify it produces reasonable delays
		var delay1 = calculator.CalculateDelay(1);
		delay1.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(50)); // With jitter
		delay1.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(150));
	}

	[Fact]
	public void CreateMessageQueueCalculatorWithCorrectSettings()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForMessageQueue();

		// Assert - verify it produces reasonable delays
		var delay1 = calculator.CalculateDelay(1);
		delay1.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(750)); // With 25% jitter
		delay1.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(1250));
	}

	[Fact]
	public void CreateTransientFailuresCalculatorWithCorrectSettings()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForTransientFailures();

		// Assert - verify it produces reasonable delays (short for transient)
		var delay1 = calculator.CalculateDelay(1);
		delay1.ShouldBeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(35)); // With 30% jitter
		delay1.ShouldBeLessThanOrEqualTo(TimeSpan.FromMilliseconds(65));
	}

	#endregion Factory Methods Tests

	#region Options Constructor Tests

	[Fact]
	public void UseRetryPolicyOptionsCorrectly()
	{
		// Arrange
		var options = new RetryPolicyOptions
		{
			BaseDelay = TimeSpan.FromSeconds(2),
			MaxDelay = TimeSpan.FromSeconds(60),
			BackoffMultiplier = 1.5,
			EnableJitter = false,
			JitterFactor = 0.2,
		};

		// Act
		var calculator = new ExponentialBackoffCalculator(options);

		// Assert - Calculate delay should use the options
		calculator.CalculateDelay(1).ShouldBe(TimeSpan.FromSeconds(2));
		calculator.CalculateDelay(2).ShouldBe(TimeSpan.FromSeconds(3)); // 2 * 1.5
	}

	[Fact]
	public void UseDefaultValuesWhenOptionsHaveNulls()
	{
		// Arrange
		var options = new RetryPolicyOptions();

		// Act
		var calculator = new ExponentialBackoffCalculator(options);

		// Assert - Should work with defaults
		var delay = calculator.CalculateDelay(1);
		delay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	[Fact]
	public void UseDefaultsWhenConstructedWithoutParameters()
	{
		// Act
		var calculator = new ExponentialBackoffCalculator();

		// Assert - Default constructor should work
		var delay = calculator.CalculateDelay(1);
		delay.ShouldBeGreaterThan(TimeSpan.Zero);
	}

	#endregion Options Constructor Tests
}
