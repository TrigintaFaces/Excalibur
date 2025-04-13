using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class CursorPageResultShould
{
	[Fact]
	public void InitializePropertiesCorrectly()
	{
		// Arrange
		var items = new[] { "A", "B", "C" };
		var pageSize = 3;
		var totalRecords = 10;

		// Act
		var result = new CursorPageResult<string>(items, pageSize, totalRecords);

		// Assert
		result.Items.ShouldBe(items);
		result.PageSize.ShouldBe(pageSize);
		result.TotalRecords.ShouldBe(totalRecords);
	}

	[Theory]
	[InlineData(0, 10, 0)]
	[InlineData(3, 10, 4)]
	[InlineData(4, 16, 4)]
	[InlineData(5, 21, 5)]
	public void CalculateTotalPagesCorrectly(int pageSize, long totalRecords, int expectedTotalPages)
	{
		// Arrange
		var items = Enumerable.Range(1, (int)Math.Min(totalRecords, pageSize)).Select(i => $"Item{i}");

		// Act
		var result = new CursorPageResult<string>(items, pageSize, totalRecords);

		// Assert
		result.TotalPages.ShouldBe(expectedTotalPages);
	}

	[Fact]
	public void ReturnCorrectValuesFromDeconstruct()
	{
		// Arrange
		var items = new[] { "X", "Y" };
		var pageSize = 2;
		var totalRecords = 5;

		var result = new CursorPageResult<string>(items, pageSize, totalRecords);

		// Act
		result.Deconstruct(out var deconstructedItems, out var deconstructedPageSize, out var deconstructedTotalRecords);

		// Assert
		deconstructedItems.ShouldBe(items);
		deconstructedPageSize.ShouldBe(pageSize);
		deconstructedTotalRecords.ShouldBe(totalRecords);
	}

	[Fact]
	public void HandleEmptyItemsCorrectly()
	{
		// Arrange
		var result = new CursorPageResult<string>([], 10, 0);

		// Assert
		result.Items.ShouldBeEmpty();
		result.TotalPages.ShouldBe(0);
	}
}
