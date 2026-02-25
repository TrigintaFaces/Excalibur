// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="DispatchException"/> to verify inheritance from ApiException,
/// error categorization, and rich problem details support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class DispatchExceptionShould
{
	[Fact]
	public void InheritFromApiException()
	{
		// Arrange & Act
		var exception = new DispatchException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void BeCatchableAsApiException()
	{
		// Arrange
		var exception = new DispatchException("Test error");

		// Act & Assert
		try
		{
			throw exception;
		}
		catch (ApiException caught)
		{
			caught.ShouldBe(exception);
		}
	}

	[Fact]
	public void HaveErrorCodeWhenCreated()
	{
		// Arrange & Act
		var exception = new DispatchException(ErrorCodes.ValidationFailed, "Validation error");

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
	}

	[Fact]
	public void DefaultToUnknownErrorCode()
	{
		// Arrange & Act
		var exception = new DispatchException("Test error");

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.UnknownError);
	}

	[Fact]
	public void DetermineCategoryFromErrorCode()
	{
		// Arrange & Act
		var validationException = new DispatchException(ErrorCodes.ValidationFailed, "Validation failed");
		var securityException = new DispatchException(ErrorCodes.SecurityAuthenticationFailed, "Auth failed");
		var resourceException = new DispatchException(ErrorCodes.ResourceNotFound, "Not found");
		var timeoutException = new DispatchException(ErrorCodes.TimeoutOperation, "Timeout");

		// Assert
		validationException.Category.ShouldBe(ErrorCategory.Validation);
		securityException.Category.ShouldBe(ErrorCategory.Security);
		resourceException.Category.ShouldBe(ErrorCategory.Resource);
		timeoutException.Category.ShouldBe(ErrorCategory.Timeout);
	}

	[Fact]
	public void DetermineSeverityFromCategory()
	{
		// Arrange & Act - Security should be Critical
		var securityException = new DispatchException(ErrorCodes.SecurityAuthenticationFailed, "Auth failed");

		// Assert
		securityException.Severity.ShouldBe(ErrorSeverity.Critical);
	}

	[Fact]
	public void HaveUniqueInstanceId()
	{
		// Arrange & Act
		var exception1 = new DispatchException();
		var exception2 = new DispatchException();

		// Assert
		exception1.InstanceId.ShouldNotBe(Guid.Empty);
		exception2.InstanceId.ShouldNotBe(Guid.Empty);
		exception1.InstanceId.ShouldNotBe(exception2.InstanceId);
	}

	[Fact]
	public void HaveTimestampWhenCreated()
	{
		// Arrange
		var before = DateTimeOffset.UtcNow;

		// Act
		var exception = new DispatchException();

		// Assert
		var after = DateTimeOffset.UtcNow;
		exception.Timestamp.ShouldBeGreaterThanOrEqualTo(before);
		exception.Timestamp.ShouldBeLessThanOrEqualTo(after);
	}

	[Fact]
	public void SupportFluentContextConfiguration()
	{
		// Arrange
		var exception = new DispatchException("Error")
			.WithContext("userId", "user-123")
			.WithContext("operation", "delete");

		// Assert
		exception.Context.ShouldContainKeyAndValue("userId", "user-123");
		exception.Context.ShouldContainKeyAndValue("operation", "delete");
	}

	[Fact]
	public void SupportFluentCorrelationId()
	{
		// Arrange & Act
		var exception = new DispatchException("Error")
			.WithCorrelationId("corr-123");

		// Assert
		exception.CorrelationId.ShouldBe("corr-123");
	}

	[Fact]
	public void SupportFluentUserMessage()
	{
		// Arrange & Act
		var exception = new DispatchException("Technical error")
			.WithUserMessage("Something went wrong. Please try again.");

		// Assert
		exception.UserMessage.ShouldBe("Something went wrong. Please try again.");
	}

	[Fact]
	public void SupportFluentSuggestedAction()
	{
		// Arrange & Act
		var exception = new DispatchException("Error")
			.WithSuggestedAction("Please check your input and try again.");

		// Assert
		exception.SuggestedAction.ShouldBe("Please check your input and try again.");
	}

	[Fact]
	public void SupportStatusCodeOverride()
	{
		// Arrange & Act
		var exception = new DispatchException(ErrorCodes.ResourceNotFound, "Not found")
			.WithStatusCode(404);

		// Assert
		exception.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void ToProblemDetails_ReturnsValidStructure()
	{
		// Arrange
		var exception = new DispatchException(ErrorCodes.ValidationFailed, "Validation failed");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		_ = problemDetails.ShouldNotBeNull();
		problemDetails.Type.ShouldBe(ProblemDetailsTypes.Validation); // URN format per RFC 9457
		problemDetails.Status.ShouldBe(400); // Validation -> 400
		problemDetails.Detail.ShouldBe("Validation failed");
		problemDetails.Instance.ShouldContain(exception.InstanceId.ToString());
	}

	[Fact]
	public void ToDispatchProblemDetails_IncludesRichData()
	{
		// Arrange
		var exception = new DispatchException(ErrorCodes.SecurityAccessDenied, "Access denied")
			.WithCorrelationId("corr-456")
			.WithSuggestedAction("Contact support");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert
		dispatchProblemDetails.ErrorCode.ShouldBe(ErrorCodes.SecurityAccessDenied);
		dispatchProblemDetails.Category.ShouldBe(ErrorCategory.Security.ToString());
		dispatchProblemDetails.Severity.ShouldBe(ErrorSeverity.Critical.ToString());
		dispatchProblemDetails.CorrelationId.ShouldBe("corr-456");
		dispatchProblemDetails.SuggestedAction.ShouldBe("Contact support");
		dispatchProblemDetails.Timestamp.ShouldBe(exception.Timestamp);
	}

	[Fact]
	public void PreserveInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new DispatchException("Outer error", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(DispatchException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void DetermineStatusCodeFromCategory()
	{
		// Arrange
		var validation = new DispatchException(ErrorCodes.ValidationFailed, "Validation failed");
		var security = new DispatchException(ErrorCodes.SecurityAuthenticationFailed, "Auth failed");
		var resource = new DispatchException(ErrorCodes.ResourceNotFound, "Not found");
		var timeout = new DispatchException(ErrorCodes.TimeoutOperation, "Timeout");

		// Act
		var validationPd = validation.ToProblemDetails();
		var securityPd = security.ToProblemDetails();
		var resourcePd = resource.ToProblemDetails();
		var timeoutPd = timeout.ToProblemDetails();

		// Assert
		validationPd.Status.ShouldBe(400);
		securityPd.Status.ShouldBe(401);
		resourcePd.Status.ShouldBe(404);
		timeoutPd.Status.ShouldBe(408);
	}

	[Fact]
	public void UseDispatchStatusCodeWhenSet()
	{
		// Arrange
		var exception = new DispatchException(ErrorCodes.ResourceConflict, "Conflict")
			.WithStatusCode(409);

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Status.ShouldBe(409);
	}
}
