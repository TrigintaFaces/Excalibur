using Excalibur.Domain.BoundedContext;

namespace Excalibur.Tests.Domain.BoundedContext;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class BoundedContextOptionsShould
{
	[Fact]
	public void HaveDefaultEnforcementModeOfWarn()
	{
		// Arrange & Act
		var options = new BoundedContextOptions();

		// Assert
		options.EnforcementMode.ShouldBe(BoundedContextEnforcementMode.Warn);
	}

	[Fact]
	public void HaveEmptyAllowedPatternsByDefault()
	{
		// Arrange & Act
		var options = new BoundedContextOptions();

		// Assert
		options.AllowedCrossBoundaryPatterns.ShouldNotBeNull();
		options.AllowedCrossBoundaryPatterns.ShouldBeEmpty();
	}

	[Fact]
	public void NotValidateOnStartupByDefault()
	{
		// Arrange & Act
		var options = new BoundedContextOptions();

		// Assert
		options.ValidateOnStartup.ShouldBeFalse();
	}

	[Fact]
	public void AllowSettingEnforcementMode()
	{
		// Arrange
		var options = new BoundedContextOptions();

		// Act
		options.EnforcementMode = BoundedContextEnforcementMode.Error;

		// Assert
		options.EnforcementMode.ShouldBe(BoundedContextEnforcementMode.Error);
	}

	[Fact]
	public void AllowAddingCrossBoundaryPatterns()
	{
		// Arrange
		var options = new BoundedContextOptions();

		// Act
		options.AllowedCrossBoundaryPatterns.Add("Orders->SharedKernel");
		options.AllowedCrossBoundaryPatterns.Add("Inventory->SharedKernel");

		// Assert
		options.AllowedCrossBoundaryPatterns.Count.ShouldBe(2);
		options.AllowedCrossBoundaryPatterns.ShouldContain("Orders->SharedKernel");
	}

	[Fact]
	public void AllowSettingValidateOnStartup()
	{
		// Arrange
		var options = new BoundedContextOptions();

		// Act
		options.ValidateOnStartup = true;

		// Assert
		options.ValidateOnStartup.ShouldBeTrue();
	}
}
