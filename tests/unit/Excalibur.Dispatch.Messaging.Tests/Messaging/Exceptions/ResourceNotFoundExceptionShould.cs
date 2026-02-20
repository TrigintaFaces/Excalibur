// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

// Use alias to disambiguate from Excalibur.Dispatch.Abstractions.ResourceException
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ResourceNotFoundException"/> to verify resource not found
/// error handling with HTTP 404 status code and specific resource identification.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResourceNotFoundExceptionShould
{
	[Fact]
	public void InheritFromResourceException()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ResourceException>();
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use404StatusCode()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.NotFound);
	}

	[Fact]
	public void UseResourceNotFoundErrorCode()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ResourceNotFound);
	}

	[Fact]
	public void SetResourceAndResourceIdWhenCreated()
	{
		// Arrange
		var resource = "User";
		var resourceId = "user-123";

		// Act
		var exception = new ResourceNotFoundException(resource, resourceId);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.ResourceId.ShouldBe(resourceId);
	}

	[Fact]
	public void FormatMessageWithResourceAndId()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException("Order", "order-456");

		// Assert
		exception.Message.ShouldContain("Order");
		exception.Message.ShouldContain("order-456");
		exception.Message.ShouldContain("not found");
	}

	[Fact]
	public void CreateForEntityWithFactory()
	{
		// Arrange & Act
		var exception = ResourceNotFoundException.ForEntity<TestEntity>(123);

		// Assert
		exception.Resource.ShouldBe("TestEntity");
		exception.ResourceId.ShouldBe("123");
	}

	[Fact]
	public void IncludeResourceInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new ResourceNotFoundException("Product", "prod-789");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "Product");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resourceId", "prod-789");
	}

	[Fact]
	public void IncludeResourceInContext()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException("Category", "cat-123");

		// Assert
		exception.Context.ShouldContainKeyAndValue("resource", "Category");
		exception.Context.ShouldContainKeyAndValue("resourceId", "cat-123");
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException("Custom not found message");

		// Assert
		exception.Message.ShouldBe("Custom not found message");
		exception.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ResourceNotFoundException("Not found", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ResourceNotFoundException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		// Arrange & Act
		var exception = new ResourceNotFoundException();

		// Assert
		exception.Message.ShouldContain("not found");
	}

	// Helper class for testing generic factory method
	private sealed class TestEntity { }
}
