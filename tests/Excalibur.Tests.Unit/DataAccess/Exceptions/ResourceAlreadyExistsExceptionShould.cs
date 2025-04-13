using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.Exceptions;

public class ResourceAlreadyExistsExceptionShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var exception = new ResourceAlreadyExistsException("TestKey", "TestResource");

		// Assert
		exception.StatusCode.ShouldBe(ResourceAlreadyExistsException.DefaultStatusCode);
		exception.Message.ShouldBe(ResourceAlreadyExistsException.DefaultMessage);
		exception.Resource.ShouldBe("TestResource");
		exception.ResourceKey.ShouldBe("TestKey");
	}

	[Fact]
	public void AllowOverrideOfMessageAndStatus()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var ex = new ResourceAlreadyExistsException("k", "X", 409, "Already exists");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		ex.StatusCode.ShouldBe(409);
		ex.Message.ShouldBe("Already exists");
	}

	[Fact]
	public void ThrowIfResourceKeyIsWhitespace()
	{
		_ = Should.Throw<ArgumentException>(() => new ResourceAlreadyExistsException("", "Customer"));
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfResourceKeyIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new ResourceAlreadyExistsException(null!, "TestResource"));
	}
}
