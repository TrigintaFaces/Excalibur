// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Buffers;

/// <summary>
/// Unit tests for <see cref="BucketStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Buffers")]
[Trait("Priority", "0")]
public sealed class BucketStatisticsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_BucketName_IsEmpty()
	{
		// Arrange & Act
		var stats = new BucketStatistics();

		// Assert
		stats.BucketName.ShouldBe(string.Empty);
	}

	[Fact]
	public void Default_MaxSize_IsZero()
	{
		// Arrange & Act
		var stats = new BucketStatistics();

		// Assert
		stats.MaxSize.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalRents_IsZero()
	{
		// Arrange & Act
		var stats = new BucketStatistics();

		// Assert
		stats.TotalRents.ShouldBe(0);
	}

	[Fact]
	public void Default_TotalReturns_IsZero()
	{
		// Arrange & Act
		var stats = new BucketStatistics();

		// Assert
		stats.TotalReturns.ShouldBe(0);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var stats = new BucketStatistics
		{
			BucketName = "SmallBuffers",
			MaxSize = 4096,
			TotalRents = 10000,
			TotalReturns = 9500,
		};

		// Assert
		stats.BucketName.ShouldBe("SmallBuffers");
		stats.MaxSize.ShouldBe(4096);
		stats.TotalRents.ShouldBe(10000);
		stats.TotalReturns.ShouldBe(9500);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void BucketName_CanBeNull()
	{
		// Note: The default is empty string, but init can set null
		var stats = new BucketStatistics { BucketName = null! };

		// Assert
		stats.BucketName.ShouldBeNull();
	}

	[Fact]
	public void MaxSize_CanBeNegative()
	{
		// Act
		var stats = new BucketStatistics { MaxSize = -1 };

		// Assert - The type allows negative values (validation is separate concern)
		stats.MaxSize.ShouldBe(-1);
	}

	[Fact]
	public void TotalRents_CanExceedTotalReturns()
	{
		// This represents buffers that are still in use
		var stats = new BucketStatistics
		{
			TotalRents = 1000,
			TotalReturns = 900,
		};

		// Assert
		(stats.TotalRents - stats.TotalReturns).ShouldBe(100); // 100 still in use
	}

	[Fact]
	public void CanHaveLongMaxValues()
	{
		// Act
		var stats = new BucketStatistics
		{
			TotalRents = long.MaxValue,
			TotalReturns = long.MaxValue,
		};

		// Assert
		stats.TotalRents.ShouldBe(long.MaxValue);
		stats.TotalReturns.ShouldBe(long.MaxValue);
	}

	#endregion
}
