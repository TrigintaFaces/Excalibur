using Excalibur.Outbox;

namespace Excalibur.Outbox.Tests.Configuration;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class OutboxPresetShould
{
	[Fact]
	public void DefineHighThroughputValue()
	{
		OutboxPreset.HighThroughput.ShouldBe((OutboxPreset)0);
	}

	[Fact]
	public void DefineBalancedValue()
	{
		OutboxPreset.Balanced.ShouldBe((OutboxPreset)1);
	}

	[Fact]
	public void DefineHighReliabilityValue()
	{
		OutboxPreset.HighReliability.ShouldBe((OutboxPreset)2);
	}

	[Fact]
	public void DefineCustomValue()
	{
		OutboxPreset.Custom.ShouldBe((OutboxPreset)3);
	}

	[Fact]
	public void HaveFourMembers()
	{
		Enum.GetValues<OutboxPreset>().Length.ShouldBe(4);
	}
}
