using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageContextAttributeShould
{
	[Fact]
	public void BeApplicableToProperties()
	{
		var usage = typeof(MessageContextAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.OfType<AttributeUsageAttribute>()
			.Single();

		usage.ValidOn.ShouldBe(AttributeTargets.Property);
	}

	[Fact]
	public void BeInstantiable()
	{
		var attr = new MessageContextAttribute();

		attr.ShouldNotBeNull();
	}
}
