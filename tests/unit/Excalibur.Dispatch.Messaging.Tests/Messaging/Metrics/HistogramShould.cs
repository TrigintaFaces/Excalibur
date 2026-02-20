// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

using Excalibur.Dispatch.Metrics;

using Histogram = Excalibur.Dispatch.Metrics.Histogram<double>;
using IntHistogram = Excalibur.Dispatch.Metrics.Histogram<int>;
using LongHistogram = Excalibur.Dispatch.Metrics.Histogram<long>;
using FloatHistogram = Excalibur.Dispatch.Metrics.Histogram<float>;
using DoubleHistogram = Excalibur.Dispatch.Metrics.Histogram<double>;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="Histogram{T}"/>.
/// </summary>
/// <remarks>
/// Tests the histogram wrapper for .NET Metrics API.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
[Trait("Priority", "0")]
public sealed class HistogramShould : IDisposable
{
	private readonly Meter _meter;

	public HistogramShould()
	{
		_meter = new Meter("Excalibur.Dispatch.Tests.Histogram", "1.0.0");
	}

	public void Dispose()
	{
		_meter.Dispose();
	}

	#region Constructor Tests

	[Fact]
	public void Constructor_WithRequiredParameters_CreatesInstance()
	{
		// Arrange & Act
		var histogram = new DoubleHistogram(_meter, "test_histogram");

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithAllParameters_CreatesInstance()
	{
		// Arrange & Act
		var histogram = new DoubleHistogram(_meter, "test_histogram", "ms", "Request duration histogram");

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullUnit_CreatesInstance()
	{
		// Arrange & Act
		var histogram = new DoubleHistogram(_meter, "test_histogram", null, "Description");

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void Constructor_WithNullDescription_CreatesInstance()
	{
		// Arrange & Act
		var histogram = new DoubleHistogram(_meter, "test_histogram", "ms", null);

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	#endregion

	#region Record Tests - Double

	[Fact]
	public void Record_WithPositiveDouble_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "double_histogram");

		// Act - Should not throw
		histogram.Record(100.5);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_WithZero_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "zero_histogram");

		// Act - Should not throw
		histogram.Record(0.0);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_WithSmallValue_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "small_histogram");

		// Act - Should not throw
		histogram.Record(0.001);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_WithLargeValue_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "large_histogram");

		// Act - Should not throw
		histogram.Record(1_000_000.0);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Record Tests - Int

	[Fact]
	public void Record_WithInt_Succeeds()
	{
		// Arrange
		var histogram = new IntHistogram(_meter, "int_histogram");

		// Act - Should not throw
		histogram.Record(42);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Record Tests - Long

	[Fact]
	public void Record_WithLong_Succeeds()
	{
		// Arrange
		var histogram = new LongHistogram(_meter, "long_histogram");

		// Act - Should not throw
		histogram.Record(999999L);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Record Tests - With Tags

	[Fact]
	public void Record_WithTags_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "tagged_histogram");
		var tags = new TagList { { "endpoint", "/api/users" } };

		// Act - Should not throw
		histogram.Record(150.5, tags);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_WithMultipleTags_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "multi_tagged_histogram");
		var tags = new TagList
		{
			{ "endpoint", "/api/users" },
			{ "method", "GET" },
			{ "status", "200" }
		};

		// Act - Should not throw
		histogram.Record(250.0, tags);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_WithDefaultTags_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "default_tags_histogram");

		// Act - Should not throw
		histogram.Record(100.0, default);

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Multiple Records Tests

	[Fact]
	public void Record_MultipleTimes_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "multi_record_histogram");

		// Act - Should not throw
		histogram.Record(10.0);
		histogram.Record(20.0);
		histogram.Record(30.0);
		histogram.Record(40.0);

		// Assert
		true.ShouldBeTrue();
	}

	[Fact]
	public void Record_ManyValues_Succeeds()
	{
		// Arrange
		var histogram = new DoubleHistogram(_meter, "many_values_histogram");

		// Act - Should not throw
		for (var i = 0; i < 100; i++)
		{
			histogram.Record(i * 10.0);
		}

		// Assert
		true.ShouldBeTrue();
	}

	#endregion

	#region Type Constraint Tests

	[Fact]
	public void SupportsIntType()
	{
		// Arrange & Act
		var histogram = new IntHistogram(_meter, "int_type_histogram");
		histogram.Record(1);

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsLongType()
	{
		// Arrange & Act
		var histogram = new LongHistogram(_meter, "long_type_histogram");
		histogram.Record(1L);

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsDoubleType()
	{
		// Arrange & Act
		var histogram = new DoubleHistogram(_meter, "double_type_histogram");
		histogram.Record(1.0);

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	[Fact]
	public void SupportsFloatType()
	{
		// Arrange & Act
		var histogram = new FloatHistogram(_meter, "float_type_histogram");
		histogram.Record(1.0f);

		// Assert
		_ = histogram.ShouldNotBeNull();
	}

	#endregion
}
