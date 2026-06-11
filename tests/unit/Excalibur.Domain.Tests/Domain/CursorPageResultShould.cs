// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="CursorPagedResult{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class CursorPagedResultShould
{
	[Fact]
	public void Constructor_SetsAllProperties()
	{
		// Arrange
		var items = new[] { "item1", "item2", "item3" };
		const int pageSize = 10;
		const long totalRecords = 100;

		// Act
		var result = new CursorPagedResult<string>(items, pageSize, totalRecords);

		// Assert
		result.Items.ShouldBe(items);
		result.PageSize.ShouldBe(pageSize);
		result.TotalRecords.ShouldBe(totalRecords);
	}

	[Fact]
	public void TotalPages_CalculatesCorrectly()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1, 2, 3], 10, 95);

		// Assert - 95 records / 10 per page = 10 pages (rounded up)
		result.TotalPages.ShouldBe(10);
	}

	[Fact]
	public void TotalPages_ReturnsZero_WhenPageSizeIsZero()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1, 2], 0, 100);

		// Assert
		result.TotalPages.ShouldBe(0);
	}

	[Fact]
	public void TotalPages_CalculatesCorrectly_ForExactDivision()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100);

		// Assert - 100 records / 10 per page = 10 pages exactly
		result.TotalPages.ShouldBe(10);
	}

	[Fact]
	public void TotalPages_CalculatesCorrectly_WithRemainder()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 101);

		// Assert - 101 records / 10 per page = 11 pages (rounded up)
		result.TotalPages.ShouldBe(11);
	}

	[Fact]
	public void Deconstruct_ReturnsAllComponents()
	{
		// Arrange
		var items = new[] { "a", "b" };
		const int pageSize = 5;
		const long totalRecords = 50;
		var result = new CursorPagedResult<string>(items, pageSize, totalRecords);

		// Act
		result.Deconstruct(out var deconstructedItems, out var deconstructedPageSize, out var deconstructedTotalRecords, out _);

		// Assert
		deconstructedItems.ShouldBe(items);
		deconstructedPageSize.ShouldBe(pageSize);
		deconstructedTotalRecords.ShouldBe(totalRecords);
	}

	[Fact]
	public void Items_CanBeModifiedViaInit()
	{
		// Arrange
		var originalItems = new[] { 1, 2, 3 };
		var newItems = new[] { 4, 5, 6 };

		// Act
		var result = new CursorPagedResult<int>(originalItems, 10, 100) { Items = newItems };

		// Assert
		result.Items.ShouldBe(newItems);
	}

	[Fact]
	public void PageSize_CanBeModifiedViaInit()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100) { PageSize = 25 };

		// Assert
		result.PageSize.ShouldBe(25);
	}

	[Fact]
	public void TotalRecords_CanBeModifiedViaInit()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100) { TotalRecords = 500 };

		// Assert
		result.TotalRecords.ShouldBe(500);
	}

	[Fact]
	public void WorksWithComplexTypes()
	{
		// Arrange
		var items = new[]
		{
			new TestItem { Id = 1, Name = "First" },
			new TestItem { Id = 2, Name = "Second" },
		};

		// Act
		var result = new CursorPagedResult<TestItem>(items, 10, 2);

		// Assert
		result.Items.Count().ShouldBe(2);
		result.Items.First().Name.ShouldBe("First");
	}

	[Fact]
	public void WorksWithEmptyCollection()
	{
		// Arrange & Act
		var result = new CursorPagedResult<string>([], 10, 0);

		// Assert
		result.Items.ShouldBeEmpty();
		result.TotalRecords.ShouldBe(0);
		result.TotalPages.ShouldBe(0);
	}

	[Fact]
	public void NextCursor_DefaultsToNull_AndHasMoreIsFalse()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100);

		// Assert
		result.NextCursor.ShouldBeNull();
		result.HasMore.ShouldBeFalse();
	}

	[Fact]
	public void NextCursor_SetsValue_AndHasMoreIsTrue()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100, nextCursor: "fwd-token");

		// Assert
		result.NextCursor.ShouldBe("fwd-token");
		result.HasMore.ShouldBeTrue();
	}

	[Fact]
	public void PreviousCursor_DefaultsToNull_AndHasPreviousIsFalse()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100, nextCursor: "fwd-token");

		// Assert - previousCursor not supplied
		result.PreviousCursor.ShouldBeNull();
		result.HasPrevious.ShouldBeFalse();
	}

	[Fact]
	public void PreviousCursor_SetsValue_AndHasPreviousIsTrue()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100, nextCursor: "fwd-token", previousCursor: "back-token");

		// Assert
		result.PreviousCursor.ShouldBe("back-token");
		result.HasPrevious.ShouldBeTrue();
	}

	[Fact]
	public void ForwardAndBackwardCursors_AreIndependent()
	{
		// Arrange & Act - a middle page has both directions available
		var middle = new CursorPagedResult<int>([1], 10, 100, nextCursor: "n", previousCursor: "p");

		// First page: backward null, forward set
		var first = new CursorPagedResult<int>([1], 10, 100, nextCursor: "n");

		// Last page: forward null, backward set
		var last = new CursorPagedResult<int>([1], 10, 100, nextCursor: null, previousCursor: "p");

		// Assert
		middle.HasMore.ShouldBeTrue();
		middle.HasPrevious.ShouldBeTrue();

		first.HasMore.ShouldBeTrue();
		first.HasPrevious.ShouldBeFalse();

		last.HasMore.ShouldBeFalse();
		last.HasPrevious.ShouldBeTrue();
	}

	[Fact]
	public void PreviousCursor_CanBeModifiedViaInit()
	{
		// Arrange & Act
		var result = new CursorPagedResult<int>([1], 10, 100) { PreviousCursor = "back-token" };

		// Assert
		result.PreviousCursor.ShouldBe("back-token");
		result.HasPrevious.ShouldBeTrue();
	}

	private sealed class TestItem
	{
		public int Id { get; init; }
		public string Name { get; init; } = string.Empty;
	}
}
