using Excalibur.Core;

using Shouldly;

namespace Excalibur.Tests.Unit.Core;

public class CursorPageRequestShould
{
	[Fact]
	public void SetPropertiesViaConstructor()
	{
		var request = new TestCursorPageRequest(25, PageNavigation.Next, "cursor123");

		request.PageSize.ShouldBe(25);
		request.Navigation.ShouldBe(PageNavigation.Next);
	}

	[Fact]
	public void AllowPropertyMutation()
	{
		var request = new TestCursorPageRequest(10, PageNavigation.First, "abc");

		request.PageSize = 50;
		request.Navigation = PageNavigation.Last;

		request.PageSize.ShouldBe(50);
		request.Navigation.ShouldBe(PageNavigation.Last);
	}

	[Fact]
	public void DeconstructCorrectly()
	{
		var request = new TestCursorPageRequest(20, PageNavigation.Previous, "test-cursor");

		var (pageSize, navigation, cursor) = request;

		pageSize.ShouldBe(20);
		navigation.ShouldBe(PageNavigation.Previous);
		cursor.ShouldBe("test-cursor");
	}

	[Fact]
	public void ReturnNullCursorWhenNotSet()
	{
		var request = new TestCursorPageRequest(15, PageNavigation.First, null);

		var (_, _, cursor) = request;

		cursor.ShouldBeNull();
	}

	private sealed class TestCursorPageRequest(int pageSize, PageNavigation navigation, string? cursor)
		: CursorPageRequest<string>(pageSize, navigation)
	{
		protected override string? GetCursor() => cursor;
	}
}
