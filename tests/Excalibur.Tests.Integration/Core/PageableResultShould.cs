using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Integration.Core;

public class PageableResultShould
{
	[Fact]
	public void CorrectlyPaginateResults()
	{
		var items = Enumerable.Range(1, 100).Select(i => $"Item {i}").ToList();
		var pageable = new PageableResult<string>(items, pageNumber: 2, pageSize: 10, totalItems: 100);

		pageable.Items.ShouldBe(items.Skip(10).Take(10));
		pageable.TotalPages.ShouldBe(10);
		pageable.HasNextPage.ShouldBeTrue();
		pageable.HasPreviousPage.ShouldBeTrue();
	}
}
