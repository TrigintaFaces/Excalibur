// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Inbox;

/// <summary>
/// Unit tests for <see cref="InboxStatistics"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Inbox")]
[Trait("Priority", "0")]
public sealed class InboxStatisticsShould
{
	#region Default Value Tests

	[Fact]
	public void Default_TotalEntries_IsZero()
	{
		// Arrange & Act
		var stats = new InboxStatistics();

		// Assert
		stats.TotalEntries.ShouldBe(0);
	}

	[Fact]
	public void Default_ProcessedEntries_IsZero()
	{
		// Arrange & Act
		var stats = new InboxStatistics();

		// Assert
		stats.ProcessedEntries.ShouldBe(0);
	}

	[Fact]
	public void Default_FailedEntries_IsZero()
	{
		// Arrange & Act
		var stats = new InboxStatistics();

		// Assert
		stats.FailedEntries.ShouldBe(0);
	}

	[Fact]
	public void Default_PendingEntries_IsZero()
	{
		// Arrange & Act
		var stats = new InboxStatistics();

		// Assert
		stats.PendingEntries.ShouldBe(0);
	}

	#endregion

	#region Object Initializer Tests

	[Fact]
	public void ObjectInitializer_SetsAllProperties()
	{
		// Act
		var stats = new InboxStatistics
		{
			TotalEntries = 100,
			ProcessedEntries = 75,
			FailedEntries = 5,
			PendingEntries = 20,
		};

		// Assert
		stats.TotalEntries.ShouldBe(100);
		stats.ProcessedEntries.ShouldBe(75);
		stats.FailedEntries.ShouldBe(5);
		stats.PendingEntries.ShouldBe(20);
	}

	#endregion

	#region Record Equality Tests

	[Fact]
	public void Equality_WithSameValues_ReturnsTrue()
	{
		// Arrange
		var stats1 = new InboxStatistics
		{
			TotalEntries = 100,
			ProcessedEntries = 75,
			FailedEntries = 5,
			PendingEntries = 20,
		};

		var stats2 = new InboxStatistics
		{
			TotalEntries = 100,
			ProcessedEntries = 75,
			FailedEntries = 5,
			PendingEntries = 20,
		};

		// Act & Assert
		stats1.ShouldBe(stats2);
	}

	[Fact]
	public void Equality_WithDifferentValues_ReturnsFalse()
	{
		// Arrange
		var stats1 = new InboxStatistics { TotalEntries = 100 };
		var stats2 = new InboxStatistics { TotalEntries = 200 };

		// Act & Assert
		stats1.ShouldNotBe(stats2);
	}

	#endregion

	#region Record With Expression Tests

	[Fact]
	public void With_CreatesCopyWithModifiedProperty()
	{
		// Arrange
		var original = new InboxStatistics
		{
			TotalEntries = 100,
			ProcessedEntries = 75,
			FailedEntries = 5,
			PendingEntries = 20,
		};

		// Act
		var modified = original with { ProcessedEntries = 80 };

		// Assert
		modified.TotalEntries.ShouldBe(100);
		modified.ProcessedEntries.ShouldBe(80);
		modified.FailedEntries.ShouldBe(5);
		modified.PendingEntries.ShouldBe(20);
		modified.ShouldNotBeSameAs(original);
	}

	#endregion

	#region Edge Cases

	[Fact]
	public void CanHaveNegativeValues()
	{
		// Act
		var stats = new InboxStatistics
		{
			TotalEntries = -1,
			ProcessedEntries = -1,
			FailedEntries = -1,
			PendingEntries = -1,
		};

		// Assert - The type allows negative values (validation is separate concern)
		stats.TotalEntries.ShouldBe(-1);
	}

	[Fact]
	public void CanHaveMaxIntValues()
	{
		// Act
		var stats = new InboxStatistics
		{
			TotalEntries = int.MaxValue,
			ProcessedEntries = int.MaxValue,
			FailedEntries = int.MaxValue,
			PendingEntries = int.MaxValue,
		};

		// Assert
		stats.TotalEntries.ShouldBe(int.MaxValue);
	}

	#endregion
}
