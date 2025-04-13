using Excalibur.DataAccess.Exceptions;

using Shouldly;

namespace Excalibur.Tests.Unit.DataAccess.Exceptions;

public class OperationFailedExceptionShould
{
	[Fact]
	public void InitializeWithDefaultValues()
	{
		// Act
		var exception = new OperationFailedException("TestOperation", "TestResource");

		// Assert
		exception.StatusCode.ShouldBe(OperationFailedException.DefaultStatusCode);
		exception.Message.ShouldBe(OperationFailedException.DefaultMessage);
		exception.Resource.ShouldBe("TestResource");
		exception.Operation.ShouldBe("TestOperation");
	}

	[Fact]
	public void AllowCustomStatusCodeAndMessage()
	{
#pragma warning disable CA1303 // Do not pass literals as localized parameters
		var ex = new OperationFailedException("Update", "Order", 403, "Failed update");
#pragma warning restore CA1303 // Do not pass literals as localized parameters

		ex.StatusCode.ShouldBe(403);
		ex.Message.ShouldBe("Failed update");
	}

	[Fact]
	public void ThrowIfOperationOrResourceIsNull()
	{
		_ = Should.Throw<ArgumentNullException>(() => new OperationFailedException(null!, "Res"));
		_ = Should.Throw<ArgumentNullException>(() => new OperationFailedException("Op", null!));
	}
}
