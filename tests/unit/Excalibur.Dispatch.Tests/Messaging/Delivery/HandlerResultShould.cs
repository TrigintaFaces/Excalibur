using Excalibur.Dispatch.Delivery.Handlers;

namespace Excalibur.Dispatch.Tests.Messaging.Delivery;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class HandlerResultShould
{
	[Fact]
	public void CreateSuccessResult()
	{
		var result = new HandlerResult<int>(42);

		result.HasValue.ShouldBeTrue();
		result.IsFaulted.ShouldBeFalse();
		result.Value.ShouldBe(42);
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateFaultedResult()
	{
		var ex = new InvalidOperationException("failed");
		var result = new HandlerResult<int>(ex);

		result.HasValue.ShouldBeFalse();
		result.IsFaulted.ShouldBeTrue();
		result.Exception.ShouldBe(ex);
	}

	[Fact]
	public void ThrowOnAccessingValueOfFaultedResult()
	{
		var result = new HandlerResult<int>(new InvalidOperationException("fail"));

		Should.Throw<InvalidOperationException>(() => _ = result.Value);
	}

	[Fact]
	public void ThrowOnAccessingValueOfDefaultResult()
	{
		var result = default(HandlerResult<int>);

		Should.Throw<InvalidOperationException>(() => _ = result.Value);
	}

	[Fact]
	public void ThrowOnNullExceptionInConstructor()
	{
		Should.Throw<ArgumentNullException>(() => new HandlerResult<int>((Exception)null!));
	}

	[Fact]
	public void FromTResult_CreatesSuccessResult()
	{
		var result = HandlerResult<string>.FromTResult("hello");

		result.HasValue.ShouldBeTrue();
		result.Value.ShouldBe("hello");
	}

	[Fact]
	public void FromException_CreatesFaultedResult()
	{
		var ex = new InvalidOperationException("fail");
		var result = HandlerResult<string>.FromException(ex);

		result.IsFaulted.ShouldBeTrue();
		result.Exception.ShouldBe(ex);
	}

	[Fact]
	public void ImplicitConversion_FromValue()
	{
		HandlerResult<int> result = 42;

		result.HasValue.ShouldBeTrue();
		result.Value.ShouldBe(42);
	}

	[Fact]
	public void ImplicitConversion_FromException()
	{
		var ex = new InvalidOperationException("fail");
		HandlerResult<int> result = ex;

		result.IsFaulted.ShouldBeTrue();
		result.Exception.ShouldBe(ex);
	}

	[Fact]
	public void Equals_SameValue()
	{
		var r1 = new HandlerResult<int>(42);
		var r2 = new HandlerResult<int>(42);

		r1.Equals(r2).ShouldBeTrue();
		(r1 == r2).ShouldBeTrue();
		(r1 != r2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_DifferentValues()
	{
		var r1 = new HandlerResult<int>(42);
		var r2 = new HandlerResult<int>(99);

		r1.Equals(r2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_SuccessVsFaulted()
	{
		var r1 = new HandlerResult<int>(42);
		var r2 = new HandlerResult<int>(new InvalidOperationException("fail"));

		r1.Equals(r2).ShouldBeFalse();
	}

	[Fact]
	public void Equals_Object()
	{
		var r1 = new HandlerResult<int>(42);
		object r2 = new HandlerResult<int>(42);

		r1.Equals(r2).ShouldBeTrue();
		r1.Equals("not a result").ShouldBeFalse();
	}

	[Fact]
	public void GetHashCode_SameForEqualResults()
	{
		var r1 = new HandlerResult<int>(42);
		var r2 = new HandlerResult<int>(42);

		r1.GetHashCode().ShouldBe(r2.GetHashCode());
	}

	[Fact]
	public void CreateWithStringValue()
	{
		var result = new HandlerResult<string>("hello");

		result.HasValue.ShouldBeTrue();
		result.Value.ShouldBe("hello");
	}

	[Fact]
	public void CreateWithNullStringValue()
	{
		// Use explicit string cast to disambiguate from Exception constructor
		var result = new HandlerResult<string?>((string?)null);

		result.HasValue.ShouldBeTrue();
		// null is still a valid value for nullable types
	}
}
