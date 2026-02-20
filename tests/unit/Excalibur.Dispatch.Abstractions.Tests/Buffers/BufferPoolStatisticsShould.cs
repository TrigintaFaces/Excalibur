// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Buffers;

/// <summary>
/// Unit tests for <see cref="BufferPoolStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Buffers")]
[Trait("Priority", "0")]
public sealed class BufferPoolStatisticsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TotalAllocations_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.TotalAllocations.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalDeallocations_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.TotalDeallocations.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalBytesRented_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.TotalBytesRented.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalBytesReturned_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.TotalBytesReturned.ShouldBe(0);
	}

	[Fact]
	public void Default_BuffersInUse_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.BuffersInUse.ShouldBe(0);
	}

	[Fact]
	public void Default_PeakBuffersInUse_IsZero()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		stats.PeakBuffersInUse.ShouldBe(0);
	}

	[Fact]
	public void Default_BucketStatistics_IsEmptyArray()
	{
		// Arrange & Act
		var stats = new BufferPoolStatistics();

		// Assert
		_ = stats.BucketStatistics.ShouldNotBeNull();
		stats.BucketStatistics.ShouldBeEmpty();
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Arrange
		var buckets = new BucketStatistics[] { new() { BucketName = "Small", MaxSize = 1024 } };

		// Act
		var stats = new BufferPoolStatistics
		{
			TotalAllocations = 1000,
			TotalDeallocations = 950,
			TotalBytesRented = 1_000_000,
			TotalBytesReturned = 950_000,
			BuffersInUse = 50,
			PeakBuffersInUse = 100,
			BucketStatistics = buckets,
		};

		// Assert
		stats.TotalAllocations.ShouldBe(1000);
		stats.TotalDeallocations.ShouldBe(950);
		stats.TotalBytesRented.ShouldBe(1_000_000);
		stats.TotalBytesReturned.ShouldBe(950_000);
		stats.BuffersInUse.ShouldBe(50);
		stats.PeakBuffersInUse.ShouldBe(100);
		stats.BucketStatistics.ShouldBe(buckets);
	}

	#endregion

	#region BucketStatistics Tests

	[Fact]
	public void BucketStatistics_CanContainMultipleBuckets()
	{
		// Arrange
		var buckets = new[]
		{
			new BucketStatistics { BucketName = "Small", MaxSize = 1024 },
			new BucketStatistics { BucketName = "Medium", MaxSize = 4096 },
			new BucketStatistics { BucketName = "Large", MaxSize = 16384 },
		};

		// Act
		var stats = new BufferPoolStatistics { BucketStatistics = buckets };

		// Assert
		stats.BucketStatistics.Length.ShouldBe(3);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void CanHaveLongMaxValues()
	{
		// Act
		var stats = new BufferPoolStatistics
		{
			TotalAllocations = long.MaxValue,
			TotalDeallocations = long.MaxValue,
			TotalBytesRented = long.MaxValue,
			TotalBytesReturned = long.MaxValue,
			PeakBuffersInUse = long.MaxValue,
		};

		// Assert
		stats.TotalAllocations.ShouldBe(long.MaxValue);
	}

	#endregion
}
