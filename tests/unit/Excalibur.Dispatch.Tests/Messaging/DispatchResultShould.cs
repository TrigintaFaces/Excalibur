using Excalibur.Dispatch.Messaging;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchResultShould
{
	[Fact]
	public void CreateSuccessResultWithDefaults()
	{
		var result = DispatchResult.Success();

		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBeNull();
		result.Exception.ShouldBeNull();
		result.Metadata.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessResultWithResultData()
	{
		var data = new { Value = 42 };

		var result = DispatchResult.Success(data);

		result.IsSuccess.ShouldBeTrue();
		result.Result.ShouldBe(data);
		result.Exception.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessResultWithMetadata()
	{
		var metadata = new Dictionary<string, object> { ["key"] = "value" };

		var result = DispatchResult.Success(metadata: metadata);

		result.IsSuccess.ShouldBeTrue();
		result.Metadata.ShouldNotBeNull();
		result.Metadata!["key"].ShouldBe("value");
	}

	[Fact]
	public void CreateFailureResultWithException()
	{
		var exception = new InvalidOperationException("test error");

		var result = DispatchResult.Failure(exception);

		result.IsSuccess.ShouldBeFalse();
		result.Result.ShouldBeNull();
		result.Exception.ShouldBe(exception);
	}

	[Fact]
	public void CreateFailureResultWithMetadata()
	{
		var exception = new InvalidOperationException("test");
		var metadata = new Dictionary<string, object> { ["retryCount"] = 3 };

		var result = DispatchResult.Failure(exception, metadata);

		result.IsSuccess.ShouldBeFalse();
		result.Exception.ShouldBe(exception);
		result.Metadata.ShouldNotBeNull();
		result.Metadata!["retryCount"].ShouldBe(3);
	}

	[Fact]
	public void ConstructDirectlyWithAllParameters()
	{
		var exception = new InvalidOperationException("err");
		var metadata = new Dictionary<string, object> { ["a"] = 1 };

		var result = new DispatchResult(
			isSuccess: false,
			result: "data",
			exception: exception,
			metadata: metadata);

		result.IsSuccess.ShouldBeFalse();
		result.Result.ShouldBe("data");
		result.Exception.ShouldBe(exception);
		result.Metadata.ShouldBe(metadata);
	}
}
