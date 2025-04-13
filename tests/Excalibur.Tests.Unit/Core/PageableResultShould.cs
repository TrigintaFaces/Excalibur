using System.Globalization;

using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class PageableResultShould
{
	[Fact]
	public void InitializeWithValidArguments()
	{
		// Arrange
		var items = new List<string> { "item1", "item2", "item3" };

		// Act
		var result = new PageableResult<string>(items, pageNumber: 1, pageSize: 2, totalItems: 3);

		// Assert
		result.Items.Count.ShouldBe(2);
		result.Items.ShouldContain("item1");
		result.Items.ShouldContain("item2");
		result.Items.ShouldNotContain("item3");
		result.PageNumber.ShouldBe(1);
		result.PageSize.ShouldBe(2);
		result.TotalItems.ShouldBe(3);
		result.TotalPages.ShouldBe(2);
	}

	[Fact]
	public void PaginateCorrectly()
	{
		// Arrange
		var items = Enumerable.Range(1, 100).Select((int x) => x.ToString(CultureInfo.CurrentCulture)).ToList();

		// Act
		var result = new PageableResult<string>(items, pageNumber: 2, pageSize: 10);

		// Assert
		result.Items.Count.ShouldBe(10);
		result.PageNumber.ShouldBe(2);
		result.HasNextPage.ShouldBeTrue();
		result.HasPreviousPage.ShouldBeTrue();
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfItemsIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new PageableResult<string>(null!));
	}

	[Fact]
	public void ThrowArgumentExceptionForInvalidPageSize()
	{
		var items = new List<string> { "item1", "item2" };

		Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 1, pageSize: 0)).Message
			.ShouldContain("pageSize");
	}

	[Fact]
	public void ThrowArgumentExceptionForInvalidPageNumber()
	{
		var items = new List<string> { "item1", "item2" };

		Should.Throw<ArgumentException>(() => new PageableResult<string>(items, pageNumber: 0, pageSize: 2)).Message
			.ShouldContain("pageNumber");
	}

	[Fact]
	public void CalculateTotalPagesCorrectly()
	{
		var items = new List<string> { "item1", "item2", "item3", "item4" };

		var result = new PageableResult<string>(items, pageNumber: 1, pageSize: 2);

		result.TotalPages.ShouldBe(2);
	}

	[Fact]
	public void IdentifyFirstAndLastPageCorrectly()
	{
		var items = new List<string> { "item1", "item2", "item3", "item4" };

		var firstPage = new PageableResult<string>(items, pageNumber: 1, pageSize: 2);
		var lastPage = new PageableResult<string>(items, pageNumber: 2, pageSize: 2);

		firstPage.IsFirstPage.ShouldBeTrue();
		firstPage.IsLastPage.ShouldBeFalse();

		lastPage.IsFirstPage.ShouldBeFalse();
		lastPage.IsLastPage.ShouldBeTrue();
	}

	[Fact]
	public void DetermineHasNextAndPreviousPagesCorrectly()
	{
		var items = new List<string> { "item1", "item2", "item3" };

		var firstPage = new PageableResult<string>(items, pageNumber: 1, pageSize: 2);
		var secondPage = new PageableResult<string>(items, pageNumber: 2, pageSize: 2);

		firstPage.HasNextPage.ShouldBeTrue();
		firstPage.HasPreviousPage.ShouldBeFalse();

		secondPage.HasNextPage.ShouldBeFalse();
		secondPage.HasPreviousPage.ShouldBeTrue();
	}
}
