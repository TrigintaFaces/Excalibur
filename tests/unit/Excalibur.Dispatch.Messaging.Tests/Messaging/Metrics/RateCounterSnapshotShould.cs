// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="RateCounterSnapshot"/>.
/// </summary>
/// <remarks>
/// Tests the counter snapshot data class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class RateCounterSnapshotShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var snapshot = new RateCounterSnapshot();

		// Assert
		_ = snapshot.ShouldNotBeNull();
		snapshot.Value.ShouldBe(0);
		snapshot.Timestamp.ShouldBe(default);
		snapshot.LastReset.ShouldBe(default);
		snapshot.TimeSinceReset.ShouldBe(TimeSpan.Zero);
		snapshot.Rate.ShouldBe(0.0);
		snapshot.AverageRate.ShouldBe(0.0);
	}

	#endregion

	#region Value Property Tests

	[Fact]
	public void Value_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Value = 42;

		// Assert
		snapshot.Value.ShouldBe(42);
	}

	[Fact]
	public void Value_CanBeNegative()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Value = -100;

		// Assert
		snapshot.Value.ShouldBe(-100);
	}

	[Fact]
	public void Value_CanBeLarge()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Value = long.MaxValue;

		// Assert
		snapshot.Value.ShouldBe(long.MaxValue);
	}

	#endregion

	#region Timestamp Property Tests

	[Fact]
	public void Timestamp_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();
		var timestamp = DateTimeOffset.UtcNow;

		// Act
		snapshot.Timestamp = timestamp;

		// Assert
		snapshot.Timestamp.ShouldBe(timestamp);
	}

	[Fact]
	public void Timestamp_CanBeMinValue()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Timestamp = DateTimeOffset.MinValue;

		// Assert
		snapshot.Timestamp.ShouldBe(DateTimeOffset.MinValue);
	}

	[Fact]
	public void Timestamp_CanBeMaxValue()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Timestamp = DateTimeOffset.MaxValue;

		// Assert
		snapshot.Timestamp.ShouldBe(DateTimeOffset.MaxValue);
	}

	#endregion

	#region LastReset Property Tests

	[Fact]
	public void LastReset_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();
		var lastReset = DateTimeOffset.UtcNow.AddHours(-1);

		// Act
		snapshot.LastReset = lastReset;

		// Assert
		snapshot.LastReset.ShouldBe(lastReset);
	}

	#endregion

	#region TimeSinceReset Property Tests

	[Fact]
	public void TimeSinceReset_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();
		var timeSpan = TimeSpan.FromHours(2);

		// Act
		snapshot.TimeSinceReset = timeSpan;

		// Assert
		snapshot.TimeSinceReset.ShouldBe(timeSpan);
	}

	[Fact]
	public void TimeSinceReset_CanBeZero()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.TimeSinceReset = TimeSpan.Zero;

		// Assert
		snapshot.TimeSinceReset.ShouldBe(TimeSpan.Zero);
	}

	[Fact]
	public void TimeSinceReset_CanBeLargeDuration()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();
		var timeSpan = TimeSpan.FromDays(365);

		// Act
		snapshot.TimeSinceReset = timeSpan;

		// Assert
		snapshot.TimeSinceReset.ShouldBe(timeSpan);
	}

	#endregion

	#region Rate Property Tests

	[Fact]
	public void Rate_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Rate = 150.5;

		// Assert
		snapshot.Rate.ShouldBe(150.5);
	}

	[Fact]
	public void Rate_CanBeZero()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Rate = 0.0;

		// Assert
		snapshot.Rate.ShouldBe(0.0);
	}

	[Fact]
	public void Rate_CanBeNegative()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.Rate = -25.5;

		// Assert
		snapshot.Rate.ShouldBe(-25.5);
	}

	#endregion

	#region AverageRate Property Tests

	[Fact]
	public void AverageRate_CanBeSet()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.AverageRate = 75.25;

		// Assert
		snapshot.AverageRate.ShouldBe(75.25);
	}

	[Fact]
	public void AverageRate_CanBeZero()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.AverageRate = 0.0;

		// Assert
		snapshot.AverageRate.ShouldBe(0.0);
	}

	[Fact]
	public void AverageRate_CanBeVerySmall()
	{
		// Arrange
		var snapshot = new RateCounterSnapshot();

		// Act
		snapshot.AverageRate = 0.0001;

		// Assert
		snapshot.AverageRate.ShouldBe(0.0001);
	}

	#endregion

	#region Full Snapshot Tests

	[Fact]
	public void Snapshot_AllPropertiesCanBeSet()
	{
		// Arrange
		var now = DateTimeOffset.UtcNow;
		var resetTime = now.AddHours(-1);

		// Act
		var snapshot = new RateCounterSnapshot
		{
			Value = 1000,
			Timestamp = now,
			LastReset = resetTime,
			TimeSinceReset = TimeSpan.FromHours(1),
			Rate = 100.5,
			AverageRate = 277.78,
		};

		// Assert
		snapshot.Value.ShouldBe(1000);
		snapshot.Timestamp.ShouldBe(now);
		snapshot.LastReset.ShouldBe(resetTime);
		snapshot.TimeSinceReset.ShouldBe(TimeSpan.FromHours(1));
		snapshot.Rate.ShouldBe(100.5);
		snapshot.AverageRate.ShouldBe(277.78);
	}

	[Fact]
	public void Snapshot_CreatedFromCounter_HasCorrectValues()
	{
		// Arrange
		var counter = new RateCounter();
		_ = counter.IncrementBy(500);

		// Act
		var snapshot = counter.GetSnapshot();

		// Assert
		snapshot.Value.ShouldBe(500);
		snapshot.Timestamp.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		snapshot.LastReset.ShouldBeGreaterThan(DateTimeOffset.MinValue);
		snapshot.TimeSinceReset.ShouldBeGreaterThanOrEqualTo(TimeSpan.Zero);
	}

	#endregion
}
