// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests.Exceptions;

/// <summary>
/// Unit tests for <see cref="ResourceException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Exceptions")]
[Trait("Priority", "0")]
public sealed class ResourceExceptionShould
{
	#region Default Constructor Tests

	[Fact]
	public void DefaultConstructor_SetsEmptyResource()
	{
		// Act
		var ex = new ResourceException();

		// Assert
		ex.Resource.ShouldBe(string.Empty);
	}

	#endregion

	#region Message Constructor Tests

	[Fact]
	public void MessageConstructor_SetsMessage()
	{
		// Act
		var ex = new ResourceException("Custom message");

		// Assert
		ex.Message.ShouldBe("Custom message");
	}

	[Fact]
	public void MessageConstructor_SetsEmptyResource()
	{
		// Act
		var ex = new ResourceException("Custom message");

		// Assert
		ex.Resource.ShouldBe(string.Empty);
	}

	#endregion

	#region InnerException Constructor Tests

	[Fact]
	public void InnerExceptionConstructor_SetsMessage()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var ex = new ResourceException("Outer", inner);

		// Assert
		ex.Message.ShouldBe("Outer");
	}

	[Fact]
	public void InnerExceptionConstructor_SetsInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var ex = new ResourceException("Outer", inner);

		// Assert
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void InnerExceptionConstructor_SetsEmptyResource()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var ex = new ResourceException("Outer", inner);

		// Assert
		ex.Resource.ShouldBe(string.Empty);
	}

	#endregion

	#region StatusCode Constructor Tests

	[Fact]
	public void StatusCodeConstructor_SetsStatusCode()
	{
		// Act
		var ex = new ResourceException(404, "Not found", null);

		// Assert
		ex.StatusCode.ShouldBe(404);
	}

	[Fact]
	public void StatusCodeConstructor_SetsMessage()
	{
		// Act
		var ex = new ResourceException(404, "Resource not found", null);

		// Assert
		ex.Message.ShouldBe("Resource not found");
	}

	[Fact]
	public void StatusCodeConstructor_SetsEmptyResource()
	{
		// Act
		var ex = new ResourceException(500, "Error", null);

		// Assert
		ex.Resource.ShouldBe(string.Empty);
	}

	#endregion

	#region Full Constructor Tests

	[Fact]
	public void FullConstructor_SetsResource()
	{
		// Act - Use named parameter to call the full constructor
		var ex = new ResourceException(resource: "Users", statusCode: 500);

		// Assert
		ex.Resource.ShouldBe("Users");
	}

	[Fact]
	public void FullConstructor_WithStatusCode_SetsStatusCode()
	{
		// Act
		var ex = new ResourceException(resource: "Users", statusCode: 409);

		// Assert
		ex.StatusCode.ShouldBe(409);
	}

	[Fact]
	public void FullConstructor_WithMessage_SetsMessage()
	{
		// Act
		var ex = new ResourceException(resource: "Users", message: "User conflict occurred");

		// Assert
		ex.Message.ShouldBe("User conflict occurred");
	}

	[Fact]
	public void FullConstructor_WithNullMessage_SetsDefaultMessage()
	{
		// Act - Use named parameter to call the full constructor
		var ex = new ResourceException(resource: "Users", statusCode: 500, message: null);

		// Assert
		ex.Message.ShouldContain("Users");
	}

	[Fact]
	public void FullConstructor_WithNullStatusCode_SetsDefaultStatusCode()
	{
		// Act - Use named parameter to call the full constructor
		var ex = new ResourceException(resource: "Users", statusCode: null);

		// Assert
		ex.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void FullConstructor_WithInnerException_SetsInnerException()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var ex = new ResourceException(resource: "Users", innerException: inner);

		// Assert
		ex.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void FullConstructor_WithNullResource_ThrowsArgumentException()
	{
		// Act & Assert - Must use named parameter to call the full constructor
		_ = Should.Throw<ArgumentException>(() => new ResourceException(resource: null!, statusCode: 500));
	}

	[Fact]
	public void FullConstructor_WithEmptyResource_ThrowsArgumentException()
	{
		// Act & Assert - Must use named parameter to call the full constructor
		_ = Should.Throw<ArgumentException>(() => new ResourceException(resource: string.Empty, statusCode: 500));
	}

	[Fact]
	public void FullConstructor_WithWhitespaceResource_ThrowsArgumentException()
	{
		// Act & Assert - Must use named parameter to call the full constructor
		_ = Should.Throw<ArgumentException>(() => new ResourceException(resource: "   ", statusCode: 500));
	}

	[Fact]
	public void FullConstructor_WithAllParameters_SetsAllProperties()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var ex = new ResourceException("Orders", statusCode: 404, message: "Order not found", innerException: inner);

		// Assert
		ex.Resource.ShouldBe("Orders");
		ex.StatusCode.ShouldBe(404);
		ex.Message.ShouldBe("Order not found");
		ex.InnerException.ShouldBe(inner);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var ex = new ResourceException();

		// Assert
		_ = ex.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void InheritsFromException()
	{
		// Arrange
		var ex = new ResourceException();

		// Assert
		_ = ex.ShouldBeAssignableTo<Exception>();
	}

	#endregion
}
