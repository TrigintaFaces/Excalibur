using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventsModeShould
{
	[Theory]
	[InlineData(CloudEventsMode.None, 0)]
	[InlineData(CloudEventsMode.Structured, 1)]
	[InlineData(CloudEventsMode.Binary, 2)]
	public void HaveExpectedValues(CloudEventsMode mode, int expected)
	{
		((int)mode).ShouldBe(expected);
	}

	[Fact]
	public void HaveThreeMembers()
	{
		Enum.GetValues<CloudEventsMode>().Length.ShouldBe(3);
	}
}
