// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

// Use alias to disambiguate from Excalibur.Dispatch.Abstractions.ResourceException
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ResourceException"/> to verify resource error handling
/// with HTTP 404 status code support and problem details extensions.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ResourceExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use404StatusCodeByDefault()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.NotFound);
	}

	[Fact]
	public void UseResourceNotFoundErrorCode()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ResourceNotFound);
	}

	[Fact]
	public void SetResourceAndResourceIdWithFactoryMethod()
	{
		// Arrange
		var resource = "User";
		var resourceId = "user-123";

		// Act
		var exception = ResourceException.ForResource(resource, resourceId);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.ResourceId.ShouldBe(resourceId);
	}

	[Fact]
	public void IncludeResourceInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = ResourceException.ForResource("Order", "order-456");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "Order");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resourceId", "order-456");
	}

	[Fact]
	public void MergeContextWithDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = ResourceException.ForResource("Product", "prod-789")
			.WithContext("customField", "customValue");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "Product");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("customField", "customValue");
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ResourceException("Custom resource error");

		// Assert
		exception.Message.ShouldBe("Custom resource error");
		exception.DispatchStatusCode.ShouldBe(404);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ResourceException("Outer error", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ResourceException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void FormatMessageWithResourceAndId()
	{
		// Arrange & Act
		var exception = ResourceException.ForResource("Customer", "cust-123");

		// Assert
		exception.Message.ShouldContain("Customer");
		exception.Message.ShouldContain("cust-123");
	}
}
