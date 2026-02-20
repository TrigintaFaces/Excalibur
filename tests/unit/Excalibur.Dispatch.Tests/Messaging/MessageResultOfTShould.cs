using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultOfTShould
{
	[Fact]
	public void CreateSuccessWithReturnValue()
	{
		var result = MessageResultOfT<int>.Success(42);

		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessWithNullReturnValue()
	{
		var result = MessageResultOfT<string>.Success(null);

		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessWithCacheHit()
	{
		var result = MessageResultOfT<string>.Success("data", cacheHit: true);

		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe("data");
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void CreateFailureWithProblemDetails()
	{
		var problemDetails = A.Fake<IMessageProblemDetails>();

		var result = MessageResultOfT<int>.Failure(problemDetails);

		result.Succeeded.ShouldBeFalse();
		result.ReturnValue.ShouldBe(default);
		result.ProblemDetails.ShouldBe(problemDetails);
	}

	[Fact]
	public void CreateCancelledResult()
	{
		var result = MessageResultOfT<string>.Cancelled();

		result.Succeeded.ShouldBeFalse();
		result.ReturnValue.ShouldBeNull();
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void ImplementIMessageResultOfT()
	{
		var result = MessageResultOfT<int>.Success(99);

		var typed = result as IMessageResult<int>;
		typed.ShouldNotBeNull();
		typed!.ReturnValue.ShouldBe(99);
	}

	[Fact]
	public void AllowSettingReturnValue()
	{
		var result = MessageResultOfT<string>.Success("initial");

		result.ReturnValue = "changed";

		result.ReturnValue.ShouldBe("changed");
	}
}
