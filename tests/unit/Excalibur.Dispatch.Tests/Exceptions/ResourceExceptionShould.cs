// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Exceptions;

/// <summary>
/// Unit tests for <see cref="ResourceException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class ResourceExceptionShould
{
	#region Constructor Tests (Full Parameter)

	[Fact]
	public void Create_WithResource_SetsResource()
	{
		// Arrange & Act
		var exception = new ResourceException(resource: "Order");

		// Assert
		exception.Resource.ShouldBe("Order");
	}

	[Fact]
	public void Create_WithResource_SetsDefaultStatusCode()
	{
		// Arrange & Act
		var exception = new ResourceException(resource: "Order");

		// Assert
		exception.StatusCode.ShouldBe(500);
	}

	[Fact]
	public void Create_WithResource_SetsDefaultMessage()
	{
		// Arrange & Act
		var exception = new ResourceException(resource: "Order");

		// Assert
		exception.Message.ShouldContain("Order");
		exception.Message.ShouldContain("failed");
	}

	[Fact]
	public void Create_WithResourceAndStatusCode_SetsStatusCode()
	{
		// Arrange & Act
		var exception = new ResourceException(resource: "Order", statusCode: 404);

		// Assert
		exception.StatusCode.ShouldBe(404);
		exception.Resource.ShouldBe("Order");
	}

	[Fact]
	public void Create_WithResourceAndMessage_SetsMessage()
	{
		// Arrange & Act
		var exception = new ResourceException(resource: "Order", message: "Custom error message");

		// Assert
		exception.Message.ShouldBe("Custom error message");
		exception.Resource.ShouldBe("Order");
	}

	[Fact]
	public void Create_WithAllParameters_SetsAll()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner error");

		// Act
		var exception = new ResourceException("Order", 404, "Not found", inner);

		// Assert
		exception.Resource.ShouldBe("Order");
		exception.StatusCode.ShouldBe(404);
		exception.Message.ShouldBe("Not found");
		exception.InnerException.ShouldBe(inner);
	}

	[Fact]
	public void Create_WithNullResource_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() =>
			new ResourceException(null!, statusCode: 500));
	}

	[Fact]
	public void Create_WithEmptyResource_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() =>
			new ResourceException("", statusCode: 500));
	}

	[Fact]
	public void Create_WithWhitespaceResource_ThrowsArgumentException()
	{
		// Arrange & Act & Assert
		Should.Throw<ArgumentException>(() =>
			new ResourceException("   ", statusCode: 500));
	}

	#endregion

	#region Constructor Tests (Alternative Constructors)

	[Fact]
	public void Create_WithDefaultConstructor_SetsEmptyResource()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		exception.Resource.ShouldBe(string.Empty);
	}

	[Fact]
	public void Create_WithMessageOnly_SetsEmptyResource()
	{
		// Arrange & Act
		var exception = new ResourceException("Error message");

		// Assert
		exception.Message.ShouldBe("Error message");
		exception.Resource.ShouldBe(string.Empty);
	}

	[Fact]
	public void Create_WithMessageAndInnerException_SetsEmptyResource()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var exception = new ResourceException("Error message", inner);

		// Assert
		exception.Message.ShouldBe("Error message");
		exception.InnerException.ShouldBe(inner);
		exception.Resource.ShouldBe(string.Empty);
	}

	[Fact]
	public void Create_WithStatusCodeMessageAndInner_SetsEmptyResource()
	{
		// Arrange
		var inner = new InvalidOperationException("Inner");

		// Act
		var exception = new ResourceException(400, "Bad request", inner);

		// Assert
		exception.StatusCode.ShouldBe(400);
		exception.Message.ShouldBe("Bad request");
		exception.InnerException.ShouldBe(inner);
		exception.Resource.ShouldBe(string.Empty);
	}

	#endregion

	#region Inheritance Tests

	[Fact]
	public void InheritsFromApiException()
	{
		// Arrange
		var exception = new ResourceException(resource: "Order");

		// Assert
		exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void InheritsFromException()
	{
		// Arrange
		var exception = new ResourceException(resource: "Order");

		// Assert
		exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void IsSerializable()
	{
		// Assert
		typeof(ResourceException).GetCustomAttributes(typeof(SerializableAttribute), false)
			.Length.ShouldBe(1);
	}

	#endregion

	#region Common Resource Types

	[Theory]
	[InlineData("Order")]
	[InlineData("User")]
	[InlineData("Product")]
	[InlineData("Invoice")]
	[InlineData("Order/order-123")]
	public void Create_WithVariousResourceTypes_Succeeds(string resource)
	{
		// Act
		var exception = new ResourceException(resource: resource);

		// Assert
		exception.Resource.ShouldBe(resource);
	}

	#endregion
}
