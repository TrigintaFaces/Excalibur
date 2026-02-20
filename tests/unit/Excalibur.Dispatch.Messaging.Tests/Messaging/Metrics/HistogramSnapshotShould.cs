// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="HistogramSnapshot"/>.
/// </summary>
/// <remarks>
/// Tests the histogram snapshot data class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class HistogramSnapshotShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var snapshot = new HistogramSnapshot();

		// Assert
		_ = snapshot.ShouldNotBeNull();
		snapshot.Count.ShouldBe(0);
		snapshot.Sum.ShouldBe(0);
		snapshot.Mean.ShouldBe(0);
		snapshot.Min.ShouldBe(0);
		snapshot.Max.ShouldBe(0);
		snapshot.P50.ShouldBe(0);
		snapshot.P75.ShouldBe(0);
		snapshot.P95.ShouldBe(0);
		snapshot.P99.ShouldBe(0);
	}

	#endregion

	#region Count Property Tests

	[Fact]
	public void Count_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Count = 1000;

		// Assert
		snapshot.Count.ShouldBe(1000);
	}

	[Theory]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(1000000L)]
	[InlineData(long.MaxValue)]
	public void Count_WithVariousValues_Works(long value)
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Count = value;

		// Assert
		snapshot.Count.ShouldBe(value);
	}

	#endregion

	#region Sum Property Tests

	[Fact]
	public void Sum_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Sum = 12345.67;

		// Assert
		snapshot.Sum.ShouldBe(12345.67);
	}

	[Theory]
	[InlineData(0.0)]
	[InlineData(1.5)]
	[InlineData(double.MaxValue)]
	[InlineData(double.MinValue)]
	public void Sum_WithVariousValues_Works(double value)
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Sum = value;

		// Assert
		snapshot.Sum.ShouldBe(value);
	}

	#endregion

	#region Mean Property Tests

	[Fact]
	public void Mean_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Mean = 45.6;

		// Assert
		snapshot.Mean.ShouldBe(45.6);
	}

	#endregion

	#region Min Property Tests

	[Fact]
	public void Min_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Min = 1.0;

		// Assert
		snapshot.Min.ShouldBe(1.0);
	}

	#endregion

	#region Max Property Tests

	[Fact]
	public void Max_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.Max = 100.0;

		// Assert
		snapshot.Max.ShouldBe(100.0);
	}

	#endregion

	#region Percentile Property Tests

	[Fact]
	public void P50_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.P50 = 25.0;

		// Assert
		snapshot.P50.ShouldBe(25.0);
	}

	[Fact]
	public void P75_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.P75 = 50.0;

		// Assert
		snapshot.P75.ShouldBe(50.0);
	}

	[Fact]
	public void P95_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.P95 = 90.0;

		// Assert
		snapshot.P95.ShouldBe(90.0);
	}

	[Fact]
	public void P99_CanBeSet()
	{
		// Arrange
		var snapshot = new HistogramSnapshot();

		// Act
		snapshot.P99 = 99.0;

		// Assert
		snapshot.P99.ShouldBe(99.0);
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange & Act
		var snapshot = new HistogramSnapshot
		{
			Count = 1000,
			Sum = 50000.0,
			Mean = 50.0,
			Min = 1.0,
			Max = 100.0,
			P50 = 45.0,
			P75 = 70.0,
			P95 = 90.0,
			P99 = 98.0,
		};

		// Assert
		snapshot.Count.ShouldBe(1000);
		snapshot.Sum.ShouldBe(50000.0);
		snapshot.Mean.ShouldBe(50.0);
		snapshot.Min.ShouldBe(1.0);
		snapshot.Max.ShouldBe(100.0);
		snapshot.P50.ShouldBe(45.0);
		snapshot.P75.ShouldBe(70.0);
		snapshot.P95.ShouldBe(90.0);
		snapshot.P99.ShouldBe(98.0);
	}

	[Fact]
	public void TypicalLatencyDistribution_Scenario()
	{
		// Arrange - Typical latency distribution in milliseconds
		var snapshot = new HistogramSnapshot
		{
			Count = 10000,
			Sum = 250000.0,
			Mean = 25.0,
			Min = 5.0,
			Max = 500.0,
			P50 = 20.0,  // Median
			P75 = 30.0,
			P95 = 100.0, // 95th percentile often used for SLA
			P99 = 250.0, // Tail latency
		};

		// Assert - Verify realistic distribution properties
		snapshot.Min.ShouldBeLessThan(snapshot.P50);
		snapshot.P50.ShouldBeLessThan(snapshot.P75);
		snapshot.P75.ShouldBeLessThan(snapshot.P95);
		snapshot.P95.ShouldBeLessThan(snapshot.P99);
		snapshot.P99.ShouldBeLessThanOrEqualTo(snapshot.Max);
	}

	[Fact]
	public void EmptyHistogram_Scenario()
	{
		// Arrange & Act - Empty histogram with no data
		var snapshot = new HistogramSnapshot
		{
			Count = 0,
			Sum = 0,
			Mean = 0,
			Min = 0,
			Max = 0,
			P50 = 0,
			P75 = 0,
			P95 = 0,
			P99 = 0,
		};

		// Assert
		snapshot.Count.ShouldBe(0);
		snapshot.Sum.ShouldBe(0);
	}

	[Fact]
	public void SingleValueHistogram_Scenario()
	{
		// Arrange & Act - Histogram with single value
		var snapshot = new HistogramSnapshot
		{
			Count = 1,
			Sum = 42.0,
			Mean = 42.0,
			Min = 42.0,
			Max = 42.0,
			P50 = 42.0,
			P75 = 42.0,
			P95 = 42.0,
			P99 = 42.0,
		};

		// Assert - All percentiles should equal the single value
		snapshot.Min.ShouldBe(42.0);
		snapshot.Max.ShouldBe(42.0);
		snapshot.Mean.ShouldBe(42.0);
		snapshot.P50.ShouldBe(42.0);
	}

	[Fact]
	public void HighVarianceDistribution_Scenario()
	{
		// Arrange & Act - Distribution with high variance (common in microservices)
		var snapshot = new HistogramSnapshot
		{
			Count = 100000,
			Sum = 1000000.0,
			Mean = 10.0,
			Min = 0.5,
			Max = 5000.0,  // Significant outliers
			P50 = 5.0,     // Most requests are fast
			P75 = 8.0,
			P95 = 50.0,
			P99 = 500.0,   // Tail is heavy
		};

		// Assert - Large gap between P99 and Max indicates outliers
		var tailSpread = snapshot.Max - snapshot.P99;
		tailSpread.ShouldBeGreaterThan(1000);
	}

	#endregion
}
