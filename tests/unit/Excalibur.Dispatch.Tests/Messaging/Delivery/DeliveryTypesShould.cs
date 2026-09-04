using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait(TraitNames.Component, TestComponents.Core)]
public sealed class DeliveryTypesShould
{
	// --- MessageFlags ---

	[Fact]
	public void MessageFlags_HaveCorrectValues()
	{
		((byte)MessageFlags.None).ShouldBe((byte)0);
		((byte)MessageFlags.Compressed).ShouldBe((byte)1);
		((byte)MessageFlags.Encrypted).ShouldBe((byte)2);
		((byte)MessageFlags.Persistent).ShouldBe((byte)4);
		((byte)MessageFlags.HighPriority).ShouldBe((byte)8);
		((byte)MessageFlags.Validated).ShouldBe((byte)16);
	}

	[Fact]
	public void MessageFlags_SupportCombination()
	{
		var flags = MessageFlags.Compressed | MessageFlags.Encrypted;

		flags.HasFlag(MessageFlags.Compressed).ShouldBeTrue();
		flags.HasFlag(MessageFlags.Encrypted).ShouldBeTrue();
		flags.HasFlag(MessageFlags.Persistent).ShouldBeFalse();
	}
}
