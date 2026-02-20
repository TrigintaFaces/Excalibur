// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Tests.Domain;

/// <summary>
/// Unit tests for <see cref="PageableResult{T}"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class PageableResultShould
{
	[Fact]
	public void Constructor_WithItemsOnly_UsesDefaults()
	{
		// Arrange
		var items = new[] { new TestEntity("1"), new TestEntity("2") };

		// Act
		var result = new PageableResult<TestEntity>(items);

		// Assert
		result.Items.Count.ShouldBe(2);
		result.PageNumber.ShouldBe(1);
		result.PageSize.ShouldBe(2);
		result.TotalItems.ShouldBe(2);
	}

	[Fact]
	public void Constructor_WithAllParameters_SetsProperties()
	{
		// Arrange - pass pre-paged items (page 2 of 10)
		var items = CreateItems(10);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 2, pageSize: 10, totalItems: 50);

		// Assert
		result.PageNumber.ShouldBe(2);
		result.PageSize.ShouldBe(10);
		result.TotalItems.ShouldBe(50);
		result.Items.Count.ShouldBe(10);
	}

	[Fact]
	public void Constructor_ThrowsArgumentNullException_WhenItemsIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() =>
			new PageableResult<TestEntity>(null!));
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageNumberIsZero()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: 0, pageSize: 10));
		exception.ParamName.ShouldBe("pageNumber");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageNumberIsNegative()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: -1, pageSize: 10));
		exception.ParamName.ShouldBe("pageNumber");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageSizeIsZero()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 0));
		exception.ParamName.ShouldBe("pageSize");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageSizeIsNegative()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: -1));
		exception.ParamName.ShouldBe("pageSize");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageNumberProvidedWithoutPageSize()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: 2, pageSize: null));
		exception.ParamName.ShouldBe("pageSize");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenPageSizeProvidedWithoutPageNumber()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, pageNumber: null, pageSize: 10));
		exception.ParamName.ShouldBe("pageNumber");
	}

	[Fact]
	public void Constructor_ThrowsArgumentException_WhenTotalItemsLessThanItemCount()
	{
		// Arrange
		var items = CreateItems(10);

		// Act & Assert
		var exception = Should.Throw<ArgumentException>(() =>
			new PageableResult<TestEntity>(items, totalItems: 5)); // 5 < 10 items
		exception.ParamName.ShouldBe("totalItems");
	}

	[Fact]
	public void TotalPages_CalculatesCorrectly()
	{
		// Arrange
		var items = CreateItems(10);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 10, totalItems: 95);

		// Assert - 95 / 10 = 10 pages (rounded up)
		result.TotalPages.ShouldBe(10);
	}

	[Fact]
	public void TotalPages_ReturnsZero_WhenPageSizeIsZero()
	{
		// Arrange
		var items = Array.Empty<TestEntity>();

		// Act
		var result = new PageableResult<TestEntity>(items);

		// Assert - empty collection has 0 page size
		result.TotalPages.ShouldBe(0);
	}

	[Fact]
	public void HasNextPage_ReturnsTrue_WhenNotOnLastPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 10, totalItems: 100);

		// Assert
		result.HasNextPage.ShouldBeTrue();
	}

	[Fact]
	public void HasNextPage_ReturnsFalse_WhenOnLastPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 10, pageSize: 10, totalItems: 100);

		// Assert
		result.HasNextPage.ShouldBeFalse();
	}

	[Fact]
	public void HasPreviousPage_ReturnsTrue_WhenNotOnFirstPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 2, pageSize: 10, totalItems: 100);

		// Assert
		result.HasPreviousPage.ShouldBeTrue();
	}

	[Fact]
	public void HasPreviousPage_ReturnsFalse_WhenOnFirstPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 10, totalItems: 100);

		// Assert
		result.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public void IsFirstPage_ReturnsTrue_WhenPageNumberIsOne()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 10, totalItems: 100);

		// Assert
		result.IsFirstPage.ShouldBeTrue();
	}

	[Fact]
	public void IsFirstPage_ReturnsFalse_WhenPageNumberIsNotOne()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 5, pageSize: 10, totalItems: 100);

		// Assert
		result.IsFirstPage.ShouldBeFalse();
	}

	[Fact]
	public void IsLastPage_ReturnsTrue_WhenOnLastPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 10, pageSize: 10, totalItems: 100);

		// Assert
		result.IsLastPage.ShouldBeTrue();
	}

	[Fact]
	public void IsLastPage_ReturnsFalse_WhenNotOnLastPage()
	{
		// Arrange
		var items = CreateItems(100);

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 1, pageSize: 10, totalItems: 100);

		// Assert
		result.IsLastPage.ShouldBeFalse();
	}

	[Fact]
	public void Indexer_ReturnsCorrectItem()
	{
		// Arrange
		var items = new[]
		{
			new TestEntity("first"),
			new TestEntity("second"),
			new TestEntity("third"),
		};

		// Act
		var result = new PageableResult<TestEntity>(items);

		// Assert
		result[0].Id.ShouldBe("first");
		result[1].Id.ShouldBe("second");
		result[2].Id.ShouldBe("third");
	}

	[Fact]
	public void GetEnumerator_AllowsIteration()
	{
		// Arrange
		var items = CreateItems(5);
		var result = new PageableResult<TestEntity>(items);

		// Act
		var count = 0;
		foreach (var item in result)
		{
			count++;
			item.ShouldNotBeNull();
		}

		// Assert
		count.ShouldBe(5);
	}

	[Fact]
	public void Items_ContainsCorrectPage()
	{
		// Arrange - pass pre-paged items (page 3: items 21-30)
		var items = Enumerable.Range(21, 10).Select(i => new TestEntity(i.ToString())).ToList();

		// Act
		var result = new PageableResult<TestEntity>(items, pageNumber: 3, pageSize: 10, totalItems: 50);

		// Assert
		result.Items.Count.ShouldBe(10);
		result.Items.First().Id.ShouldBe("21");
		result.Items.Last().Id.ShouldBe("30");
	}

	private static IList<TestEntity> CreateItems(int count)
	{
		return Enumerable.Range(1, count)
			.Select(i => new TestEntity(i.ToString()))
			.ToList();
	}

	private sealed class TestEntity
	{
		public TestEntity(string id) => Id = id;
		public string Id { get; }
	}
}
