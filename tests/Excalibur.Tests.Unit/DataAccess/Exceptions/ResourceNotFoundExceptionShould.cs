using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.Exceptions;

public class ResourceNotFoundExceptionShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var exception = new ResourceNotFoundException("TestKey", "TestResource");

		// Assert
		exception.StatusCode.ShouldBe(ResourceNotFoundException.DefaultStatusCode);
		exception.Message.ShouldBe(ResourceNotFoundException.DefaultMessage);
		exception.Resource.ShouldBe("TestResource");
		exception.ResourceKey.ShouldBe("TestKey");
	}

	[Fact]
	public void ThrowIfResourceKeyIsWhitespace()
	{
		_ = Should.Throw<ArgumentException>(() => new ResourceNotFoundException("", "Customer"));
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfResourceKeyIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => new ResourceNotFoundException(null!, "TestResource"));
	}
}
