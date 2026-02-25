// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Outbox.Inbox;

namespace Excalibur.Outbox.Tests;

/// <summary>
/// Unit tests for <see cref="DeduplicatorStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class DeduplicatorStatisticsShould : UnitTestBase
{
	#region Default Values Tests

	[Fact]
	public void DefaultConstructor_TotalEntries_IsZero()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics();

		// Assert
		stats.TotalEntries.ShouldBe(0);
	}

	[Fact]
	public void DefaultConstructor_ExpiredEntries_IsZero()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics();

		// Assert
		stats.ExpiredEntries.ShouldBe(0);
	}

	[Fact]
	public void DefaultConstructor_OldestEntry_IsDefault()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics();

		// Assert
		stats.OldestEntry.ShouldBe(default);
	}

	[Fact]
	public void DefaultConstructor_NewestEntry_IsDefault()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics();

		// Assert
		stats.NewestEntry.ShouldBe(default);
	}

	[Fact]
	public void DefaultConstructor_AverageAge_IsZero()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics();

		// Assert
		stats.AverageAge.ShouldBe(TimeSpan.Zero);
	}

	#endregion

	#region Property Initialization Tests

	[Fact]
	public void TotalEntries_CanBeInitialized()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics { TotalEntries = 100 };

		// Assert
		stats.TotalEntries.ShouldBe(100);
	}

	[Fact]
	public void ExpiredEntries_CanBeInitialized()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics { ExpiredEntries = 25 };

		// Assert
		stats.ExpiredEntries.ShouldBe(25);
	}

	[Fact]
	public void OldestEntry_CanBeInitialized()
	{
		// Arrange
		var oldest = DateTimeOffset.UtcNow.AddDays(-7);

		// Act
		var stats = new DeduplicatorStatistics { OldestEntry = oldest };

		// Assert
		stats.OldestEntry.ShouldBe(oldest);
	}

	[Fact]
	public void NewestEntry_CanBeInitialized()
	{
		// Arrange
		var newest = DateTimeOffset.UtcNow;

		// Act
		var stats = new DeduplicatorStatistics { NewestEntry = newest };

		// Assert
		stats.NewestEntry.ShouldBe(newest);
	}

	[Fact]
	public void AverageAge_CanBeInitialized()
	{
		// Arrange
		var avgAge = TimeSpan.FromHours(2);

		// Act
		var stats = new DeduplicatorStatistics { AverageAge = avgAge };

		// Assert
		stats.AverageAge.ShouldBe(avgAge);
	}

	#endregion

	#region Full Statistics Tests

	[Fact]
	public void FullStatistics_AllPropertiesSet()
	{
		// Arrange
		var oldest = DateTimeOffset.UtcNow.AddDays(-7);
		var newest = DateTimeOffset.UtcNow;
		var avgAge = TimeSpan.FromDays(3.5);

		// Act
		var stats = new DeduplicatorStatistics
		{
			TotalEntries = 1000,
			ExpiredEntries = 150,
			OldestEntry = oldest,
			NewestEntry = newest,
			AverageAge = avgAge
		};

		// Assert
		stats.TotalEntries.ShouldBe(1000);
		stats.ExpiredEntries.ShouldBe(150);
		stats.OldestEntry.ShouldBe(oldest);
		stats.NewestEntry.ShouldBe(newest);
		stats.AverageAge.ShouldBe(avgAge);
	}

	[Fact]
	public void Statistics_WithZeroEntries_IsValid()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics
		{
			TotalEntries = 0,
			ExpiredEntries = 0,
			OldestEntry = default,
			NewestEntry = default,
			AverageAge = TimeSpan.Zero
		};

		// Assert
		stats.TotalEntries.ShouldBe(0);
		stats.ExpiredEntries.ShouldBe(0);
	}

	[Fact]
	public void Statistics_ExpiredCanEqualTotal()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics
		{
			TotalEntries = 100,
			ExpiredEntries = 100
		};

		// Assert - all entries can be expired
		stats.ExpiredEntries.ShouldBe(stats.TotalEntries);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void Statistics_WithMaxValues_IsValid()
	{
		// Arrange & Act
		var stats = new DeduplicatorStatistics
		{
			TotalEntries = int.MaxValue,
			ExpiredEntries = int.MaxValue,
			OldestEntry = DateTimeOffset.MinValue,
			NewestEntry = DateTimeOffset.MaxValue,
			AverageAge = TimeSpan.MaxValue
		};

		// Assert
		stats.TotalEntries.ShouldBe(int.MaxValue);
		stats.ExpiredEntries.ShouldBe(int.MaxValue);
	}

	[Fact]
	public void Statistics_WithNegativeTimeSpan_IsValid()
	{
		// Arrange & Act - TimeSpan can be negative
		var stats = new DeduplicatorStatistics
		{
			AverageAge = TimeSpan.FromHours(-1)
		};

		// Assert
		stats.AverageAge.ShouldBe(TimeSpan.FromHours(-1));
	}

	#endregion
}
