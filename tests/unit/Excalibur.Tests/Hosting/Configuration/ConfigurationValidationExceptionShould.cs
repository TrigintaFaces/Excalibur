using ExcaliburConfigurationValidationError = Excalibur.Hosting.Configuration.ConfigurationValidationError;
using ExcaliburConfigurationValidationException = Excalibur.Hosting.Configuration.ConfigurationValidationException;

namespace Excalibur.Tests.Hosting.Configuration;

[Trait("Category", "Unit")]
public sealed class ExcaliburConfigurationValidationExceptionShould
{
	[Fact]
	public void InitializeWithMessageAndErrors()
	{
		// Arrange
		var errors = new List<ExcaliburConfigurationValidationError> { new("Error 1"), new("Error 2") };

		// Act
		var exception = new ExcaliburConfigurationValidationException("Validation failed", errors);

		// Assert
		exception.Message.ShouldBe("Validation failed");
		exception.Errors.Count.ShouldBe(2);
		exception.Errors.ShouldBe(errors);
	}

	[Fact]
	public void HandleNullErrorsList()
	{
		// Arrange & Act
		var exception = new ExcaliburConfigurationValidationException("Validation failed", null);

		// Assert
		exception.Message.ShouldBe("Validation failed");
		_ = exception.Errors.ShouldNotBeNull();
		exception.Errors.ShouldBeEmpty();
	}
}
