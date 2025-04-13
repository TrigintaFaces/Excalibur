using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.Exceptions;

public class ConcurrencyExceptionShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var exception = new ConcurrencyException("TestKey", "TestResource");

		// Assert
		exception.StatusCode.ShouldBe(ConcurrencyException.DefaultStatusCode);
		exception.Message.ShouldBe(ConcurrencyException.DefaultMessage);
		exception.Resource.ShouldBe("TestResource");
		exception.ResourceKey.ShouldBe("TestKey");
	}

	[Fact]
	public void AllowCustomMessageAndStatusCode()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
#pragma warning disable CA2201 // Do not raise reserved exception types
		var ex = new ConcurrencyException("abc", "Product", 409, "Custom", new Exception("inner"));
#pragma warning restore CA2201 // Do not raise reserved exception types
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		ex.StatusCode.ShouldBe(409);
		ex.Message.ShouldBe("Custom");
		ex.InnerException!.Message.ShouldBe("inner");
	}

	[Fact]
	public void ThrowIfKeyOrResourceIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new ConcurrencyException(null!, "Test"));
		_ = Should.Throw<ArgumentNullException>(() => new ConcurrencyException("123", null!));
	}
}
