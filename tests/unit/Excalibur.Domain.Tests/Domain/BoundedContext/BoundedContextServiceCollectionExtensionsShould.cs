using Excalibur.Domain.BoundedContext;

namespace Excalibur.Tests.Domain.BoundedContext;

[Trait("Category", "Unit")]
[Trait("Component", "Domain")]
public sealed class BoundedContextServiceCollectionExtensionsShould
{
	[Fact]
	public void RegisterValidatorAndOptions_WithConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddBoundedContextEnforcement(options =>
		{
			options.EnforcementMode = BoundedContextEnforcementMode.Error;
			options.ValidateOnStartup = true;
		});

		// Assert
		var provider = services.BuildServiceProvider();
		var validator = provider.GetService<IBoundedContextValidator>();
		validator.ShouldNotBeNull();
		validator.ShouldBeOfType<DefaultBoundedContextValidator>();
	}

	[Fact]
	public void RegisterValidatorAndOptions_WithDefaults()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act
		services.AddBoundedContextEnforcement();

		// Assert
		var provider = services.BuildServiceProvider();
		var validator = provider.GetService<IBoundedContextValidator>();
		validator.ShouldNotBeNull();
	}

	[Fact]
	public void ThrowOnNullServices()
	{
		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			((IServiceCollection)null!).AddBoundedContextEnforcement(_ => { }));
	}

	[Fact]
	public void ThrowOnNullConfigure()
	{
		// Arrange
		var services = new ServiceCollection();

		// Act & Assert
		Should.Throw<ArgumentNullException>(() =>
			services.AddBoundedContextEnforcement(null!));
	}

	[Fact]
	public void NotReplaceExistingValidator()
	{
		// Arrange
		var services = new ServiceCollection();
		var fakeValidator = A.Fake<IBoundedContextValidator>();
		services.AddSingleton(fakeValidator);

		// Act
		services.AddBoundedContextEnforcement();

		// Assert
		var provider = services.BuildServiceProvider();
		var validator = provider.GetService<IBoundedContextValidator>();
		validator.ShouldBeSameAs(fakeValidator);
	}
}
