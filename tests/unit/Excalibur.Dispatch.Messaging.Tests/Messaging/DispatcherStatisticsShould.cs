// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

/// <summary>
/// Unit tests for <see cref="DispatcherStatistics"/>.
/// </summary>
/// <remarks>
/// Tests the dispatcher performance statistics struct.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Messaging")]
[Trait("Priority", "0")]
public sealed class DispatcherStatisticsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_Default_InitializesWithDefaults()
	{
		// Arrange & Act
		var stats = new DispatcherStatistics();

		// Assert
		stats.TotalMessages.ShouldBe(0);
		stats.TotalBatches.ShouldBe(0);
		stats.AverageBatchSize.ShouldBe(0);
		stats.Throughput.ShouldBe(0);
		stats.QueueDepth.ShouldBe(0);
		stats.ElapsedMilliseconds.ShouldBe(0);
	}

	#endregion

	#region TotalMessages Property Tests

	[Theory]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(1000000L)]
	[InlineData(long.MaxValue)]
	public void TotalMessages_WithVariousValues_Works(long value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { TotalMessages = value };

		// Assert
		stats.TotalMessages.ShouldBe(value);
	}

	#endregion

	#region TotalBatches Property Tests

	[Theory]
	[InlineData(0L)]
	[InlineData(1L)]
	[InlineData(100000L)]
	public void TotalBatches_WithVariousValues_Works(long value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { TotalBatches = value };

		// Assert
		stats.TotalBatches.ShouldBe(value);
	}

	#endregion

	#region AverageBatchSize Property Tests

	[Theory]
	[InlineData(0.0)]
	[InlineData(1.0)]
	[InlineData(10.5)]
	[InlineData(100.123)]
	public void AverageBatchSize_WithVariousValues_Works(double value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { AverageBatchSize = value };

		// Assert
		stats.AverageBatchSize.ShouldBe(value);
	}

	#endregion

	#region Throughput Property Tests

	[Theory]
	[InlineData(0.0)]
	[InlineData(1000.0)]
	[InlineData(1000000.5)]
	public void Throughput_WithVariousValues_Works(double value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { Throughput = value };

		// Assert
		stats.Throughput.ShouldBe(value);
	}

	#endregion

	#region QueueDepth Property Tests

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	[InlineData(1000)]
	[InlineData(int.MaxValue)]
	public void QueueDepth_WithVariousValues_Works(int value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { QueueDepth = value };

		// Assert
		stats.QueueDepth.ShouldBe(value);
	}

	#endregion

	#region ElapsedMilliseconds Property Tests

	[Theory]
	[InlineData(0.0)]
	[InlineData(100.5)]
	[InlineData(60000.0)]
	public void ElapsedMilliseconds_WithVariousValues_Works(double value)
	{
		// Arrange & Act
		var stats = new DispatcherStatistics { ElapsedMilliseconds = value };

		// Assert
		stats.ElapsedMilliseconds.ShouldBe(value);
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equals_SameValues_ReturnsTrue()
	{
		// Arrange
		var stats1 = new DispatcherStatistics
		{
			TotalMessages = 1000,
			TotalBatches = 100,
			AverageBatchSize = 10.0,
			Throughput = 500.0,
			QueueDepth = 50,
			ElapsedMilliseconds = 2000.0,
		};
		var stats2 = new DispatcherStatistics
		{
			TotalMessages = 1000,
			TotalBatches = 100,
			AverageBatchSize = 10.0,
			Throughput = 500.0,
			QueueDepth = 50,
			ElapsedMilliseconds = 2000.0,
		};

		// Act & Assert
		stats1.Equals(stats2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_DifferentTotalMessages_ReturnsFalse()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 1000 };
		var stats2 = new DispatcherStatistics { TotalMessages = 2000 };

		// Act & Assert
		stats1.Equals(stats2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentThroughput_ReturnsFalse()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { Throughput = 100.0 };
		var stats2 = new DispatcherStatistics { Throughput = 200.0 };

		// Act & Assert
		stats1.Equals(stats2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithObject_SameValues_ReturnsTrue()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		object stats2 = new DispatcherStatistics { TotalMessages = 100 };

		// Act & Assert
		stats1.Equals(stats2).ShouldBeTrue();
	}

	[Fact]
	public void Equals_WithNull_ReturnsFalse()
	{
		// Arrange
		var stats = new DispatcherStatistics();

		// Act & Assert
		stats.Equals(null).ShouldBeFalse();
	}

	[Fact]
	public void Equals_WithWrongType_ReturnsFalse()
	{
		// Arrange
		var stats = new DispatcherStatistics();

		// Act & Assert
		stats.Equals("not a stat").ShouldBeFalse();
	}

	#endregion

	#region GetHashCode Tests

	[Fact]
	public void GetHashCode_SameValues_ReturnsSameHashCode()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100, Throughput = 50.0 };
		var stats2 = new DispatcherStatistics { TotalMessages = 100, Throughput = 50.0 };

		// Act & Assert
		stats1.GetHashCode().ShouldBe(stats2.GetHashCode());
	}

	[Fact]
	public void GetHashCode_DifferentValues_ReturnsDifferentHashCodes()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		var stats2 = new DispatcherStatistics { TotalMessages = 200 };

		// Act & Assert
		stats1.GetHashCode().ShouldNotBe(stats2.GetHashCode());
	}

	#endregion

	#region Operator Tests

	[Fact]
	public void OperatorEquals_SameValues_ReturnsTrue()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		var stats2 = new DispatcherStatistics { TotalMessages = 100 };

		// Act & Assert
		(stats1 == stats2).ShouldBeTrue();
	}

	[Fact]
	public void OperatorEquals_DifferentValues_ReturnsFalse()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		var stats2 = new DispatcherStatistics { TotalMessages = 200 };

		// Act & Assert
		(stats1 == stats2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorNotEquals_SameValues_ReturnsFalse()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		var stats2 = new DispatcherStatistics { TotalMessages = 100 };

		// Act & Assert
		(stats1 != stats2).ShouldBeFalse();
	}

	[Fact]
	public void OperatorNotEquals_DifferentValues_ReturnsTrue()
	{
		// Arrange
		var stats1 = new DispatcherStatistics { TotalMessages = 100 };
		var stats2 = new DispatcherStatistics { TotalMessages = 200 };

		// Act & Assert
		(stats1 != stats2).ShouldBeTrue();
	}

	#endregion

	#region Full Object Tests

	[Fact]
	public void AllProperties_CanBeSetViaObjectInitializer()
	{
		// Arrange & Act
		var stats = new DispatcherStatistics
		{
			TotalMessages = 10000,
			TotalBatches = 1000,
			AverageBatchSize = 10.0,
			Throughput = 5000.5,
			QueueDepth = 25,
			ElapsedMilliseconds = 2000.0,
		};

		// Assert
		stats.TotalMessages.ShouldBe(10000);
		stats.TotalBatches.ShouldBe(1000);
		stats.AverageBatchSize.ShouldBe(10.0);
		stats.Throughput.ShouldBe(5000.5);
		stats.QueueDepth.ShouldBe(25);
		stats.ElapsedMilliseconds.ShouldBe(2000.0);
	}

	#endregion

	#region Typical Usage Scenarios

	[Fact]
	public void HighThroughputScenario()
	{
		// Arrange & Act
		var stats = new DispatcherStatistics
		{
			TotalMessages = 1000000,
			TotalBatches = 10000,
			AverageBatchSize = 100.0,
			Throughput = 500000.0, // 500K messages/second
			QueueDepth = 0,
			ElapsedMilliseconds = 2000.0,
		};

		// Assert
		stats.Throughput.ShouldBeGreaterThan(100000);
		stats.QueueDepth.ShouldBe(0); // Queue is empty
	}

	[Fact]
	public void BacklogScenario()
	{
		// Arrange & Act - System with significant backlog
		var stats = new DispatcherStatistics
		{
			TotalMessages = 100000,
			TotalBatches = 10000,
			AverageBatchSize = 10.0,
			Throughput = 1000.0, // Slow throughput
			QueueDepth = 50000, // Large backlog
			ElapsedMilliseconds = 100000.0,
		};

		// Assert
		stats.QueueDepth.ShouldBeGreaterThan(10000); // Significant backlog
		stats.Throughput.ShouldBeLessThan(10000); // Processing slower than ideal
	}

	#endregion
}
