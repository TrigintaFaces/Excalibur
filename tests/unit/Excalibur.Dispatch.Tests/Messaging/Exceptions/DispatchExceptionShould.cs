using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchExceptionDepthShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		var ex = new DispatchException();

		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
		ex.Category.ShouldBe(ErrorCategory.Unknown);
		ex.Severity.ShouldBe(ErrorSeverity.Information);
		ex.InstanceId.ShouldNotBe(Guid.Empty);
		ex.Timestamp.ShouldNotBe(default);
	}

	[Fact]
	public void CreateWithMessage()
	{
		var ex = new DispatchException("test error");

		ex.Message.ShouldBe("test error");
		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new DispatchException("outer", inner);

		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void CreateWithErrorCodeAndMessage()
	{
		var ex = new DispatchException(ErrorCodes.ConfigurationInvalid, "config error");

		ex.ErrorCode.ShouldBe(ErrorCodes.ConfigurationInvalid);
		ex.Message.ShouldBe("config error");
		ex.Category.ShouldBe(ErrorCategory.Configuration);
		ex.Severity.ShouldBe(ErrorSeverity.Critical);
	}

	[Fact]
	public void CreateWithStatusCodeErrorCodeMessageAndInnerException()
	{
		var inner = new InvalidOperationException("inner");
		var ex = new DispatchException(503, ErrorCodes.NetworkConnectionFailed, "network error", inner);

		ex.DispatchStatusCode.ShouldBe(503);
		ex.ErrorCode.ShouldBe(ErrorCodes.NetworkConnectionFailed);
		ex.Message.ShouldBe("network error");
		ex.InnerException.ShouldBe(inner);
	}

	[Theory]
	[InlineData("CFG001", ErrorCategory.Configuration)]
	[InlineData("VAL001", ErrorCategory.Validation)]
	[InlineData("MSG001", ErrorCategory.Messaging)]
	[InlineData("SER001", ErrorCategory.Serialization)]
	[InlineData("NET001", ErrorCategory.Network)]
	[InlineData("SEC001", ErrorCategory.Security)]
	[InlineData("DAT001", ErrorCategory.Data)]
	[InlineData("TIM001", ErrorCategory.Timeout)]
	[InlineData("RES001", ErrorCategory.Resource)]
	[InlineData("SYS001", ErrorCategory.System)]
	[InlineData("UNK001", ErrorCategory.Unknown)]
	[InlineData("RANDOM", ErrorCategory.Unknown)]
	[InlineData("", ErrorCategory.Unknown)]
	public void DetermineCategoryFromErrorCode(string errorCode, ErrorCategory expectedCategory)
	{
		var ex = new DispatchException(errorCode, "test");

		ex.Category.ShouldBe(expectedCategory);
	}

	[Theory]
	[InlineData(ErrorCategory.Configuration, ErrorSeverity.Critical)]
	[InlineData(ErrorCategory.Security, ErrorSeverity.Critical)]
	[InlineData(ErrorCategory.System, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Data, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Serialization, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Resource, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Messaging, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Validation, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Timeout, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Network, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Unknown, ErrorSeverity.Information)]
	public void DetermineSeverityFromCategory(ErrorCategory category, ErrorSeverity expectedSeverity)
	{
		var errorCode = category switch
		{
			ErrorCategory.Configuration => "CFG001",
			ErrorCategory.Security => "SEC001",
			ErrorCategory.System => "SYS001",
			ErrorCategory.Data => "DAT001",
			ErrorCategory.Serialization => "SER001",
			ErrorCategory.Resource => "RES001",
			ErrorCategory.Messaging => "MSG001",
			ErrorCategory.Validation => "VAL001",
			ErrorCategory.Timeout => "TIM001",
			ErrorCategory.Network => "NET001",
			_ => "UNK001",
		};

		var ex = new DispatchException(errorCode, "test");

		ex.Severity.ShouldBe(expectedSeverity);
	}

	[Fact]
	public void SupportFluentWithContext()
	{
		var ex = new DispatchException("test")
			.WithContext("key1", "value1")
			.WithContext("key2", 42);

		ex.Context["key1"].ShouldBe("value1");
		ex.Context["key2"].ShouldBe(42);
	}

	[Fact]
	public void SupportFluentWithCorrelationId()
	{
		var ex = new DispatchException("test").WithCorrelationId("corr-123");

		ex.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SupportFluentWithUserMessage()
	{
		var ex = new DispatchException("test").WithUserMessage("user-friendly message");

		ex.UserMessage.ShouldBe("user-friendly message");
	}

	[Fact]
	public void SupportFluentWithSuggestedAction()
	{
		var ex = new DispatchException("test").WithSuggestedAction("Try again later");

		ex.SuggestedAction.ShouldBe("Try again later");
	}

	[Fact]
	public void SupportFluentWithStatusCode()
	{
		var ex = new DispatchException("test").WithStatusCode(404);

		ex.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void ConvertToDispatchProblemDetails()
	{
		var ex = new DispatchException(ErrorCodes.ValidationFailed, "Invalid input")
			.WithUserMessage("Please fix the input")
			.WithStatusCode(400)
			.WithCorrelationId("corr-abc")
			.WithSuggestedAction("Check the field values")
			.WithContext("field", "name");

		var pd = ex.ToDispatchProblemDetails();

		pd.Title.ShouldBe("Please fix the input");
		pd.Status.ShouldBe(400);
		pd.Detail.ShouldBe("Invalid input");
		pd.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		pd.CorrelationId.ShouldBe("corr-abc");
		pd.SuggestedAction.ShouldBe("Check the field values");
		pd.Instance.ShouldStartWith("urn:excalibur:error:");
		pd.Category.ShouldBe("Validation");
		pd.Severity.ShouldBe("Warning");
	}

	[Fact]
	public void ConvertToProblemDetails()
	{
		var ex = new DispatchException(ErrorCodes.ResourceNotFound, "Not found")
			.WithStatusCode(404)
			.WithUserMessage("Resource missing");

		var pd = ex.ToProblemDetails();

		pd.Title.ShouldBe("Resource missing");
		pd.Status.ShouldBe(404);
		pd.Detail.ShouldBe("Not found");
		pd.Instance.ShouldStartWith("urn:excalibur:error:");
	}

	[Fact]
	public void UseAutoStatusCodeWhenNotExplicitlySet()
	{
		var ex = new DispatchException(ErrorCodes.ValidationFailed, "Bad input");

		var pd = ex.ToDispatchProblemDetails();

		pd.Status.ShouldBe(400);
	}

	[Fact]
	public void HandleNullErrorCode()
	{
		var ex = new DispatchException(null!, "test");

		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
	}

	[Fact]
	public void ReturnFluentSelf()
	{
		var ex = new DispatchException("test");

		var returned = ex.WithContext("k", "v");

		returned.ShouldBeSameAs(ex);
	}
}
