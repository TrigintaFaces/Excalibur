// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricSnapshot"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricSnapshotShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void CreateWithAllParameters()
	{
		// Arrange
		var buckets = new[] { new HistogramBucket(1.0, 5), new HistogramBucket(5.0, 10) };

		// Act
		var snapshot = new MetricSnapshot(
			metricId: 1,
			type: MetricType.Histogram,
			timestampTicks: 1000L,
			value: 100.0,
			labelSetId: 5,
			count: 10,
			sum: 500.0,
			min: 10.0,
			max: 200.0,
			buckets: buckets);

		// Assert
		snapshot.MetricId.ShouldBe(1);
		snapshot.Type.ShouldBe(MetricType.Histogram);
		snapshot.TimestampTicks.ShouldBe(1000L);
		snapshot.Value.ShouldBe(100.0);
		snapshot.LabelSetId.ShouldBe(5);
		snapshot.Count.ShouldBe(10);
		snapshot.Sum.ShouldBe(500.0);
		snapshot.Min.ShouldBe(10.0);
		snapshot.Max.ShouldBe(200.0);
		snapshot.Buckets.ShouldBe(buckets);
	}

	[Fact]
	public void CreateWithRequiredParametersOnly()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(
			metricId: 1,
			type: MetricType.Counter,
			timestampTicks: 1000L,
			value: 50.0,
			labelSetId: 0);

		// Assert
		snapshot.MetricId.ShouldBe(1);
		snapshot.Type.ShouldBe(MetricType.Counter);
		snapshot.TimestampTicks.ShouldBe(1000L);
		snapshot.Value.ShouldBe(50.0);
		snapshot.LabelSetId.ShouldBe(0);
		snapshot.Count.ShouldBe(1); // Default
		snapshot.Sum.ShouldBe(50.0); // Defaults to value when sum is 0
		snapshot.Min.ShouldBe(50.0); // Defaults to value when min is 0
		snapshot.Max.ShouldBe(50.0); // Defaults to value when max is 0
		snapshot.Buckets.ShouldBeNull();
	}

	[Fact]
	public void DefaultSumMinMaxToValueWhenZero()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(
			metricId: 1,
			type: MetricType.Gauge,
			timestampTicks: 1000L,
			value: 75.5,
			labelSetId: 0,
			count: 1,
			sum: 0,
			min: 0,
			max: 0);

		// Assert
		snapshot.Sum.ShouldBe(75.5);
		snapshot.Min.ShouldBe(75.5);
		snapshot.Max.ShouldBe(75.5);
	}

	[Fact]
	public void UseExplicitSumMinMaxWhenProvided()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(
			metricId: 1,
			type: MetricType.Histogram,
			timestampTicks: 1000L,
			value: 50.0,
			labelSetId: 0,
			count: 5,
			sum: 250.0,
			min: 10.0,
			max: 100.0);

		// Assert
		snapshot.Sum.ShouldBe(250.0);
		snapshot.Min.ShouldBe(10.0);
		snapshot.Max.ShouldBe(100.0);
	}

	#endregion

	#region MetricType Tests

	[Fact]
	public void StoreCounterType()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Counter);
	}

	[Fact]
	public void StoreGaugeType()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Gauge, 1000, 100.0, 0);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Gauge);
	}

	[Fact]
	public void StoreHistogramType()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Histogram, 1000, 100.0, 0);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Histogram);
	}

	[Fact]
	public void StoreSummaryType()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Summary, 1000, 100.0, 0);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Summary);
	}

	#endregion

	#region Value Tests

	[Fact]
	public void StorePositiveValue()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 123.456, 0);

		// Assert
		snapshot.Value.ShouldBe(123.456);
	}

	[Fact]
	public void StoreNegativeValue()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Gauge, 1000, -50.5, 0);

		// Assert
		snapshot.Value.ShouldBe(-50.5);
	}

	[Fact]
	public void StoreZeroValue()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 0.0, 0);

		// Assert
		snapshot.Value.ShouldBe(0.0);
	}

	#endregion

	#region Buckets Tests

	[Fact]
	public void StoreHistogramBuckets()
	{
		// Arrange
		var buckets = new[]
		{
			new HistogramBucket(0.005, 100),
			new HistogramBucket(0.01, 200),
			new HistogramBucket(0.025, 350),
			new HistogramBucket(0.05, 400),
			new HistogramBucket(0.1, 450)
		};

		// Act
		var snapshot = new MetricSnapshot(1, MetricType.Histogram, 1000, 0.0, 0, buckets: buckets);

		// Assert
		snapshot.Buckets.ShouldNotBeNull();
		snapshot.Buckets.Length.ShouldBe(5);
		snapshot.Buckets[0].UpperBound.ShouldBe(0.005);
		snapshot.Buckets[0].Count.ShouldBe(100);
	}

	[Fact]
	public void AllowNullBuckets()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, buckets: null);

		// Assert
		snapshot.Buckets.ShouldBeNull();
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenAllFieldsMatch()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5, 10, 500.0, 10.0, 200.0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5, 10, 500.0, 10.0, 200.0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeTrue();
		(snapshot1 == snapshot2).ShouldBeTrue();
		(snapshot1 != snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenMetricIdDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);
		var snapshot2 = new MetricSnapshot(2, MetricType.Counter, 1000, 100.0, 0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
		(snapshot1 != snapshot2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqualWhenTypeDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Gauge, 1000, 100.0, 0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenTimestampDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 2000, 100.0, 0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenValueDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 200.0, 0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenLabelSetIdDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 10);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenCountDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, count: 5);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, count: 10);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenSumDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, sum: 500.0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, sum: 600.0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenMinDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, min: 10.0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, min: 20.0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenMaxDiffers()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, max: 100.0);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0, max: 200.0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForNull()
	{
		// Arrange
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);

		// Act & Assert
		snapshot.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForDifferentType()
	{
		// Arrange
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);

		// Act & Assert
		snapshot.Equals("not a snapshot").ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnTrueForMatchingSnapshot()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);
		object snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 0);

		// Act & Assert
		snapshot1.Equals(snapshot2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void ProduceConsistentHashCode()
	{
		// Arrange
		var snapshot = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5);

		// Act
		var hash1 = snapshot.GetHashCode();
		var hash2 = snapshot.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ProduceSameHashCodeForEqualSnapshots()
	{
		// Arrange
		var snapshot1 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5);
		var snapshot2 = new MetricSnapshot(1, MetricType.Counter, 1000, 100.0, 5);

		// Act & Assert
		snapshot1.GetHashCode().ShouldBe(snapshot2.GetHashCode());
	}

	[Fact]
	public void IncludeBucketsInHashCode()
	{
		// Arrange
		var buckets = new[] { new HistogramBucket(1.0, 5) };
		var snapshot1 = new MetricSnapshot(1, MetricType.Histogram, 1000, 100.0, 0, buckets: buckets);
		var snapshot2 = new MetricSnapshot(1, MetricType.Histogram, 1000, 100.0, 0, buckets: null);

		// Act & Assert - different buckets should produce different hash codes
		snapshot1.GetHashCode().ShouldNotBe(snapshot2.GetHashCode());
	}

	#endregion

	#region Real-World Scenario Tests

	[Fact]
	public void CreateCounterSnapshot()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(
			metricId: 100,
			type: MetricType.Counter,
			timestampTicks: DateTime.UtcNow.Ticks,
			value: 12345,
			labelSetId: 42,
			count: 1,
			sum: 12345);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Counter);
		snapshot.Value.ShouldBe(12345);
	}

	[Fact]
	public void CreateGaugeSnapshot()
	{
		// Arrange & Act
		var snapshot = new MetricSnapshot(
			metricId: 200,
			type: MetricType.Gauge,
			timestampTicks: DateTime.UtcNow.Ticks,
			value: 75.5,
			labelSetId: 0);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Gauge);
		snapshot.Value.ShouldBe(75.5);
	}

	[Fact]
	public void CreateHistogramSnapshot()
	{
		// Arrange
		var buckets = new[]
		{
			new HistogramBucket(0.005, 100),
			new HistogramBucket(0.01, 250),
			new HistogramBucket(0.025, 400),
			new HistogramBucket(0.05, 480),
			new HistogramBucket(0.1, 495),
			new HistogramBucket(double.PositiveInfinity, 500)
		};

		// Act
		var snapshot = new MetricSnapshot(
			metricId: 300,
			type: MetricType.Histogram,
			timestampTicks: DateTime.UtcNow.Ticks,
			value: 0.0,
			labelSetId: 0,
			count: 500,
			sum: 15.75,
			min: 0.001,
			max: 0.095,
			buckets: buckets);

		// Assert
		snapshot.Type.ShouldBe(MetricType.Histogram);
		snapshot.Count.ShouldBe(500);
		snapshot.Buckets.ShouldNotBeNull();
		snapshot.Buckets.Length.ShouldBe(6);
	}

	#endregion
}
