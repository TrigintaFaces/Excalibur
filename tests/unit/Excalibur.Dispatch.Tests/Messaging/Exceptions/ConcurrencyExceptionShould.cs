using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConcurrencyExceptionShould
{
	[Fact]
	public void CreateWithDefaultMessage()
	{
		var ex = new ConcurrencyException();

		ex.Message.ShouldContain("concurrency conflict");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new ConcurrencyException("Custom message");

		ex.Message.ShouldBe("Custom message");
		ex.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new ConcurrencyException("msg", inner);

		ex.Message.ShouldBe("msg");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithNumericVersions()
	{
		var ex = new ConcurrencyException("Order", "order-1", 5, 3);

		ex.ExpectedVersion.ShouldBe(5);
		ex.ActualVersion.ShouldBe(3);
		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBe("order-1");
		ex.Message.ShouldContain("order-1");
		ex.Message.ShouldContain("5");
		ex.Message.ShouldContain("3");
	}

	[Fact]
	public void CreateWithNumericVersions_NullResourceId()
	{
		var ex = new ConcurrencyException("Order", null, 5, 3);

		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBeNull();
		ex.Message.ShouldContain("Order");
	}

	[Fact]
	public void CreateWithStringVersions()
	{
		var ex = new ConcurrencyException("Order", "order-1", "etag-abc", "etag-xyz");

		ex.ExpectedVersionString.ShouldBe("etag-abc");
		ex.ActualVersionString.ShouldBe("etag-xyz");
		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBe("order-1");
		ex.Message.ShouldContain("etag-abc");
	}

	[Fact]
	public void CreateWithStringVersions_NullResourceId()
	{
		var ex = new ConcurrencyException("Order", null, "v1", "v2");

		ex.ResourceId.ShouldBeNull();
		ex.Message.ShouldContain("Order");
	}

	[Fact]
	public void ForAggregate_CreatesWithTypeName()
	{
		var ex = ConcurrencyException.ForAggregate<string>("id-1", 5, 3);

		ex.Resource.ShouldBe("String");
		ex.ExpectedVersion.ShouldBe(5);
		ex.ActualVersion.ShouldBe(3);
	}

	[Fact]
	public void ETagMismatch_CreatesWithStringVersions()
	{
		var ex = ConcurrencyException.ETagMismatch("Order", "order-1", "etag-old", "etag-new");

		ex.ExpectedVersionString.ShouldBe("etag-old");
		ex.ActualVersionString.ShouldBe("etag-new");
		ex.Resource.ShouldBe("Order");
		ex.ResourceId.ShouldBe("order-1");
	}
}
