using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Functional.DataAccess.Exceptions;

public class ResourceAlreadyExistsExceptionShould
{
	[Fact]
	public void IncludeResourceKeyAndResourceInMessage()
	{
		// Arrange
		var exception = new ResourceAlreadyExistsException("123", "TestResource");

		// Act
		var message = exception.Message;

		// Assert
		message.ShouldBe("The specified resource already exists.");
		exception.ResourceKey.ShouldBe("123");
	}

	[Fact]
	public void UseDefaultMessageIfNotProvided()
	{
		// Arrange
		var exception = new ResourceAlreadyExistsException("123", "TestResource");

		// Act & Assert
		exception.Message.ShouldBe(ResourceAlreadyExistsException.DefaultMessage);
	}

	[Fact]
	public void PreserveInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner exception");
		var exception = new ResourceAlreadyExistsException("123", "TestResource", innerException: innerException);

		// Act & Assert
		exception.InnerException.ShouldBe(innerException);
	}
}
