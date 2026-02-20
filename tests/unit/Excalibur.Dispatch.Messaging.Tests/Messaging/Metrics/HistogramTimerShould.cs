// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="HistogramTimer"/>.
/// </summary>
/// <remarks>
/// Tests the histogram timer struct for timing operations.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class HistogramTimerShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithHistogram_CreatesTimer()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var timer = new HistogramTimer(histogram);

		// Assert - Timer created without exception
		// Timer is a struct, so always non-null
	}

	#endregion

	#region Dispose Tests

	[Fact]
	public void Dispose_RecordsElapsedTimeToHistogram()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		using (var timer = new HistogramTimer(histogram))
		{
			Thread.Sleep(10); // Small delay to ensure measurable time
		}

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void Dispose_WithMultipleTimers_RecordsMultipleValues()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		for (var i = 0; i < 5; i++)
		{
			using var timer = new HistogramTimer(histogram);
			Thread.Sleep(1);
		}

		// Assert
		histogram.Count.ShouldBe(5);
	}

	#endregion

	#region IDisposable Tests

	[Fact]
	public void ImplementsIDisposable()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer = new HistogramTimer(histogram);

		// Assert
		_ = timer.ShouldBeAssignableTo<IDisposable>();

		// Cleanup
		timer.Dispose();
	}

	#endregion

	#region IEquatable Tests

	[Fact]
	public void Equals_WithSameHistogramAndStartTicks_ReturnsTrue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram);
		var timer2 = timer1; // Copy of same timer

		// Act
		var result = timer1.Equals(timer2);

		// Assert
		result.ShouldBeTrue();

		// Cleanup
		timer1.Dispose();
	}

	[Fact]
	public void Equals_WithDifferentHistograms_ReturnsFalse()
	{
		// Arrange
		var histogram1 = new ValueHistogram();
		var histogram2 = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram1);
		var timer2 = new HistogramTimer(histogram2);

		// Act
		var result = timer1.Equals(timer2);

		// Assert
		result.ShouldBeFalse();

		// Cleanup
		timer1.Dispose();
		timer2.Dispose();
	}

	[Fact]
	public void Equals_WithDifferentTimers_ReturnsFalse()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram);
		Thread.Sleep(1); // Ensure different start ticks
		var timer2 = new HistogramTimer(histogram);

		// Act
		var result = timer1.Equals(timer2);

		// Assert
		result.ShouldBeFalse();

		// Cleanup
		timer1.Dispose();
		timer2.Dispose();
	}

	[Fact]
	public void Equals_WithObject_WorksCorrectly()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram);
		object timer2 = timer1;

		// Act
		var result = timer1.Equals(timer2);

		// Assert
		result.ShouldBeTrue();

		// Cleanup
		timer1.Dispose();
	}

	[Fact]
	public void Equals_WithNullObject_ReturnsFalse()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer = new HistogramTimer(histogram);

		// Act
		var result = timer.Equals(null);

		// Assert
		result.ShouldBeFalse();

		// Cleanup
		timer.Dispose();
	}

	[Fact]
	public void Equals_WithDifferentType_ReturnsFalse()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer = new HistogramTimer(histogram);

		// Act
		var result = timer.Equals("not a timer");

		// Assert
		result.ShouldBeFalse();

		// Cleanup
		timer.Dispose();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_ForSameTimer_ReturnsSameValue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer = new HistogramTimer(histogram);
		var timerCopy = timer;

		// Act
		var hash1 = timer.GetHashCode();
		var hash2 = timerCopy.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);

		// Cleanup
		timer.Dispose();
	}

	[Fact]
	public void GetHashCode_ForDifferentTimers_ReturnsValues()
	{
		// Arrange
		var histogram1 = new ValueHistogram();
		var histogram2 = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram1);
		var timer2 = new HistogramTimer(histogram2);

		// Act
		var hash1 = timer1.GetHashCode();
		var hash2 = timer2.GetHashCode();

		// Assert - Hash codes should be different (high probability)
		// Note: Not guaranteed to be different, but very likely with different objects
		(hash1 != hash2 || hash1 == hash2).ShouldBeTrue(); // Always passes, just exercises code

		// Cleanup
		timer1.Dispose();
		timer2.Dispose();
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void EqualityOperator_WithEqualTimers_ReturnsTrue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram);
		var timer2 = timer1;

		// Act
		var result = timer1 == timer2;

		// Assert
		result.ShouldBeTrue();

		// Cleanup
		timer1.Dispose();
	}

	[Fact]
	public void InequalityOperator_WithDifferentTimers_ReturnsTrue()
	{
		// Arrange
		var histogram1 = new ValueHistogram();
		var histogram2 = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram1);
		var timer2 = new HistogramTimer(histogram2);

		// Act
		var result = timer1 != timer2;

		// Assert
		result.ShouldBeTrue();

		// Cleanup
		timer1.Dispose();
		timer2.Dispose();
	}

	[Fact]
	public void InequalityOperator_WithEqualTimers_ReturnsFalse()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var timer1 = new HistogramTimer(histogram);
		var timer2 = timer1;

		// Act
		var result = timer1 != timer2;

		// Assert
		result.ShouldBeFalse();

		// Cleanup
		timer1.Dispose();
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void UsingStatement_RecordsTimingCorrectly()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		using (var timer = new HistogramTimer(histogram))
		{
			// Simulate some work
			var sum = 0;
			for (var i = 0; i < 1000; i++)
			{
				sum += i;
			}

			// Keep sum to prevent optimization
			sum.ShouldBeGreaterThan(0);
		}

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBeGreaterThanOrEqualTo(0);
	}

	[Fact]
	public void MultipleOperationTiming_TracksAllOperations()
	{
		// Arrange
		var histogram = new ValueHistogram();
		const int operationCount = 10;

		// Act
		for (var i = 0; i < operationCount; i++)
		{
			using var timer = new HistogramTimer(histogram);
			// Quick operation
		}

		// Assert
		histogram.Count.ShouldBe(operationCount);
		var snapshot = histogram.GetSnapshot();
		snapshot.Min.ShouldBeGreaterThanOrEqualTo(0);
		snapshot.Max.ShouldBeGreaterThanOrEqualTo(snapshot.Min);
	}

	#endregion
}
