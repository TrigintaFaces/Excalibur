// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="ValueHistogram"/>.
/// </summary>
/// <remarks>
/// Tests the value histogram implementation.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class ValueHistogramShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_WithNoParameters_CreatesInstance()
	{
		// Arrange & Act
		var histogram = new ValueHistogram();

		// Assert
		_ = histogram.ShouldNotBeNull();
		histogram.Count.ShouldBe(0);
	}

	[Fact]
	public void Constructor_WithMetadataAndConfiguration_CreatesInstance()
	{
		// Arrange
		var metadata = new MetricMetadata(1, "test_histogram", "Test histogram", "microseconds", MetricType.Histogram);
		var config = HistogramConfiguration.DefaultLatency;

		// Act
		var histogram = new ValueHistogram(metadata, config);

		// Assert
		_ = histogram.ShouldNotBeNull();
		histogram.Metadata.ShouldBe(metadata);
	}

	#endregion

	#region Record Tests

	[Fact]
	public void Record_WithValue_IncrementsCount()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Record(100);

		// Assert
		histogram.Count.ShouldBe(1);
	}

	[Fact]
	public void Record_WithMultipleValues_TracksCorrectCount()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Assert
		histogram.Count.ShouldBe(3);
	}

	[Fact]
	public void Record_WithValues_UpdatesSum()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Assert - Mean = Sum / Count = 60 / 3 = 20
		histogram.Mean.ShouldBe(20);
	}

	[Fact]
	public void Record_WithValues_TracksMin()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Record(50);
		histogram.Record(25);
		histogram.Record(75);

		// Assert
		histogram.Min.ShouldBe(25);
	}

	[Fact]
	public void Record_WithValues_TracksMax()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.Record(50);
		histogram.Record(25);
		histogram.Record(75);

		// Assert
		histogram.Max.ShouldBe(75);
	}

	#endregion

	#region RecordMilliseconds Tests

	[Fact]
	public void RecordMilliseconds_WithValue_RecordsValue()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		histogram.RecordMilliseconds(123.5);

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBe(123.5);
	}

	#endregion

	#region Mean Tests

	[Fact]
	public void Mean_WithNoRecords_ReturnsZero()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var mean = histogram.Mean;

		// Assert
		mean.ShouldBe(0);
	}

	[Fact]
	public void Mean_WithSingleRecord_ReturnsThatValue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(42);

		// Act
		var mean = histogram.Mean;

		// Assert
		mean.ShouldBe(42);
	}

	#endregion

	#region Min/Max Tests

	[Fact]
	public void Min_WithNoRecords_ReturnsZero()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var min = histogram.Min;

		// Assert
		min.ShouldBe(0);
	}

	[Fact]
	public void Max_WithNoRecords_ReturnsZero()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var max = histogram.Max;

		// Assert
		max.ShouldBe(0);
	}

	#endregion

	#region GetPercentile Tests

	[Fact]
	public void GetPercentile_WithNoRecords_ReturnsZero()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var p50 = histogram.GetPercentile(50);

		// Assert
		p50.ShouldBe(0);
	}

	[Fact]
	public void GetPercentile_WithSingleValue_ReturnsThatValue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(100);

		// Act
		var p50 = histogram.GetPercentile(50);

		// Assert
		p50.ShouldBe(100);
	}

	[Fact]
	public void GetPercentile_WithMultipleValues_ReturnsCorrectPercentile()
	{
		// Arrange
		var histogram = new ValueHistogram();
		for (var i = 1; i <= 100; i++)
		{
			histogram.Record(i);
		}

		// Act
		var p50 = histogram.GetPercentile(50);
		var p90 = histogram.GetPercentile(90);

		// Assert
		p50.ShouldBe(50);
		p90.ShouldBe(90);
	}

	[Fact]
	public void GetPercentile_With0Percentile_ReturnsMinValue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Act
		var p0 = histogram.GetPercentile(0);

		// Assert - 0th percentile returns the minimum value
		p0.ShouldBe(10);
	}

	[Fact]
	public void GetPercentile_With100Percentile_ReturnsMaxValue()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Act
		var p100 = histogram.GetPercentile(100);

		// Assert
		p100.ShouldBe(30);
	}

	[Fact]
	public void GetPercentile_WithNegativePercentile_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => histogram.GetPercentile(-1));
	}

	[Fact]
	public void GetPercentile_WithPercentileOver100_ThrowsArgumentOutOfRangeException()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => histogram.GetPercentile(101));
	}

	#endregion

	#region Reset Tests

	[Fact]
	public void Reset_ClearsAllStatistics()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(100);
		histogram.Record(200);
		histogram.Record(300);

		// Act
		histogram.Reset();

		// Assert
		histogram.Count.ShouldBe(0);
		histogram.Mean.ShouldBe(0);
		histogram.Min.ShouldBe(0);
		histogram.Max.ShouldBe(0);
	}

	[Fact]
	public void Reset_AllowsRecordingAfterReset()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(100);
		histogram.Reset();

		// Act
		histogram.Record(50);

		// Assert
		histogram.Count.ShouldBe(1);
		histogram.Mean.ShouldBe(50);
	}

	#endregion

	#region GetSnapshot Tests

	[Fact]
	public void GetSnapshot_WithNoRecords_ReturnsEmptySnapshot()
	{
		// Arrange
		var histogram = new ValueHistogram();

		// Act
		var snapshot = histogram.GetSnapshot();

		// Assert
		_ = snapshot.ShouldNotBeNull();
		snapshot.Count.ShouldBe(0);
		snapshot.Sum.ShouldBe(0);
		snapshot.Mean.ShouldBe(0);
		snapshot.Min.ShouldBe(0);
		snapshot.Max.ShouldBe(0);
	}

	[Fact]
	public void GetSnapshot_WithRecords_ReturnsCorrectValues()
	{
		// Arrange
		var histogram = new ValueHistogram();
		histogram.Record(10);
		histogram.Record(20);
		histogram.Record(30);

		// Act
		var snapshot = histogram.GetSnapshot();

		// Assert
		snapshot.Count.ShouldBe(3);
		snapshot.Sum.ShouldBe(60);
		snapshot.Mean.ShouldBe(20);
		snapshot.Min.ShouldBe(10);
		snapshot.Max.ShouldBe(30);
	}

	[Fact]
	public void GetSnapshot_IncludesPercentiles()
	{
		// Arrange
		var histogram = new ValueHistogram();
		for (var i = 1; i <= 100; i++)
		{
			histogram.Record(i);
		}

		// Act
		var snapshot = histogram.GetSnapshot();

		// Assert
		snapshot.P50.ShouldBe(50);
		snapshot.P75.ShouldBe(75);
		snapshot.P95.ShouldBe(95);
		snapshot.P99.ShouldBe(99);
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIMetric()
	{
		// Arrange & Act
		var histogram = new ValueHistogram();

		// Assert
		_ = histogram.ShouldBeAssignableTo<IMetric>();
	}

	#endregion

	#region Thread Safety Tests

	[Fact]
	public void ConcurrentRecord_IsThreadSafe()
	{
		// Arrange
		var histogram = new ValueHistogram();
		var exceptions = new List<Exception>();

		// Act
		_ = Parallel.For(0, 1000, i =>
		{
			try
			{
				histogram.Record(i);
			}
			catch (Exception ex)
			{
				lock (exceptions)
				{
					exceptions.Add(ex);
				}
			}
		});

		// Assert
		exceptions.ShouldBeEmpty();
		histogram.Count.ShouldBe(1000);
	}

	#endregion
}
