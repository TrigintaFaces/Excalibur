// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Resilience;
using Excalibur.Dispatch.Options.Resilience;

namespace Excalibur.Dispatch.Tests.Messaging.Resilience;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
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
		var options = new RetryPolicyOptions
		{
			BaseDelay = TimeSpan.FromMilliseconds(500),
			MaxDelay = TimeSpan.FromSeconds(10),
			BackoffMultiplier = 3.0,
			EnableJitter = false,
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
			multiplier: 2.0,
			enableJitter: false);

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
			multiplier: 2.0,
			enableJitter: false);

		// Act — attempt 10 would be 1s * 2^9 = 512s, should clamp
		var delay = calculator.CalculateDelay(10);

		// Assert
		delay.ShouldBe(TimeSpan.FromSeconds(5));
	}

	[Fact]
	public void CalculateDelay_WithJitter_VaryDelay()
	{
		// Arrange
		var calculator = new ExponentialBackoffCalculator(
			baseDelay: TimeSpan.FromMilliseconds(100),
			maxDelay: TimeSpan.FromSeconds(60),
			multiplier: 2.0,
			enableJitter: true,
			jitterFactor: 0.5);

		// Act — run multiple times and collect results
		var delays = new HashSet<double>();
		for (var i = 0; i < 20; i++)
		{
			delays.Add(calculator.CalculateDelay(1).TotalMilliseconds);
		}

		// Assert — with 50% jitter, delays should vary (not all exactly 100ms)
		// At least some variation is expected
		delays.Count.ShouldBeGreaterThan(1);
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
	public void Constructor_ThrowOnZeroBaseDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.Zero,
				maxDelay: TimeSpan.FromSeconds(10)));
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
	public void Constructor_ThrowOnZeroMaxDelay()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromSeconds(1),
				maxDelay: TimeSpan.Zero));
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
	public void Constructor_ThrowOnNegativeJitterFactor()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromSeconds(1),
				maxDelay: TimeSpan.FromSeconds(10),
				jitterFactor: -0.1));
	}

	[Fact]
	public void Constructor_ThrowOnJitterFactorGreaterThanOne()
	{
		// Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ExponentialBackoffCalculator(
				baseDelay: TimeSpan.FromSeconds(1),
				maxDelay: TimeSpan.FromSeconds(10),
				jitterFactor: 1.5));
	}

	[Fact]
	public void CreateForHighThroughput_ReturnConfiguredCalculator()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForHighThroughput();

		// Assert — base delay of 100ms, no jitter variance check needed
		var delay = calculator.CalculateDelay(1);
		delay.TotalMilliseconds.ShouldBeInRange(50, 150); // 100ms +/- 50% jitter
	}

	[Fact]
	public void CreateForMessageQueue_ReturnConfiguredCalculator()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForMessageQueue();

		// Assert
		var delay = calculator.CalculateDelay(1);
		delay.TotalMilliseconds.ShouldBeInRange(750, 1250); // 1000ms +/- 25% jitter
	}

	[Fact]
	public void CreateForTransientFailures_ReturnConfiguredCalculator()
	{
		// Act
		var calculator = ExponentialBackoffCalculator.CreateForTransientFailures();

		// Assert
		var delay = calculator.CalculateDelay(1);
		delay.TotalMilliseconds.ShouldBeInRange(35, 65); // 50ms +/- 30% jitter
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
			multiplier: 1.0,
			enableJitter: false);

		// Act
		var delay1 = calculator.CalculateDelay(1);
		var delay2 = calculator.CalculateDelay(5);
		var delay3 = calculator.CalculateDelay(10);

		// Assert — all should be the same since 1^n = 1
		delay1.ShouldBe(TimeSpan.FromMilliseconds(200));
		delay2.ShouldBe(TimeSpan.FromMilliseconds(200));
		delay3.ShouldBe(TimeSpan.FromMilliseconds(200));
	}
}
