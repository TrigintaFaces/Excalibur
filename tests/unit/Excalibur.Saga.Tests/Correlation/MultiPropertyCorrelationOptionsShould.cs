using Excalibur.Saga.Correlation;

namespace Excalibur.Saga.Tests.Correlation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MultiPropertyCorrelationOptionsShould
{
	[Fact]
	public void HaveCorrectDefaults()
	{
		// Arrange & Act
		var options = new MultiPropertyCorrelationOptions();

		// Assert
		options.RequireAllProperties.ShouldBeTrue();
		options.UseCompositeKey.ShouldBeTrue();
	}

	[Fact]
	public void AllowDisablingRequireAllProperties()
	{
		// Arrange & Act
		var options = new MultiPropertyCorrelationOptions { RequireAllProperties = false };

		// Assert
		options.RequireAllProperties.ShouldBeFalse();
	}

	[Fact]
	public void AllowDisablingUseCompositeKey()
	{
		// Arrange & Act
		var options = new MultiPropertyCorrelationOptions { UseCompositeKey = false };

		// Assert
		options.UseCompositeKey.ShouldBeFalse();
	}
}
