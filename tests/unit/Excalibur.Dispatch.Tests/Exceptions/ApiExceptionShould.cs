// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Exceptions;

/// <summary>
/// Unit tests for <see cref="ApiException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ApiExceptionShould
{
	#region Constructor Tests

	[Fact]
	public void Create_WithDefaultConstructor_SetsDefaultValues()
	{
		// Arrange & Act
		var exception = new ApiException();

		// Assert
		exception.StatusCode.ShouldBe(500);
		exception.Message.ShouldNotBeNullOrEmpty();
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	[Fact]
	public void Create_WithMessage_SetsMessage()
	{
		// Arrange
		var message = "Custom error message";

		// Act
		var exception = new ApiException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void Create_WithMessageAndInnerException_SetsBoth()
	{
		// Arrange
		var message = "Outer error";
		var inner = new InvalidOperationException("Inner error");

		// Act
		var exception = new ApiException(message, inner);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithStatusCodeMessageAndInner_SetsAll()
	{
		// Arrange
		var statusCode = 404;
		var message = "Not found";
		var inner = new InvalidOperationException("Resource missing");

		// Act
		var exception = new ApiException(statusCode, message, inner);

		// Assert
		exception.StatusCode.ShouldBe(statusCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithNullMessage_UsesDefaultMessage()
	{
		// Arrange & Act
		var exception = new ApiException(400, null, null);

		// Assert
		exception.Message.ShouldNotBeNullOrEmpty();
	}

	[Theory]
	[InlineData(99)]
	[InlineData(600)]
	[InlineData(-1)]
	[InlineData(1000)]
	public void Create_WithInvalidStatusCode_ThrowsArgumentOutOfRangeException(int invalidStatusCode)
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentOutOfRangeException>(() =>
			new ApiException(invalidStatusCode, "Message", null));
	}

	[Theory]
	[InlineData(100)]
	[InlineData(200)]
	[InlineData(301)]
	[InlineData(400)]
	[InlineData(404)]
	[InlineData(500)]
	[InlineData(599)]
	public void Create_WithValidStatusCode_Succeeds(int validStatusCode)
	{
		// Arrange & Act
		var exception = new ApiException(validStatusCode, "Message", null);

		// Assert
		exception.StatusCode.ShouldBe(validStatusCode);
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_IsUniquePerInstance()
	{
		// Arrange
		var exception1 = new ApiException();
		var exception2 = new ApiException();

		// Assert
		exception1.Id.ShouldNotBe(exception2.Id);
	}

	[Fact]
	public void Id_IsNotEmpty()
	{
		// Arrange
		var exception = new ApiException();

		// Assert
		exception.Id.ShouldNotBe(Guid.Empty);
	}

	#endregion

	#region ToProblemDetails Tests

	[Fact]
	public void ToProblemDetails_ReturnsMessageProblemDetails()
	{
		// Arrange
		var exception = new ApiException(400, "Bad request", null);

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.ShouldNotBeNull();
		problemDetails.Status.ShouldBe(400);
		problemDetails.Detail.ShouldBe("Bad request");
	}

	[Fact]
	public void ToProblemDetails_SetsTypeUri()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Type.ShouldContain("urn:dispatch:error:");
		problemDetails.Type.ShouldContain("apiexception");
	}

	[Fact]
	public void ToProblemDetails_SetsTitle()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Title.ShouldBe("ApiException");
	}

	[Fact]
	public void ToProblemDetails_SetsInstanceWithId()
	{
		// Arrange
		var exception = new ApiException();

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Instance.ShouldContain("urn:dispatch:exception:");
		problemDetails.Instance.ShouldContain(exception.Id.ToString());
	}

	[Fact]
	public void ToProblemDetails_SetsErrorCode()
	{
		// Arrange
		var exception = new ApiException(422, "Unprocessable entity", null);

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.ErrorCode.ShouldBe(422);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Arrange
		var exception = new ApiException();

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void IsSerializable()
	{
		// Assert
		typeof(ApiException).GetCustomAttributes(typeof(SerializableAttribute), false)
			.Length.ShouldBe(1);
	}

	#endregion
}
