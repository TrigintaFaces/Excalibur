// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Exceptions;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchExceptionShould
{
	[Fact]
	public void InitializeWithDefaultConstructor()
	{
		var lowerBound = DateTimeOffset.UtcNow;

		// Act
		var ex = new DispatchException();
		var upperBound = DateTimeOffset.UtcNow;

		// Assert
		ex.ErrorCode.ShouldNotBeNullOrWhiteSpace();
		ex.Category.ShouldBe(ErrorCategory.Unknown);
		ex.Severity.ShouldBe(ErrorSeverity.Information);
		ex.InstanceId.ShouldNotBe(Guid.Empty);
		ex.Timestamp.ShouldBeGreaterThanOrEqualTo(lowerBound);
		ex.Timestamp.ShouldBeLessThanOrEqualTo(upperBound);
	}

	[Fact]
	public void InitializeWithMessage()
	{
		// Act
		var ex = new DispatchException("Custom error message");

		// Assert
		ex.Message.ShouldBe("Custom error message");
	}

	[Fact]
	public void InitializeWithMessageAndInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("inner");

		// Act
		var ex = new DispatchException("outer", inner);

		// Assert
		ex.Message.ShouldBe("outer");
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void InitializeWithErrorCodeAndMessage()
	{
		// Act
		var ex = new DispatchException("CFG001", "Configuration error");

		// Assert
		ex.ErrorCode.ShouldBe("CFG001");
		ex.Category.ShouldBe(ErrorCategory.Configuration);
		ex.Severity.ShouldBe(ErrorSeverity.Critical);
	}

	[Fact]
	public void InitializeWithStatusCodeErrorCodeAndMessage()
	{
		// Act
		var ex = new DispatchException(503, "NET001", "Network error", null);

		// Assert
		ex.ErrorCode.ShouldBe("NET001");
		ex.Category.ShouldBe(ErrorCategory.Network);
		ex.DispatchStatusCode.ShouldBe(503);
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
	[InlineData("UNKNOWN", ErrorCategory.Unknown)]
	[InlineData("", ErrorCategory.Unknown)]
	public void DetermineCategoryFromErrorCode(string errorCode, ErrorCategory expectedCategory)
	{
		// Act
		var ex = new DispatchException(errorCode, "test");

		// Assert
		ex.Category.ShouldBe(expectedCategory);
	}

	[Theory]
	[InlineData(ErrorCategory.Configuration, ErrorSeverity.Critical)]
	[InlineData(ErrorCategory.Security, ErrorSeverity.Critical)]
	[InlineData(ErrorCategory.System, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Data, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Messaging, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Validation, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Timeout, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Network, ErrorSeverity.Warning)]
	[InlineData(ErrorCategory.Serialization, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Resource, ErrorSeverity.Error)]
	[InlineData(ErrorCategory.Unknown, ErrorSeverity.Information)]
	public void DetermineSeverityFromCategory(ErrorCategory category, ErrorSeverity expectedSeverity)
	{
		// Build an error code that maps to the given category
		var codePrefix = category switch
		{
			ErrorCategory.Configuration => "CFG",
			ErrorCategory.Validation => "VAL",
			ErrorCategory.Messaging => "MSG",
			ErrorCategory.Serialization => "SER",
			ErrorCategory.Network => "NET",
			ErrorCategory.Security => "SEC",
			ErrorCategory.Data => "DAT",
			ErrorCategory.Timeout => "TIM",
			ErrorCategory.Resource => "RES",
			ErrorCategory.System => "SYS",
			_ => "UNK",
		};

		// Act
		var ex = new DispatchException(codePrefix + "001", "test");

		// Assert
		ex.Severity.ShouldBe(expectedSeverity);
	}

	[Fact]
	public void AddContextWithFluentApi()
	{
		// Arrange
		var ex = new DispatchException("ERR", "test");

		// Act
		var result = ex.WithContext("key1", "value1").WithContext("key2", 42);

		// Assert
		result.ShouldBeSameAs(ex);
		ex.Context["key1"].ShouldBe("value1");
		ex.Context["key2"].ShouldBe(42);
	}

	[Fact]
	public void SetCorrelationIdWithFluentApi()
	{
		// Act
		var ex = new DispatchException("ERR", "test").WithCorrelationId("corr-123");

		// Assert
		ex.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SetUserMessageWithFluentApi()
	{
		// Act
		var ex = new DispatchException("ERR", "test").WithUserMessage("User friendly message");

		// Assert
		ex.UserMessage.ShouldBe("User friendly message");
	}

	[Fact]
	public void SetSuggestedActionWithFluentApi()
	{
		// Act
		var ex = new DispatchException("ERR", "test").WithSuggestedAction("Retry the operation");

		// Assert
		ex.SuggestedAction.ShouldBe("Retry the operation");
	}

	[Fact]
	public void SetStatusCodeWithFluentApi()
	{
		// Act
		var ex = new DispatchException("ERR", "test").WithStatusCode(429);

		// Assert
		ex.DispatchStatusCode.ShouldBe(429);
	}

	[Fact]
	public void ConvertToDispatchProblemDetails()
	{
		// Arrange
		var ex = new DispatchException("VAL001", "Validation failed")
			.WithCorrelationId("corr-1")
			.WithUserMessage("Please fix your input")
			.WithSuggestedAction("Check the field values")
			.WithStatusCode(400);

		// Act
		var details = ex.ToDispatchProblemDetails();

		// Assert
		details.ShouldNotBeNull();
		details.ErrorCode.ShouldBe("VAL001");
		details.Title.ShouldBe("Please fix your input");
		details.Detail.ShouldBe("Validation failed");
		details.Status.ShouldBe(400);
		details.CorrelationId.ShouldBe("corr-1");
		details.SuggestedAction.ShouldBe("Check the field values");
		details.Category.ShouldBe("Validation");
		details.Severity.ShouldBe("Warning");
		details.Instance.ShouldContain("urn:excalibur:error:");
	}

	[Fact]
	public void ConvertToProblemDetails()
	{
		// Arrange
		var ex = new DispatchException("MSG001", "Message error").WithStatusCode(500);

		// Act
		var details = ex.ToProblemDetails();

		// Assert
		details.ShouldNotBeNull();
		details.Status.ShouldBe(500);
		details.Detail.ShouldBe("Message error");
		details.Instance.ShouldContain("urn:excalibur:error:");
	}

	[Fact]
	public void DetermineStatusCodeFromCategoryWhenNotExplicitlySet()
	{
		// Arrange - Validation → 400
		var valEx = new DispatchException("VAL001", "test");
		var valDetails = valEx.ToDispatchProblemDetails();
		valDetails.Status.ShouldBe(400);

		// Security → 401
		var secEx = new DispatchException("SEC001", "test");
		var secDetails = secEx.ToDispatchProblemDetails();
		secDetails.Status.ShouldBe(401);

		// Resource → 404
		var resEx = new DispatchException("RES001", "test");
		var resDetails = resEx.ToDispatchProblemDetails();
		resDetails.Status.ShouldBe(404);

		// Timeout → 408
		var timEx = new DispatchException("TIM001", "test");
		var timDetails = timEx.ToDispatchProblemDetails();
		timDetails.Status.ShouldBe(408);

		// Data → 422
		var datEx = new DispatchException("DAT001", "test");
		var datDetails = datEx.ToDispatchProblemDetails();
		datDetails.Status.ShouldBe(422);

		// Network → 503
		var netEx = new DispatchException("NET001", "test");
		var netDetails = netEx.ToDispatchProblemDetails();
		netDetails.Status.ShouldBe(503);
	}

	[Fact]
	public void HandleNullErrorCodeGracefully()
	{
		// Act
		var ex = new DispatchException(null!, "test");

		// Assert
		ex.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
		ex.Category.ShouldBe(ErrorCategory.Unknown);
	}
}
