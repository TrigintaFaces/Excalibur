// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Metrics;

namespace Excalibur.Dispatch.Tests.Messaging.Metrics;

/// <summary>
/// Unit tests for <see cref="MetricEntry"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Metrics")]
public sealed class MetricEntryShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void CreateWithAllParameters()
	{
		// Arrange
		var ticks = DateTime.UtcNow.Ticks;

		// Act
		var entry = new MetricEntry(ticks, MetricType.Counter, 1, 100.0, 5);

		// Assert
		entry.TimestampTicks.ShouldBe(ticks);
		entry.Type.ShouldBe(MetricType.Counter);
		entry.MetricId.ShouldBe(1);
		entry.Value.ShouldBe(100.0);
		entry.LabelSetId.ShouldBe(5);
	}

	[Fact]
	public void CreateWithDefaultLabelSetId()
	{
		// Arrange
		var ticks = DateTime.UtcNow.Ticks;

		// Act
		var entry = new MetricEntry(ticks, MetricType.Gauge, 2, 50.5);

		// Assert
		entry.LabelSetId.ShouldBe(0);
	}

	[Fact]
	public void HaveZeroReservedField()
	{
		// Arrange
		var entry = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Assert
		entry.Reserved.ShouldBe(0);
	}

	#endregion

	#region MetricType Tests

	[Fact]
	public void StoreCounterType()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Counter, 1, 1.0);

		// Assert
		entry.Type.ShouldBe(MetricType.Counter);
	}

	[Fact]
	public void StoreGaugeType()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Gauge, 1, 1.0);

		// Assert
		entry.Type.ShouldBe(MetricType.Gauge);
	}

	[Fact]
	public void StoreHistogramType()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Histogram, 1, 1.0);

		// Assert
		entry.Type.ShouldBe(MetricType.Histogram);
	}

	[Fact]
	public void StoreSummaryType()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Summary, 1, 1.0);

		// Assert
		entry.Type.ShouldBe(MetricType.Summary);
	}

	#endregion

	#region Value Tests

	[Fact]
	public void StorePositiveValue()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Counter, 1, 123.456);

		// Assert
		entry.Value.ShouldBe(123.456);
	}

	[Fact]
	public void StoreNegativeValue()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Gauge, 1, -50.5);

		// Assert
		entry.Value.ShouldBe(-50.5);
	}

	[Fact]
	public void StoreZeroValue()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Counter, 1, 0.0);

		// Assert
		entry.Value.ShouldBe(0.0);
	}

	[Fact]
	public void StoreLargeValue()
	{
		// Arrange & Act
		var entry = new MetricEntry(1, MetricType.Counter, 1, 1e15);

		// Assert
		entry.Value.ShouldBe(1e15);
	}

	#endregion

	#region Size Tests

	[Fact]
	public void HavePositiveSize()
	{
		// Act
		var size = MetricEntry.Size;

		// Assert
		size.ShouldBeGreaterThan(0);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void BeEqualWhenAllFieldsMatch()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0, 5);
		var entry2 = new MetricEntry(1000, MetricType.Counter, 1, 100.0, 5);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeTrue();
		(entry1 == entry2).ShouldBeTrue();
		(entry1 != entry2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenTimestampDiffers()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		var entry2 = new MetricEntry(2000, MetricType.Counter, 1, 100.0);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeFalse();
		(entry1 != entry2).ShouldBeTrue();
	}

	[Fact]
	public void NotBeEqualWhenTypeDiffers()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		var entry2 = new MetricEntry(1000, MetricType.Gauge, 1, 100.0);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenMetricIdDiffers()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		var entry2 = new MetricEntry(1000, MetricType.Counter, 2, 100.0);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenValueDiffers()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		var entry2 = new MetricEntry(1000, MetricType.Counter, 1, 200.0);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeFalse();
	}

	[Fact]
	public void NotBeEqualWhenLabelSetIdDiffers()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0, 5);
		var entry2 = new MetricEntry(1000, MetricType.Counter, 1, 100.0, 10);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForNull()
	{
		// Arrange
		var entry = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Act & Assert
		entry.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnFalseForDifferentType()
	{
		// Arrange
		var entry = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Act & Assert
		entry.Equals("not an entry").ShouldBeFalse();
	}

	[Fact]
	public void EqualsObjectReturnTrueForMatchingEntry()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		object entry2 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Act & Assert
		entry1.Equals(entry2).ShouldBeTrue();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void ProduceConsistentHashCode()
	{
		// Arrange
		var entry = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Act
		var hash1 = entry.GetHashCode();
		var hash2 = entry.GetHashCode();

		// Assert
		hash1.ShouldBe(hash2);
	}

	[Fact]
	public void ProduceSameHashCodeForEqualEntries()
	{
		// Arrange
		var entry1 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);
		var entry2 = new MetricEntry(1000, MetricType.Counter, 1, 100.0);

		// Act & Assert
		entry1.GetHashCode().ShouldBe(entry2.GetHashCode());
	}

	#endregion
}
