using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class CursorPageResultIntegrationShould
{
	[Fact]
	public void CalculateTotalPagesCorrectly()
	{
		// Arrange
		var testData = new List<TestItem> { new(1, "Item 1"), new(2, "Item 2"), new(3, "Item 3") };

		// Act - Even division
		var result1 = new CursorPageResult<TestItem>(testData, 3, 9);

		// Assert
		result1.TotalPages.ShouldBe(3);

		// Act - Uneven division (requires rounding up)
		var result2 = new CursorPageResult<TestItem>(testData, 4, 10);

		// Assert
		result2.TotalPages.ShouldBe(3);
	}

	[Fact]
	public void HandleZeroPageSize()
	{
		// Arrange
		var testData = new List<TestItem> { new(1, "Item 1"), new(2, "Item 2") };

		// Act
		var result = new CursorPageResult<TestItem>(testData, 0, 10);

		// Assert
		result.TotalPages.ShouldBe(0);
	}

	[Fact]
	public void ReturnProvidedItemsCollection()
	{
		// Arrange
		var testData = new List<TestItem> { new(1, "Item 1"), new(2, "Item 2"), new(3, "Item 3") };

		// Act
		var result = new CursorPageResult<TestItem>(testData, 10, 100);

		// Assert
		result.Items.ShouldBeSameAs(testData);
	}

	[Fact]
	public void DeconstructCorrectly()
	{
		// Arrange
		var testData = new List<TestItem> { new(1, "Item 1"), new(2, "Item 2") };
		var pageSize = 10;
		var totalRecords = 50L;

		var result = new CursorPageResult<TestItem>(testData, pageSize, totalRecords);

		// Act
		var (items, size, records) = result;

		// Assert
		items.ShouldBeSameAs(testData);
		size.ShouldBe(pageSize);
		records.ShouldBe(totalRecords);
	}

	[Fact]
	public void IntegrateWithTestCursorPageRequest()
	{
		// Arrange
		var pageSize = 20;
		var cursor = "abc123";
		var request = new TestCursorPageRequest(pageSize, cursor, PageNavigation.Next);

		var testData = new List<TestItem> { new(1, "Item 1"), new(2, "Item 2") };

		// Act - Create the result using the request params
		var (size, navigation, requestCursor) = request;
		var result = new CursorPageResult<TestItem>(testData, size, 50);

		// Assert
		requestCursor.ShouldBe(cursor);
		result.PageSize.ShouldBe(pageSize);
		result.Items.Count().ShouldBe(2);
	}

	private sealed class TestItem(int id, string name)
	{
		public int Id { get; } = id;
		public string Name { get; } = name;
	}

	private sealed class TestCursorPageRequest(int pageSize, string? cursor, PageNavigation navigation)
		: CursorPageRequest<string>(pageSize, navigation)
	{
		protected override string? GetCursor() => cursor;
	}
}
