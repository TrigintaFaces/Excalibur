// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="SimpleCounter"/>.
/// </summary>
/// <remarks>
/// Tests the non-thread-safe counter implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class SimpleCounterShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_InitializesValueToZero()
	{
		// Arrange & Act
		var counter = new SimpleCounter();

		// Assert
		counter.Value.ShouldBe(0.0);
	}

	#endregion

	#region Increment Tests

	[Fact]
	public void Increment_WithDefaultAmount_IncrementsBy1()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment();

		// Assert
		counter.Value.ShouldBe(1.0);
	}

	[Fact]
	public void Increment_WithSpecificAmount_IncrementsCorrectly()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment(5.0);

		// Assert
		counter.Value.ShouldBe(5.0);
	}

	[Fact]
	public void Increment_MultipleTimesWithDefault_AccumulatesCorrectly()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment();
		counter.Increment();
		counter.Increment();

		// Assert
		counter.Value.ShouldBe(3.0);
	}

	[Fact]
	public void Increment_WithVariousAmounts_AccumulatesCorrectly()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment(10.0);
		counter.Increment(5.5);
		counter.Increment(2.5);

		// Assert
		counter.Value.ShouldBe(18.0);
	}

	[Fact]
	public void Increment_WithZero_DoesNotChangeValue()
	{
		// Arrange
		var counter = new SimpleCounter();
		counter.Increment(5.0);

		// Act
		counter.Increment(0.0);

		// Assert
		counter.Value.ShouldBe(5.0);
	}

	[Fact]
	public void Increment_WithNegativeAmount_DecreasesValue()
	{
		// Arrange
		var counter = new SimpleCounter();
		counter.Increment(10.0);

		// Act
		counter.Increment(-3.0);

		// Assert
		counter.Value.ShouldBe(7.0);
	}

	[Fact]
	public void Increment_WithVerySmallAmount_Works()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment(0.0001);

		// Assert
		counter.Value.ShouldBe(0.0001);
	}

	[Fact]
	public void Increment_WithVeryLargeAmount_Works()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment(1_000_000.0);

		// Assert
		counter.Value.ShouldBe(1_000_000.0);
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_ReturnsCurrentValue()
	{
		// Arrange
		var counter = new SimpleCounter();
		counter.Increment(42.5);

		// Act
		var value = counter.Value;

		// Assert
		value.ShouldBe(42.5);
	}

	[Fact]
	public void Value_CanBeReadMultipleTimes()
	{
		// Arrange
		var counter = new SimpleCounter();
		counter.Increment(10.0);

		// Act
		var value1 = counter.Value;
		var value2 = counter.Value;
		var value3 = counter.Value;

		// Assert
		value1.ShouldBe(10.0);
		value2.ShouldBe(10.0);
		value3.ShouldBe(10.0);
	}

	#endregion

	#region Interface Tests

	[Fact]
	public void ImplementsICounterMetric()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Assert
		_ = counter.ShouldBeAssignableTo<ICounterMetric>();
	}

	#endregion

	#region Edge Case Tests

	[Fact]
	public void Increment_ManyTimes_AccumulatesCorrectly()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		for (var i = 0; i < 1000; i++)
		{
			counter.Increment();
		}

		// Assert
		counter.Value.ShouldBe(1000.0);
	}

	[Fact]
	public void Increment_WithDecimalPrecision_MaintainsPrecision()
	{
		// Arrange
		var counter = new SimpleCounter();

		// Act
		counter.Increment(0.1);
		counter.Increment(0.2);

		// Assert - Note: floating point precision may vary slightly
		counter.Value.ShouldBe(0.3, 0.0000001);
	}

	#endregion
}
