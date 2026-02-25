using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ForbiddenExceptionShould
{
	[Fact]
	public void CreateWithDefault()
	{
		var ex = new ForbiddenException();

		ex.DispatchStatusCode.ShouldBe(403);
		ex.Message.ShouldContain("forbidden");
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new ForbiddenException("Custom message");

		ex.Message.ShouldBe("Custom message");
		ex.DispatchStatusCode.ShouldBe(403);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ForbiddenException("msg", inner);

		ex.Message.ShouldBe("msg");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithResourceAndOperation()
	{
		var ex = new ForbiddenException("Order", "Delete");

		ex.Resource.ShouldBe("Order");
		ex.Operation.ShouldBe("Delete");
		ex.Message.ShouldContain("Delete");
		ex.Message.ShouldContain("Order");
	}

	[Fact]
	public void CreateWithRequiredPermission()
	{
		var ex = new ForbiddenException("Order", "Delete", "admin:delete");

		ex.Resource.ShouldBe("Order");
		ex.Operation.ShouldBe("Delete");
		ex.RequiredPermission.ShouldBe("admin:delete");
		ex.Message.ShouldContain("admin:delete");
	}

	[Fact]
	public void MissingRole_CreatesWithRoleContext()
	{
		var ex = ForbiddenException.MissingRole("Order", "Delete", "Admin");

		ex.Resource.ShouldBe("Order");
		ex.Operation.ShouldBe("Delete");
		ex.RequiredPermission.ShouldBe("Role:Admin");
	}

	[Fact]
	public void SubscriptionRequired_CreatesWithTierContext()
	{
		var ex = ForbiddenException.SubscriptionRequired("Analytics", "Enterprise");

		ex.Message.ShouldContain("Enterprise");
		ex.Message.ShouldContain("subscription");
	}
}
