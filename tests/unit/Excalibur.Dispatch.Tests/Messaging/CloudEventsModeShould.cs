using Excalibur.Dispatch.CloudEvents;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CloudEventsModeShould
{
	[Theory]
	[InlineData(CloudEventMode.Structured, 0)]
	[InlineData(CloudEventMode.Binary, 1)]
	public void HaveExpectedValues(CloudEventMode mode, int expected)
	{
		((int)mode).ShouldBe(expected);
	}

	[Fact]
	public void HaveTwoMembers()
	{
		Enum.GetValues<CloudEventMode>().Length.ShouldBe(2);
	}
}
