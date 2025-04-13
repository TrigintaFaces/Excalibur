using Excalibur.Core;

using FakeItEasy;

using Shouldly;

namespace Excalibur.Tests.Functional.Core;

public class PageableResultShould
{
	[Fact]
	public void PageItemsCorrectlyInFunctionalScenario()
	{
		// Arrange
		var items = Enumerable.Range(1, 100).Select((int i) => $"item{i}").ToList();

		// Act
		var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 10);

		// Assert
		result.Items.Count.ShouldBe(10);
		result.PageNumber.ShouldBe(2);
		result.PageSize.ShouldBe(10);
		result.TotalItems.ShouldBe(100);
		result.TotalPages.ShouldBe(10);

		result.Items.First().ShouldBe("item11");
		result.Items.Last().ShouldBe("item20");
	}

	[Fact]
	public void HandleSinglePageScenariosCorrectly()
	{
		// Arrange
		var items = Enumerable.Range(1, 5).Select((int i) => $"item{i}").ToList();

		// Act
		var result = new PageableResult<string>(items, pageNumber: 1, pageSize: 10);

		// Assert
		result.TotalPages.ShouldBe(1);
		result.IsFirstPage.ShouldBeTrue();
		result.IsLastPage.ShouldBeTrue();
		result.HasNextPage.ShouldBeFalse();
		result.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public void PaginateStringRecordsCorrectlyWithFakeItEasy()
	{
		// Arrange
		var items = A.Fake<IEnumerable<string>>();
		_ = A.CallTo(() => items.GetEnumerator()).Returns(
			new List<string>
			{
				"Item1",
				"Item2",
				"Item3",
				"Item4",
				"Item5",
				"Item6",
				"Item7",
				"Item8",
				"Item9",
				"Item10",
				"Item11",
				"Item12",
				"Item13",
				"Item14",
				"Item15"
			}.GetEnumerator());

		// Act
		var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 5);

		// Assert
		result.Items.Count.ShouldBe(5);
		result.PageNumber.ShouldBe(2);
		result.PageSize.ShouldBe(5);
		result.TotalItems.ShouldBe(15);
		result.TotalPages.ShouldBe(3);
		result.HasNextPage.ShouldBeTrue();
		result.HasPreviousPage.ShouldBeTrue();
	}

	[Fact]
	public void HandleEmptyRecordsGracefullyWithFakeItEasy()
	{
		// Arrange
		var items = A.Fake<IEnumerable<string>>();
		_ = A.CallTo(() => items.GetEnumerator()).Returns(new List<string>().GetEnumerator());

		// Act
		var result = new PageableResult<string>(items, pageNumber: 1, pageSize: 5);

		// Assert
		result.Items.Count.ShouldBe(0);
		result.PageNumber.ShouldBe(1);
		result.PageSize.ShouldBe(5);
		result.TotalItems.ShouldBe(0);
		result.TotalPages.ShouldBe(0);
		result.HasNextPage.ShouldBeFalse();
		result.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public void HandleInvalidPageNumberGracefullyWithFakeItEasy()
	{
		// Arrange
		var items = A.Fake<IEnumerable<string>>();
		_ = A.CallTo(() => items.GetEnumerator()).Returns(new List<string> { "Item1", "Item2" }.GetEnumerator());

		// Act & Assert
		Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 0, pageSize: 1)).Message
			.ShouldContain("pageNumber");
	}

	[Fact]
	public void HandleInvalidPageSizeGracefullyWithFakeItEasy()
	{
		// Arrange
		var items = A.Fake<IEnumerable<string>>();
		_ = A.CallTo(() => items.GetEnumerator()).Returns(new List<string> { "Item1", "Item2" }.GetEnumerator());

		// Act & Assert
		Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 1, pageSize: 0)).Message
			.ShouldContain("pageSize");
	}

	[Fact]
	public void ReturnCorrectItemsForPageWithFakeItEasy()
	{
		// Arrange
		var items = A.Fake<IEnumerable<string>>();
		_ = A.CallTo(() => items.GetEnumerator()).Returns(
			new List<string>
			{
				"Item1",
				"Item2",
				"Item3",
				"Item4",
				"Item5",
				"Item6",
				"Item7",
				"Item8",
				"Item9",
				"Item10"
			}.GetEnumerator());

		// Act
		var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 3);

		// Assert
		result.Items.Count.ShouldBe(3);
		result.Items[0].ShouldBe("Item4");
		result.Items[1].ShouldBe("Item5");
		result.Items[2].ShouldBe("Item6");
	}

	[Fact]
	public void PageItemsCorrectly()
	{
		var items = Enumerable.Range(1, 100).Select(i => $"item{i}").ToList();

		var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 10);

		result.Items.Count.ShouldBe(10);
		result.PageNumber.ShouldBe(2);
		result.PageSize.ShouldBe(10);
		result.TotalItems.ShouldBe(100);
		result.TotalPages.ShouldBe(10);
		result.Items.First().ShouldBe("item11");
		result.Items.Last().ShouldBe("item20");
		result.HasNextPage.ShouldBeTrue();
		result.HasPreviousPage.ShouldBeTrue();
		result.IsFirstPage.ShouldBeFalse();
		result.IsLastPage.ShouldBeFalse();
	}

	[Fact]
	public void SupportSinglePage()
	{
		var items = Enumerable.Range(1, 5).Select(i => $"item{i}").ToList();

		var result = new PageableResult<string>(items, pageNumber: 1, pageSize: 10);

		result.TotalPages.ShouldBe(1);
		result.IsFirstPage.ShouldBeTrue();
		result.IsLastPage.ShouldBeTrue();
		result.HasNextPage.ShouldBeFalse();
		result.HasPreviousPage.ShouldBeFalse();
	}

	[Fact]
	public void SupportEmptyItems()
	{
		var result = new PageableResult<string>([], pageNumber: 1, pageSize: 10);

		result.Items.ShouldBeEmpty();
		result.PageNumber.ShouldBe(1);
		result.PageSize.ShouldBe(10);
		result.TotalItems.ShouldBe(0);
		result.TotalPages.ShouldBe(0);
	}

	[Fact]
	public void ThrowIfItemsIsNull()
	{
		IEnumerable<string> items = null!;
		_ = Should.Throw<ArgumentNullException>(() => new PageableResult<string>(items));
	}

	[Fact]
	public void ThrowIfPageSizeWithoutPageNumber()
	{
		var items = new List<string> { "a", "b" };
		_ = Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageSize: 2));
	}

	[Fact]
	public void ThrowIfPageNumberWithoutPageSize()
	{
		var items = new List<string> { "a", "b" };
		_ = Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 1));
	}

	[Fact]
	public void ThrowIfPageNumberIsZero()
	{
		var items = new List<string> { "a", "b" };
		_ = Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 0, pageSize: 2));
	}

	[Fact]
	public void ThrowIfPageSizeIsZero()
	{
		var items = new List<string> { "a", "b" };
		Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 1, pageSize: 0)).Message
			.ShouldContain("pageSize");
	}

	[Fact]
	public void ThrowIfTotalItemsLessThanItemCount()
	{
		var items = Enumerable.Range(1, 5).Select(i => $"item{i}").ToList();
		_ = Should.Throw<ArgumentException>(() => new PageableResult<string>(items, 1, 5, totalItems: 3));
	}

	[Fact]
	public void IndexerReturnsCorrectItem()
	{
		var result = new PageableResult<string>(new List<string> { "a", "b", "c" });

		result[1].ShouldBe("b");
	}

	[Fact]
	public void EnumeratorWorks()
	{
		var result = new PageableResult<string>(new List<string> { "a", "b", "c" });

		_ = result.GetEnumerator().ShouldNotBeNull();
		_ = result.GetEnumerator().ShouldBeAssignableTo<IEnumerator<string>>();
	}

	[Fact]
	public void LastPageIsCorrect()
	{
		var items = Enumerable.Range(1, 15).Select(i => $"item{i}").ToList();
		var result = new PageableResult<string>(items, pageNumber: 3, pageSize: 5);

		result.IsLastPage.ShouldBeTrue();
		result.HasNextPage.ShouldBeFalse();
		result.HasPreviousPage.ShouldBeTrue();
	}
}
