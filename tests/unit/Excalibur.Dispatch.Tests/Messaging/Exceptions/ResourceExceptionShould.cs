using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResourceExceptionShould
{
	[Fact]
	public void CreateWithDefault()
	{
		var ex = new ResourceException();

		ex.DispatchStatusCode.ShouldBe(404);
		ex.Message.ShouldContain("resource error");
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new ResourceException("Custom message");

		ex.Message.ShouldBe("Custom message");
		ex.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ResourceException("msg", inner);

		ex.Message.ShouldBe("msg");
		ex.InnerException.ShouldBe(inner);
		ex.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void ForResource_WithId()
	{
		var ex = ResourceException.ForResource("Order", "order-123");

		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBe("order-123");
		ex.Message.ShouldContain("Order");
		ex.Message.ShouldContain("order-123");
	}

	[Fact]
	public void ForResource_WithoutId()
	{
		var ex = ResourceException.ForResource("Order");

		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBeNull();
		ex.Message.ShouldContain("Order");
	}
}
