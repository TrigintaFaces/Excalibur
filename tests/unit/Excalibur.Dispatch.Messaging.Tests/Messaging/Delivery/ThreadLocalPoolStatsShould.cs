// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

/// <summary>
/// Unit tests for <see cref="ThreadLocalPoolStats"/>.
/// </summary>
/// <remarks>
/// Tests the thread-local pool statistics class.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Delivery")]
[Trait("Priority", "0")]
public sealed class ThreadLocalPoolStatsShould
{
	#region Constructor Tests

	[Fact]
	public void Constructor_CreatesInstanceWithDefaults()
	{
		// Arrange & Act
		var stats = new ThreadLocalPoolStats();

		// Assert
		_ = stats.ShouldNotBeNull();
		stats.CachedItems.ShouldBe(0);
		stats.MaxSize.ShouldBe(0);
	}

	#endregion

	#region CachedItems Property Tests

	[Fact]
	public void CachedItems_CanBeSet()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.CachedItems = 10;

		// Assert
		stats.CachedItems.ShouldBe(10);
	}

	[Fact]
	public void CachedItems_CanBeSetToZero()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats { CachedItems = 100 };

		// Act
		stats.CachedItems = 0;

		// Assert
		stats.CachedItems.ShouldBe(0);
	}

	[Fact]
	public void CachedItems_CanBeSetToNegativeValue()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.CachedItems = -1;

		// Assert - Negative values allowed (no validation)
		stats.CachedItems.ShouldBe(-1);
	}

	[Fact]
	public void CachedItems_CanBeSetToMaxInt()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.CachedItems = int.MaxValue;

		// Assert
		stats.CachedItems.ShouldBe(int.MaxValue);
	}

	#endregion

	#region MaxSize Property Tests

	[Fact]
	public void MaxSize_CanBeSet()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.MaxSize = 100;

		// Assert
		stats.MaxSize.ShouldBe(100);
	}

	[Fact]
	public void MaxSize_CanBeSetToZero()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats { MaxSize = 100 };

		// Act
		stats.MaxSize = 0;

		// Assert
		stats.MaxSize.ShouldBe(0);
	}

	[Fact]
	public void MaxSize_CanBeSetToNegativeValue()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.MaxSize = -1;

		// Assert - Negative values allowed (no validation)
		stats.MaxSize.ShouldBe(-1);
	}

	[Fact]
	public void MaxSize_CanBeSetToMaxInt()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats();

		// Act
		stats.MaxSize = int.MaxValue;

		// Assert
		stats.MaxSize.ShouldBe(int.MaxValue);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void CanBeInitializedWithObjectInitializer()
	{
		// Arrange & Act
		var stats = new ThreadLocalPoolStats
		{
			CachedItems = 50,
			MaxSize = 200,
		};

		// Assert
		stats.CachedItems.ShouldBe(50);
		stats.MaxSize.ShouldBe(200);
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanCalculateUtilizationPercentage()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats
		{
			CachedItems = 75,
			MaxSize = 100,
		};

		// Act
		var utilization = stats.MaxSize > 0 ? (double)stats.CachedItems / stats.MaxSize * 100 : 0;

		// Assert
		utilization.ShouldBe(75.0);
	}

	[Fact]
	public void CanCheckIfPoolIsFull()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats
		{
			CachedItems = 100,
			MaxSize = 100,
		};

		// Act
		var isFull = stats.CachedItems >= stats.MaxSize;

		// Assert
		isFull.ShouldBeTrue();
	}

	[Fact]
	public void CanCheckIfPoolIsEmpty()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats
		{
			CachedItems = 0,
			MaxSize = 100,
		};

		// Act
		var isEmpty = stats.CachedItems == 0;

		// Assert
		isEmpty.ShouldBeTrue();
	}

	[Fact]
	public void CanCalculateAvailableCapacity()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats
		{
			CachedItems = 30,
			MaxSize = 100,
		};

		// Act
		var available = stats.MaxSize - stats.CachedItems;

		// Assert
		available.ShouldBe(70);
	}

	#endregion

	#region Multiple Instance Tests

	[Fact]
	public void MultipleInstances_AreIndependent()
	{
		// Arrange
		var stats1 = new ThreadLocalPoolStats { CachedItems = 10, MaxSize = 100 };
		var stats2 = new ThreadLocalPoolStats { CachedItems = 50, MaxSize = 200 };

		// Act
		stats1.CachedItems = 20;

		// Assert
		stats1.CachedItems.ShouldBe(20);
		stats2.CachedItems.ShouldBe(50);
	}

	#endregion

	#region Reference Type Tests

	[Fact]
	public void IsReferenceType()
	{
		// Arrange
		var stats = new ThreadLocalPoolStats { CachedItems = 10 };

		// Act
		var reference = stats;
		reference.CachedItems = 20;

		// Assert - Both point to same object
		stats.CachedItems.ShouldBe(20);
	}

	#endregion
}
