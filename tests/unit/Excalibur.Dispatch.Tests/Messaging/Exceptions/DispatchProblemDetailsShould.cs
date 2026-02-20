using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchProblemDetailsShould
{
	[Fact]
	public void HaveDefaultValues()
	{
		var pd = new DispatchProblemDetails();

		pd.Type.ShouldBe("about:blank");
		pd.Title.ShouldBeNull();
		pd.Status.ShouldBeNull();
		pd.Detail.ShouldBeNull();
		pd.Instance.ShouldBeNull();
		pd.ErrorCode.ShouldBeNull();
		pd.Category.ShouldBeNull();
		pd.Severity.ShouldBeNull();
		pd.CorrelationId.ShouldBeNull();
		pd.TraceId.ShouldBeNull();
		pd.SpanId.ShouldBeNull();
		pd.Timestamp.ShouldBeNull();
		pd.SuggestedAction.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingAllProperties()
	{
		var now = DateTimeOffset.UtcNow;
		var pd = new DispatchProblemDetails
		{
			Type = "urn:error:test",
			Title = "Test Error",
			Status = 500,
			Detail = "details",
			Instance = "instance-1",
			ErrorCode = "ERR001",
			Category = "Test",
			Severity = "Error",
			CorrelationId = "corr-1",
			TraceId = "trace-1",
			SpanId = "span-1",
			Timestamp = now,
			SuggestedAction = "Retry",
		};

		pd.Type.ShouldBe("urn:error:test");
		pd.Title.ShouldBe("Test Error");
		pd.Status.ShouldBe(500);
		pd.Detail.ShouldBe("details");
		pd.Instance.ShouldBe("instance-1");
		pd.ErrorCode.ShouldBe("ERR001");
		pd.Category.ShouldBe("Test");
		pd.Severity.ShouldBe("Error");
		pd.CorrelationId.ShouldBe("corr-1");
		pd.TraceId.ShouldBe("trace-1");
		pd.SpanId.ShouldBe("span-1");
		pd.Timestamp.ShouldBe(now);
		pd.SuggestedAction.ShouldBe("Retry");
	}

	[Fact]
	public void CreateFromDispatchException()
	{
		var ex = new DispatchException(ErrorCodes.ValidationFailed, "Bad input")
			.WithStatusCode(400);

		var pd = DispatchProblemDetails.FromException(ex);

		pd.Status.ShouldBe(400);
		pd.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		pd.Category.ShouldBe("Validation");
	}

	[Fact]
	public void CreateFromGenericExceptionWithDetails()
	{
		var ex = new InvalidOperationException("something went wrong");

		var pd = DispatchProblemDetails.FromException(ex, includeDetails: true);

		pd.Status.ShouldBe(500);
		pd.Detail.ShouldBe("something went wrong");
		pd.Extensions.ShouldNotBeNull();
		pd.Extensions!["exceptionType"].ShouldBe("InvalidOperationException");
	}

	[Fact]
	public void CreateFromGenericExceptionWithoutDetails()
	{
		var ex = new InvalidOperationException("something went wrong");

		var pd = DispatchProblemDetails.FromException(ex, includeDetails: false);

		pd.Status.ShouldBe(500);
		pd.Detail.ShouldBeNull();
		pd.Extensions.ShouldBeNull();
	}

	[Fact]
	public void CreateFromExceptionWithErrorCodeInData()
	{
		var ex = new InvalidOperationException("test");
		ex.Data["ErrorCode"] = "CUSTOM001";

		var pd = DispatchProblemDetails.FromException(ex);

		pd.ErrorCode.ShouldBe("CUSTOM001");
	}

	[Fact]
	public void CreateFromExceptionWithCorrelationIdInData()
	{
		var ex = new InvalidOperationException("test");
		ex.Data["CorrelationId"] = "corr-xyz";

		var pd = DispatchProblemDetails.FromException(ex);

		pd.CorrelationId.ShouldBe("corr-xyz");
	}

	[Fact]
	public void CreateValidationProblemDetails()
	{
		var errors = new Dictionary<string, string[]>
		{
			["Name"] = ["Required"],
			["Email"] = ["Invalid format"],
		};

		var pd = DispatchProblemDetails.ForValidation(errors);

		pd.Status.ShouldBe(400);
		pd.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		pd.Category.ShouldBe("Validation");
		pd.Extensions.ShouldNotBeNull();
		pd.Extensions!["errors"].ShouldBe(errors);
	}

	[Fact]
	public void CreateNotFoundProblemDetailsWithId()
	{
		var pd = DispatchProblemDetails.ForNotFound("Order", "order-123");

		pd.Status.ShouldBe(404);
		pd.ErrorCode.ShouldBe(ErrorCodes.ResourceNotFound);
		pd.Detail.ShouldContain("order-123");
	}

	[Fact]
	public void CreateNotFoundProblemDetailsWithoutId()
	{
		var pd = DispatchProblemDetails.ForNotFound("Customer");

		pd.Status.ShouldBe(404);
		pd.Detail.ShouldContain("Customer");
		pd.Detail.ShouldNotContain("ID");
	}

	[Fact]
	public void CreateUnauthorizedProblemDetails()
	{
		var pd = DispatchProblemDetails.ForUnauthorized("Expired token");

		pd.Status.ShouldBe(401);
		pd.Detail.ShouldBe("Expired token");
		pd.SuggestedAction.ShouldNotBeNull();
	}

	[Fact]
	public void CreateUnauthorizedProblemDetailsWithDefault()
	{
		var pd = DispatchProblemDetails.ForUnauthorized();

		pd.Status.ShouldBe(401);
		pd.Detail.ShouldContain("Authentication is required");
	}

	[Fact]
	public void CreateForbiddenProblemDetails()
	{
		var pd = DispatchProblemDetails.ForForbidden("Admin required");

		pd.Status.ShouldBe(403);
		pd.Detail.ShouldBe("Admin required");
	}

	[Fact]
	public void CreateForbiddenProblemDetailsWithDefault()
	{
		var pd = DispatchProblemDetails.ForForbidden();

		pd.Status.ShouldBe(403);
		pd.Detail.ShouldContain("permission");
	}
}
