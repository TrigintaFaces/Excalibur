using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerRegistryEntryShould
{
	[Fact]
	public void CreateWithAllProperties()
	{
		var entry = new HandlerRegistryEntry(typeof(string), typeof(object), true);

		entry.MessageType.ShouldBe(typeof(string));
		entry.HandlerType.ShouldBe(typeof(object));
		entry.ExpectsResponse.ShouldBeTrue();
	}

	[Fact]
	public void CreateWithNoResponse()
	{
		var entry = new HandlerRegistryEntry(typeof(int), typeof(string), false);

		entry.ExpectsResponse.ShouldBeFalse();
	}

	[Fact]
	public void StoreActualTypes()
	{
		var entry = new HandlerRegistryEntry(typeof(List<string>), typeof(Dictionary<string, object>), true);

		entry.MessageType.ShouldBe(typeof(List<string>));
		entry.HandlerType.ShouldBe(typeof(Dictionary<string, object>));
	}
}
