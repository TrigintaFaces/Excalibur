using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class SkipBehaviorShould
{
	[Theory]
	[InlineData(SkipBehavior.Silent, 0)]
	[InlineData(SkipBehavior.LogOnly, 1)]
	[InlineData(SkipBehavior.ReturnSkippedResult, 2)]
	public void HaveExpectedValues(SkipBehavior behavior, int expected)
	{
		((int)behavior).ShouldBe(expected);
	}

	[Fact]
	public void HaveThreeMembers()
	{
		Enum.GetValues<SkipBehavior>().Length.ShouldBe(3);
	}
}
