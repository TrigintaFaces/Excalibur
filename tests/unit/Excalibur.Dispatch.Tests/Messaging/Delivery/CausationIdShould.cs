using Excalibur.Dispatch.Delivery;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class CausationIdShould
{
	[Fact]
	public void HaveDefaultGuidValue()
	{
		var causation = new CausationId();

		causation.Value.ShouldBe(Guid.Empty);
	}

	[Fact]
	public void AllowSettingValue()
	{
		var guid = Guid.NewGuid();
		var causation = new CausationId { Value = guid };

		causation.Value.ShouldBe(guid);
	}

	[Fact]
	public void ToStringReturnsGuidString()
	{
		var guid = Guid.NewGuid();
		var causation = new CausationId { Value = guid };

		causation.ToString().ShouldBe(guid.ToString());
	}

	[Fact]
	public void ToStringReturnsEmptyGuidStringByDefault()
	{
		var causation = new CausationId();

		causation.ToString().ShouldBe(Guid.Empty.ToString());
	}
}
