using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Delivery.Pipeline;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MiddlewareContextShould
{
	[Fact]
	public void InitializeWithMiddleware()
	{
		var middleware = new IDispatchMiddleware[]
		{
			A.Fake<IDispatchMiddleware>(),
			A.Fake<IDispatchMiddleware>(),
		};

		var ctx = new MiddlewareContext(middleware);

		ctx.CurrentIndex.ShouldBe(-1);
		ctx.HasNext.ShouldBeTrue();
	}

	[Fact]
	public void MoveNextThroughMiddleware()
	{
		var m1 = A.Fake<IDispatchMiddleware>();
		var m2 = A.Fake<IDispatchMiddleware>();
		var middleware = new[] { m1, m2 };

		var ctx = new MiddlewareContext(middleware);

		ctx.MoveNext().ShouldBe(m1);
		ctx.CurrentIndex.ShouldBe(0);
		ctx.HasNext.ShouldBeTrue();

		ctx.MoveNext().ShouldBe(m2);
		ctx.CurrentIndex.ShouldBe(1);
		ctx.HasNext.ShouldBeFalse();
	}

	[Fact]
	public void ReturnNullWhenNoMoreMiddleware()
	{
		var middleware = new[] { A.Fake<IDispatchMiddleware>() };
		var ctx = new MiddlewareContext(middleware);

		ctx.MoveNext(); // first
		ctx.MoveNext().ShouldBeNull(); // past end
	}

	[Fact]
	public void ResetToBeginning()
	{
		var m1 = A.Fake<IDispatchMiddleware>();
		var middleware = new[] { m1 };
		var ctx = new MiddlewareContext(middleware);

		ctx.MoveNext();
		ctx.CurrentIndex.ShouldBe(0);

		ctx.Reset();

		ctx.CurrentIndex.ShouldBe(-1);
		ctx.HasNext.ShouldBeTrue();
		ctx.MoveNext().ShouldBe(m1);
	}

	[Fact]
	public void HandleEmptyMiddleware()
	{
		var ctx = new MiddlewareContext([]);

		ctx.HasNext.ShouldBeFalse();
		ctx.MoveNext().ShouldBeNull();
	}

	[Fact]
	public void SupportEquality()
	{
		var middleware = new[] { A.Fake<IDispatchMiddleware>() };
		var c1 = new MiddlewareContext(middleware);
		var c2 = new MiddlewareContext(middleware);

		c1.Equals(c2).ShouldBeTrue();
		(c1 == c2).ShouldBeTrue();
	}

	[Fact]
	public void SupportInequalityAfterMoveNext()
	{
		var middleware = new[] { A.Fake<IDispatchMiddleware>(), A.Fake<IDispatchMiddleware>() };
		var c1 = new MiddlewareContext(middleware);
		var c2 = new MiddlewareContext(middleware);

		c1.MoveNext();

		c1.Equals(c2).ShouldBeFalse();
		(c1 != c2).ShouldBeTrue();
	}

	[Fact]
	public void SupportEqualsWithObject()
	{
		var ctx = new MiddlewareContext([]);

		ctx.Equals((object)new MiddlewareContext([])).ShouldBeTrue();
		ctx.Equals(null).ShouldBeFalse();
		ctx.Equals("not a context").ShouldBeFalse();
	}

	[Fact]
	public void SupportGetHashCode()
	{
		var middleware = new[] { A.Fake<IDispatchMiddleware>() };
		var c1 = new MiddlewareContext(middleware);
		var c2 = new MiddlewareContext(middleware);

		c1.GetHashCode().ShouldBe(c2.GetHashCode());
	}
}
