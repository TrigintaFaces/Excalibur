using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;

using MsgResult = Excalibur.Dispatch.Messaging.MessageResult;

namespace Excalibur.Dispatch.Tests.Messaging;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultShould
{
	[Fact]
	public void CreateSuccessResultWithDefaults()
	{
		var result = MsgResult.Success();

		result.Succeeded.ShouldBeTrue();
		result.ProblemDetails.ShouldBeNull();
		result.RoutingDecision.ShouldBeNull();
		(result.ValidationResult is null).ShouldBeTrue();
		result.AuthorizationResult.ShouldBeNull();
		result.CacheHit.ShouldBeFalse();
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void CreateSuccessResultWithCacheHit()
	{
		var result = MsgResult.Success(cacheHit: true);

		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void CreateFailureResultWithProblemDetails()
	{
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("test error");

		var result = MsgResult.Failure(problemDetails);

		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.ErrorMessage.ShouldBe("test error");
	}

	[Fact]
	public void CreateFailedResultWithProblemDetails()
	{
		var problemDetails = A.Fake<IMessageProblemDetails>();

		var result = MsgResult.Failed(problemDetails);

		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
	}

	[Fact]
	public void CreateCancelledResult()
	{
		var result = MsgResult.Cancelled();

		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void ReturnSerializableValidationResultWhenNull()
	{
		var result = MsgResult.Success();
		result.ValidationResult = null;

		result.SerializableValidationResult.ShouldBeNull();
	}

	[Fact]
	public void ReturnSerializableValidationResultWhenAlreadySerializable()
	{
		var serializable = new SerializableValidationResult { IsValid = true, Errors = [] };
		var result = MsgResult.Success();
		result.ValidationResult = serializable;

		result.SerializableValidationResult.ShouldBe(serializable);
	}

	[Fact]
	public void ConvertValidationResultToSerializable()
	{
		var validationResult = new SerializableValidationResult
		{
			IsValid = false,
			Errors = ["Field is required"],
		};

		var result = MsgResult.Success();
		result.ValidationResult = validationResult;

		var serializable = result.SerializableValidationResult;
		serializable.ShouldNotBeNull();
		serializable!.IsValid.ShouldBeFalse();
		serializable.Errors.ShouldContain("Field is required");
	}

	[Fact]
	public void SetValidationResultViaSerializableProperty()
	{
		var serializable = new SerializableValidationResult { IsValid = true };
		var result = MsgResult.Success();

		result.SerializableValidationResult = serializable;

		((object?)result.ValidationResult).ShouldBe(serializable);
	}

	[Fact]
	public void ExposeValidationResultAsObjectViaInterface()
	{
		var validationResult = new SerializableValidationResult { IsValid = true };
		var result = MsgResult.Success(validationResult: validationResult);

		((IMessageResult)result).ValidationResult.ShouldBe(validationResult);
	}

	[Fact]
	public void ExposeAuthorizationResultAsObjectViaInterface()
	{
		var authResult = A.Fake<IAuthorizationResult>();
		var result = MsgResult.Success(authorizationResult: authResult);

		((IMessageResult)result).AuthorizationResult.ShouldBe(authResult);
	}

	[Fact]
	public void AllowSettingProperties()
	{
		var result = new MsgResult(true);
		var problemDetails = A.Fake<IMessageProblemDetails>();

		result.Succeeded = false;
		result.ProblemDetails = problemDetails;
		result.CacheHit = true;

		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.CacheHit.ShouldBeTrue();
	}
}
