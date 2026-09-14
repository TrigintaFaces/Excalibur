// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch;
using Excalibur.Dispatch.Exceptions;
using Excalibur.Dispatch.Extensions;

namespace Excalibur.Dispatch.Tests.Messaging.Extensions;

/// <summary>
/// Unit tests for <see cref="ExceptionExtensions"/>.
/// </summary>
/// <remarks>
/// Tests the exception extension methods for error and status code retrieval.
/// </remarks>
[Trait(TraitNames.Category, TestCategories.Unit)]
[Trait("Component", "Extensions")]
[Trait("Priority", "0")]
public sealed class ExceptionExtensionsShould
{
	#region GetErrorCode Tests

	[Fact]
	public void GetErrorCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetErrorCode());
	}

	[Fact]
	public void GetErrorCode_WithForeignExceptionExposingErrorCodeProperty_ReturnsNull()
	{
		// A foreign (non-framework) exception exposing a conventionally-named ErrorCode property is no
		// longer duck-typed -- that job belongs to the registered IExceptionMapper, not a property probe.
		var exception = new ExceptionWithErrorCode(123);

		var result = exception.GetErrorCode();

		result.ShouldBeNull();
	}

	[Fact]
	public void GetErrorCode_WithExceptionWithoutErrorCode_ReturnsNull()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetErrorCode_WithErrorCodeInData_ReturnsErrorCode()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");
		exception.Data["ErrorCode"] = 456;

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBe("456");
	}

	[Fact]
	public void GetErrorCode_WithInnerExceptionHavingErrorCode_ReturnsInnerErrorCode()
	{
		// Arrange
		var innerException = new MessagingException(ErrorCodes.MessageRoutingFailed, "inner");
		var exception = new InvalidOperationException("Outer", innerException);

		// Act
		var result = exception.GetErrorCode();

		// Assert
		result.ShouldBe(ErrorCodes.MessageRoutingFailed);
	}

	[Fact]
	public void GetErrorCode_WithAggregateException_ReturnsFirstInnerErrorCode()
	{
		// Arrange
		var inner1 = new InvalidOperationException("No code");
		var inner2 = new MessagingException(ErrorCodes.MessageDuplicate, "duplicate");
		var aggEx = new AggregateException(inner1, inner2);

		// Act
		var result = aggEx.GetErrorCode();

		// Assert
		result.ShouldBe(ErrorCodes.MessageDuplicate);
	}

	[Fact]
	public void GetErrorCode_WithAggregateExceptionNoErrorCodes_ReturnsNull()
	{
		// Arrange
		var inner1 = new InvalidOperationException("No code");
		var inner2 = new ArgumentException("No code");
		var aggEx = new AggregateException(inner1, inner2);

		// Act
		var result = aggEx.GetErrorCode();

		// Assert
		result.ShouldBeNull();
	}

	#endregion

	#region GetStatusCode Tests

	[Fact]
	public void GetStatusCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetStatusCode());
	}

	[Fact]
	public void GetStatusCode_WithForeignExceptionExposingStatusCodeProperty_ReturnsNull()
	{
		// A foreign (non-framework) exception exposing a conventionally-named StatusCode property is no
		// longer duck-typed -- that job belongs to the registered IExceptionMapper, not a property probe.
		var exception = new ExceptionWithStatusCode(404);

		var result = exception.GetStatusCode();

		result.ShouldBeNull();
	}

	[Fact]
	public void GetStatusCode_WithApiException_ReturnsStatusCode()
	{
		// ApiException.StatusCode is reached by a direct type check, covering every ApiException that is
		// not more specifically a DispatchException, with zero reflection.
		var exception = new ApiException(403, "forbidden", innerException: null);

		var result = exception.GetStatusCode();

		result.ShouldBe(403);
	}

	[Fact]
	public void GetStatusCode_WithDispatchExceptionWithoutDispatchStatusCode_FallsBackToApiExceptionStatusCode()
	{
		// A DispatchException that never set DispatchStatusCode still resolves through the ApiException
		// type check (the inherited default), rather than returning null.
		var exception = new MessagingException(ErrorCodes.MessageRoutingFailed, "routing failed");

		var result = exception.GetStatusCode();

		result.ShouldBe(exception.StatusCode);
	}

	[Fact]
	public void GetStatusCode_WithExceptionWithoutStatusCode_ReturnsNull()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public void GetStatusCode_WithStatusCodeInData_ReturnsStatusCode()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");
		exception.Data["StatusCode"] = 500;

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBe(500);
	}

	[Fact]
	public void GetStatusCode_WithInnerExceptionHavingStatusCode_ReturnsInnerStatusCode()
	{
		// Arrange
		var innerException = new ApiException(401, "unauthorized", innerException: null);
		var exception = new InvalidOperationException("Outer", innerException);

		// Act
		var result = exception.GetStatusCode();

		// Assert
		result.ShouldBe(401);
	}

	#endregion

	#region GetStatusCodeOrDefault Tests

	[Fact]
	public void GetStatusCodeOrDefault_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetStatusCodeOrDefault());
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithStatusCode_ReturnsStatusCode()
	{
		// Arrange
		var exception = new ApiException(403, "forbidden", innerException: null);

		// Act
		var result = exception.GetStatusCodeOrDefault();

		// Assert
		result.ShouldBe(403);
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithoutStatusCode_ReturnsDefaultValue()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCodeOrDefault();

		// Assert
		result.ShouldBe(500);
	}

	[Fact]
	public void GetStatusCodeOrDefault_WithCustomDefault_ReturnsCustomDefault()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetStatusCodeOrDefault(418);

		// Assert
		result.ShouldBe(418);
	}

	[Fact]
	public void GetErrorCode_WithDispatchException_ReturnsTheCodeTheExceptionCarries()
	{
		// The accessor filtered candidate properties on int, and DispatchException.ErrorCode is text, so
		// the framework's own error-code accessor could never see the framework's own error code.
		var exception = new MessagingException(ErrorCodes.MessageRoutingFailed, "routing failed");

		exception.GetErrorCode().ShouldBe(ErrorCodes.MessageRoutingFailed);
	}

	[Fact]
	public void GetErrorCode_WithDispatchExceptionInnerException_ReturnsTheInnerCode()
	{
		var inner = new MessagingException(ErrorCodes.MessageDuplicate, "duplicate");
		var exception = new InvalidOperationException("outer", inner);

		exception.GetErrorCode().ShouldBe(ErrorCodes.MessageDuplicate);
	}

	#endregion

	#region GetErrorCodeOrDefault Tests

	[Fact]
	public void GetErrorCodeOrDefault_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.GetErrorCodeOrDefault("none"));
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithErrorCode_ReturnsErrorCode()
	{
		// Arrange
		var exception = new MessagingException(ErrorCodes.MessageDuplicate, "duplicate");

		// Act
		var result = exception.GetErrorCodeOrDefault("none");

		// Assert
		result.ShouldBe(ErrorCodes.MessageDuplicate);
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithoutErrorCode_ReturnsDefaultValue()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCodeOrDefault("none");

		// Assert
		result.ShouldBe("none");
	}

	[Fact]
	public void GetErrorCodeOrDefault_WithCustomDefault_ReturnsCustomDefault()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.GetErrorCodeOrDefault("custom");

		// Assert
		result.ShouldBe("custom");
	}

	#endregion

	#region HasErrorCode Tests

	[Fact]
	public void HasErrorCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.HasErrorCode());
	}

	[Fact]
	public void HasErrorCode_WithErrorCode_ReturnsTrue()
	{
		// Arrange
		var exception = new MessagingException(ErrorCodes.MessageRoutingFailed, "routing failed");

		// Act
		var result = exception.HasErrorCode();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasErrorCode_WithoutErrorCode_ReturnsFalse()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.HasErrorCode();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region HasStatusCode Tests

	[Fact]
	public void HasStatusCode_WithNullException_ThrowsArgumentNullException()
	{
		// Arrange
		Exception exception = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => exception.HasStatusCode());
	}

	[Fact]
	public void HasStatusCode_WithStatusCode_ReturnsTrue()
	{
		// Arrange
		var exception = new ApiException(500, "error", innerException: null);

		// Act
		var result = exception.HasStatusCode();

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void HasStatusCode_WithoutStatusCode_ReturnsFalse()
	{
		// Arrange
		var exception = new InvalidOperationException("Test");

		// Act
		var result = exception.HasStatusCode();

		// Assert
		result.ShouldBeFalse();
	}

	#endregion

	#region Precedence Tests

	[Fact]
	public void GetErrorCode_WithBothDispatchExceptionAndData_PrefersTheTypeCheck()
	{
		// Arrange
		var exception = new MessagingException(ErrorCodes.MessageRoutingFailed, "routing failed");
		exception.Data["ErrorCode"] = "FromData";

		// Act
		var result = exception.GetErrorCode();

		// Assert - the direct type check outranks the Data dictionary
		result.ShouldBe(ErrorCodes.MessageRoutingFailed);
	}

	[Fact]
	public void GetStatusCode_WithBothApiExceptionAndData_PrefersTheTypeCheck()
	{
		// Arrange
		var exception = new ApiException(400, "bad request", innerException: null);
		exception.Data["StatusCode"] = 500;

		// Act
		var result = exception.GetStatusCode();

		// Assert - the direct type check outranks the Data dictionary
		result.ShouldBe(400);
	}

	#endregion

	#region Test Exception Types

	private sealed class ExceptionWithErrorCode : Exception
	{
		public ExceptionWithErrorCode(int errorCode)
			: base("Exception with error code")
		{
			ErrorCode = errorCode;
		}

		public int ErrorCode { get; }
	}

	private sealed class ExceptionWithStatusCode : Exception
	{
		public ExceptionWithStatusCode(int statusCode)
			: base("Exception with status code")
		{
			StatusCode = statusCode;
		}

		public int StatusCode { get; }
	}

	#endregion
}
