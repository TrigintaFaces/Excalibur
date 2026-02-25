using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class InboxPresetShould
{
	[Fact]
	public void DefineHighThroughputValue()
	{
		InboxPreset.HighThroughput.ShouldBe((InboxPreset)0);
	}

	[Fact]
	public void DefineBalancedValue()
	{
		InboxPreset.Balanced.ShouldBe((InboxPreset)1);
	}

	[Fact]
	public void DefineHighReliabilityValue()
	{
		InboxPreset.HighReliability.ShouldBe((InboxPreset)2);
	}

	[Fact]
	public void DefineCustomValue()
	{
		InboxPreset.Custom.ShouldBe((InboxPreset)3);
	}

	[Fact]
	public void HaveFourMembers()
	{
		Enum.GetValues<InboxPreset>().Length.ShouldBe(4);
	}
}
