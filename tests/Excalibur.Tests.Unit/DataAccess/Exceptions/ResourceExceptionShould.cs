using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.Exceptions;

public class ResourceExceptionShould
{
	[Fact]
	public void InitializeCorrectly()
	{
		// Act
		var exception = new ResourceException("TestResource", 500);

		// Assert
		exception.StatusCode.ShouldBe(500);
		exception.Message.ShouldBe("Operation failed for resource TestResource");
		exception.Resource.ShouldBe("TestResource");
	}

	[Fact]
	public void SetPropertiesCorrectly()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var ex = new ResourceException("Customer", 400, "Custom error", new InvalidOperationException());
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		ex.StatusCode.ShouldBe(400);
		ex.Message.ShouldBe("Custom error");
		_ = ex.InnerException.ShouldBeOfType<InvalidOperationException>();
		ex.Resource.ShouldBe("Customer");
	}

	[Fact]
	public void UseDefaultMessageIfNoneProvided()
	{
		var ex = new ResourceException("Order", 404);

		ex.Message.ShouldBe("Operation failed for resource Order");
	}

	[Fact]
	public void ThrowArgumentExceptionIfResourceIsWhitespace()
	{
		_ = Should.Throw<ArgumentException>(() => new ResourceException("", 500));
	}

	[Fact]
	public void ThrowArgumentNullExceptionIfResourceIsNull()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentException>(() => new ResourceException(null!, 500));
	}
}
