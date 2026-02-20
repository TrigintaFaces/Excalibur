using Excalibur.Domain.BoundedContext;

namespace Excalibur.Tests.Domain.BoundedContext;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class BoundedContextAttributeShould
{
	[Fact]
	public void StoreNameCorrectly()
	{
		// Arrange & Act
		var attribute = new BoundedContextAttribute("Orders");

		// Assert
		attribute.Name.ShouldBe("Orders");
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("   ")]
	public void ThrowOnNullOrWhiteSpaceName(string? name)
	{
		// Act & Assert
		Should.Throw<ArgumentException>(() => new BoundedContextAttribute(name!));
	}

	[Fact]
	public void BeApplicableToClassesAndInterfaces()
	{
		// Arrange
		var usage = typeof(BoundedContextAttribute)
			.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
			.Cast<AttributeUsageAttribute>()
			.Single();

		// Assert
		usage.ValidOn.ShouldBe(AttributeTargets.Class | AttributeTargets.Interface);
		usage.Inherited.ShouldBeTrue();
		usage.AllowMultiple.ShouldBeFalse();
	}
}
