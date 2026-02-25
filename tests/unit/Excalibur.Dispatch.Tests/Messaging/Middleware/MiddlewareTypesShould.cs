using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareTypesShould
{
	// --- AuthorizationResult ---

	[Fact]
	public void AuthorizationResult_Success_IsAuthorized()
	{
		var result = AuthorizationResult.Success();

		result.IsAuthorized.ShouldBeTrue();
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void AuthorizationResult_Failure_IsNotAuthorized()
	{
		var result = AuthorizationResult.Failure("Insufficient permissions");

		result.IsAuthorized.ShouldBeFalse();
		result.Reason.ShouldBe("Insufficient permissions");
	}

	// --- AuthenticationException ---

	[Fact]
	public void AuthenticationException_CreateWithMessage()
	{
		var ex = new AuthenticationException("Access denied");

		ex.Message.ShouldBe("Access denied");
		ex.InnerException.ShouldBeNull();
	}

	[Fact]
	public void AuthenticationException_CreateWithMessageAndInner()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new AuthenticationException("Access denied", inner);

		ex.Message.ShouldBe("Access denied");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void AuthenticationException_CreateWithDefaultConstructor()
	{
		var ex = new AuthenticationException();

		ex.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void AuthenticationException_IsException()
	{
		var ex = new AuthenticationException("test");

		ex.ShouldBeAssignableTo<Exception>();
	}
}
