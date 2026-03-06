// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Data.Abstractions;
using Excalibur.Dispatch.Abstractions;

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
	public void InheritFromApiException()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use404StatusCodeByDefault()
	{
		// Arrange & Act
		var exception = new ResourceException();

		// Assert
		exception.StatusCode.ShouldBe((int)HttpStatusCode.NotFound);
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
	public void IncludeResourceInProblemDetailsExtensions()
	{
		// Arrange
		var exception = ResourceException.ForResource("Order", "order-456");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Extensions.ShouldContainKeyAndValue("resource", "Order");
		problemDetails.Extensions.ShouldContainKeyAndValue("resourceId", "order-456");
	}

	[Fact]
	public void MergeContextWithProblemDetailsExtensions()
	{
		// Arrange
		var exception = ResourceException.ForResource("Product", "prod-789")
			.WithContext("customField", "customValue");

		// Act
		var problemDetails = exception.ToProblemDetails();

		// Assert
		problemDetails.Extensions.ShouldContainKeyAndValue("resource", "Product");
		problemDetails.Extensions.ShouldContainKeyAndValue("customField", "customValue");
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ResourceException("Custom resource error");

		// Assert
		exception.Message.ShouldBe("Custom resource error");
		exception.StatusCode.ShouldBe(404);
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
