// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Buffers;

namespace Excalibur.Dispatch.Tests.Messaging.Buffers;

/// <summary>
/// Unit tests for <see cref="BufferStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class BufferStatisticsShould
{
	[Fact]
	public void HaveDefaultZeroValues()
	{
		// Arrange & Act
		var stats = new BufferStatistics();

		// Assert
		stats.TotalRented.ShouldBe(0);
		stats.TotalReturned.ShouldBe(0);
		stats.OutstandingBuffers.ShouldBe(0);
	}

	[Fact]
	public void HaveEmptySizeDistributionByDefault()
	{
		// Arrange & Act
		var stats = new BufferStatistics();

		// Assert
		stats.SizeDistribution.ShouldNotBeNull();
		stats.SizeDistribution.ShouldBeEmpty();
	}

	[Fact]
	public void AllowSettingTotalRented()
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.TotalRented = 100;

		// Assert
		stats.TotalRented.ShouldBe(100);
	}

	[Fact]
	public void AllowSettingTotalReturned()
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.TotalReturned = 95;

		// Assert
		stats.TotalReturned.ShouldBe(95);
	}

	[Fact]
	public void AllowSettingOutstandingBuffers()
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.OutstandingBuffers = 5;

		// Assert
		stats.OutstandingBuffers.ShouldBe(5);
	}

	[Fact]
	public void AllowAddingSizeDistributionEntries()
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.SizeDistribution[1024] = 50;
		stats.SizeDistribution[4096] = 25;

		// Assert
		stats.SizeDistribution.Count.ShouldBe(2);
		stats.SizeDistribution[1024].ShouldBe(50);
		stats.SizeDistribution[4096].ShouldBe(25);
	}

	[Fact]
	public void SupportObjectInitializer()
	{
		// Arrange & Act
		var stats = new BufferStatistics
		{
			TotalRented = 1000,
			TotalReturned = 990,
			OutstandingBuffers = 10,
			SizeDistribution = new Dictionary<int, long>
			{
				[1024] = 500,
				[2048] = 300,
				[4096] = 200,
			},
		};

		// Assert
		stats.TotalRented.ShouldBe(1000);
		stats.TotalReturned.ShouldBe(990);
		stats.OutstandingBuffers.ShouldBe(10);
		stats.SizeDistribution.Count.ShouldBe(3);
		stats.SizeDistribution[1024].ShouldBe(500);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousTotalRentedValues(long value)
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.TotalRented = value;

		// Assert
		stats.TotalRented.ShouldBe(value);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(100)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousTotalReturnedValues(long value)
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.TotalReturned = value;

		// Assert
		stats.TotalReturned.ShouldBe(value);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(50)]
	[InlineData(long.MaxValue)]
	public void AcceptVariousOutstandingBuffersValues(long value)
	{
		// Arrange
		var stats = new BufferStatistics();

		// Act
		stats.OutstandingBuffers = value;

		// Assert
		stats.OutstandingBuffers.ShouldBe(value);
	}

	[Fact]
	public void AllowUpdatingSizeDistributionValues()
	{
		// Arrange
		var stats = new BufferStatistics();
		stats.SizeDistribution[1024] = 50;

		// Act
		stats.SizeDistribution[1024] = 75;

		// Assert
		stats.SizeDistribution[1024].ShouldBe(75);
	}

	[Fact]
	public void AllowRemovingSizeDistributionEntries()
	{
		// Arrange
		var stats = new BufferStatistics();
		stats.SizeDistribution[1024] = 50;
		stats.SizeDistribution[2048] = 25;

		// Act
		stats.SizeDistribution.Remove(1024);

		// Assert
		stats.SizeDistribution.Count.ShouldBe(1);
		stats.SizeDistribution.ContainsKey(1024).ShouldBeFalse();
		stats.SizeDistribution.ContainsKey(2048).ShouldBeTrue();
	}

	[Fact]
	public void SupportReplacingSizeDistribution()
	{
		// Arrange
		var stats = new BufferStatistics
		{
			SizeDistribution = new Dictionary<int, long>
			{
				[512] = 100,
			},
		};

		// Act - Replace the distribution
		stats = new BufferStatistics
		{
			TotalRented = stats.TotalRented,
			TotalReturned = stats.TotalReturned,
			OutstandingBuffers = stats.OutstandingBuffers,
			SizeDistribution = new Dictionary<int, long>
			{
				[1024] = 200,
				[2048] = 150,
			},
		};

		// Assert
		stats.SizeDistribution.Count.ShouldBe(2);
		stats.SizeDistribution.ContainsKey(512).ShouldBeFalse();
		stats.SizeDistribution.ContainsKey(1024).ShouldBeTrue();
	}

	[Fact]
	public void TrackTypicalBufferUsageScenario()
	{
		// Arrange & Act - Simulate buffer pool usage
		var stats = new BufferStatistics
		{
			TotalRented = 1000,
			TotalReturned = 950,
			OutstandingBuffers = 50,
			SizeDistribution = new Dictionary<int, long>
			{
				[256] = 100,    // Small buffers
				[1024] = 400,   // Medium buffers
				[4096] = 350,   // Large buffers
				[16384] = 150,  // Extra large buffers
			},
		};

		// Assert
		stats.OutstandingBuffers.ShouldBe(stats.TotalRented - stats.TotalReturned);

		var totalDistributed = stats.SizeDistribution.Values.Sum();
		totalDistributed.ShouldBe(stats.TotalRented);
	}
}
