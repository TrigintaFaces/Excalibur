// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Exceptions;

/// <summary>
/// Unit tests for <see cref="ApiException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Exceptions")]
[Trait("Priority", "0")]
public sealed class ApiExceptionShould
{
	#region Default Constructor Tests

	[Fact]
	public void DefaultConstructor_SetsDefaultMessage()
	{
		// Act
		var ex = new ApiException();

		// Assert
		ex.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void DefaultConstructor_SetsDefaultStatusCode()
	{
		// Act
		var ex = new ApiException();

		// Assert
		ex.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void DefaultConstructor_GeneratesUniqueId()
	{
		// Act
		var ex = new ApiException();

		// Assert
		ex.Id.ShouldNotBe(Guid.Empty);
	}

	#endregion

	#region Message Constructor Tests

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Act
		var ex = new ApiException("Custom message");

		// Assert
		ex.Message.ShouldBe("Custom message");
	}

	[Fact]
	public void MessageConstructor_SetsDefaultStatusCode()
	{
		// Act
		var ex = new ApiException("Custom message");

		// Assert
		ex.StatusCode.ShouldBe(500);
	}

	#endregion

	#region InnerException Constructor Tests

	[Fact]
	public void InnerExceptionConstructor_SetsMessage()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner error");

		// Act
		var ex = new ApiException("Outer message", inner);

		// Assert
		ex.Message.ShouldBe("Outer message");
	}

	[Fact]
	public void InnerExceptionConstructor_SetsInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner error");

		// Act
		var ex = new ApiException("Outer message", inner);

		// Assert
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void InnerExceptionConstructor_AllowsNullInnerException()
	{
		// Act
		var ex = new ApiException("Outer message", null);

		// Assert
		ex.InnerException.ShouldBeNull();
	}

	#endregion

	#region StatusCode Constructor Tests

	[Fact]
	public void StatusCodeConstructor_SetsStatusCode()
	{
		// Act
		var ex = new ApiException(404, "Not found", null);

		// Assert
		ex.StatusCode.ShouldBe(404);
	}

	[Fact]
	public void StatusCodeConstructor_SetsMessage()
	{
		// Act
		var ex = new ApiException(400, "Bad request", null);

		// Assert
		ex.Message.ShouldBe("Bad request");
	}

	[Fact]
	public void StatusCodeConstructor_WithNullMessage_UsesDefaultMessage()
	{
		// Act
		var ex = new ApiException(500, null, null);

		// Assert
		ex.Message.ShouldNotBeNullOrEmpty();
	}

	[Fact]
	public void StatusCodeConstructor_WithStatusCodeBelow100_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ApiException(99, "Invalid", null));
	}

	[Fact]
	public void StatusCodeConstructor_WithStatusCodeAbove599_ThrowsArgumentOutOfRangeException()
	{
		// Act & Assert
		_ = Should.Throw<ArgumentOutOfRangeException>(() => new ApiException(600, "Invalid", null));
	}

	[Fact]
	public void StatusCodeConstructor_WithStatusCode100_IsValid()
	{
		// Act
		var ex = new ApiException(100, "Continue", null);

		// Assert
		ex.StatusCode.ShouldBe(100);
	}

	[Fact]
	public void StatusCodeConstructor_WithStatusCode599_IsValid()
	{
		// Act
		var ex = new ApiException(599, "Custom", null);

		// Assert
		ex.StatusCode.ShouldBe(599);
	}

	#endregion

	#region StatusCode Init Tests

	[Fact]
	public void StatusCode_CanBeInitialized()
	{
		// Act
		var ex = new ApiException("Test") { StatusCode = 403 };

		// Assert
		ex.StatusCode.ShouldBe(403);
	}

	#endregion

	#region ToProblemDetails Tests

	[Fact]
	public void ToProblemDetails_ReturnsNonNullResult()
	{
		// Arrange
		var ex = new ApiException("Test error");

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		_ = problemDetails.ShouldNotBeNull();
	}

	[Fact]
	public void ToProblemDetails_SetsTypeUri()
	{
		// Arrange
		var ex = new ApiException("Test error");

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.Type.ShouldStartWith("urn:dispatch:error:");
		problemDetails.Type.ShouldContain("apiexception");
	}

	[Fact]
	public void ToProblemDetails_SetsTitle()
	{
		// Arrange
		var ex = new ApiException("Test error");

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.Title.ShouldBe("ApiException");
	}

	[Fact]
	public void ToProblemDetails_SetsStatusCode()
	{
		// Arrange
		var ex = new ApiException(404, "Not found", null);

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.Status.ShouldBe(404);
	}

	[Fact]
	public void ToProblemDetails_SetsDetail()
	{
		// Arrange
		var ex = new ApiException("Test error message");

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.Detail.ShouldBe("Test error message");
	}

	[Fact]
	public void ToProblemDetails_SetsInstance()
	{
		// Arrange
		var ex = new ApiException("Test error");

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.Instance.ShouldStartWith("urn:dispatch:exception:");
		problemDetails.Instance.ShouldContain(ex.Id.ToString());
	}

	[Fact]
	public void ToProblemDetails_SetsErrorCode()
	{
		// Arrange
		var ex = new ApiException(422, "Validation failed", null);

		// Act
		var problemDetails = ex.ToProblemDetails();

		// Assert
		problemDetails.ErrorCode.ShouldBe(422);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromException()
	{
		// Arrange
		var ex = new ApiException();

		// Assert
		_ = ex.ShouldBeAssignableTo<Exception>();
	}

	#endregion

	#region Id Property Tests

	[Fact]
	public void Id_IsUniqueAcrossInstances()
	{
		// Arrange & Act
		var ex1 = new ApiException();
		var ex2 = new ApiException();

		// Assert
		ex1.Id.ShouldNotBe(ex2.Id);
	}

	#endregion
}
