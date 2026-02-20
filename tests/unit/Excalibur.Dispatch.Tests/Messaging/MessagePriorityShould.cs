using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessagePriorityShould
{
	[Theory]
	[InlineData(MessagePriority.Low, 0)]
	[InlineData(MessagePriority.Normal, 1)]
	[InlineData(MessagePriority.High, 2)]
	[InlineData(MessagePriority.Critical, 3)]
	public void HaveExpectedValues(MessagePriority priority, int expected)
	{
		((int)priority).ShouldBe(expected);
	}

	[Fact]
	public void HaveFourMembers()
	{
		Enum.GetValues<MessagePriority>().Length.ShouldBe(4);
	}
}
