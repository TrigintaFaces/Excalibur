// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

// Use alias to disambiguate from Excalibur.Dispatch.Abstractions.ResourceException
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ForbiddenException"/> to verify permission denied
/// error handling with HTTP 403 status code support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ForbiddenExceptionShould
{
	[Fact]
	public void InheritFromResourceException()
	{
		// Arrange & Act
		var exception = new ForbiddenException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ResourceException>();
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use403StatusCode()
	{
		// Arrange & Act
		var exception = new ForbiddenException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.Forbidden);
	}

	[Fact]
	public void UseSecurityForbiddenErrorCode()
	{
		// Arrange & Act
		var exception = new ForbiddenException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.SecurityForbidden);
	}

	[Fact]
	public void SetResourceAndOperationWhenCreated()
	{
		// Arrange
		var resource = "Order";
		var operation = "Delete";

		// Act
		var exception = new ForbiddenException(resource, operation);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.Operation.ShouldBe(operation);
	}

	[Fact]
	public void SetRequiredPermissionWhenProvided()
	{
		// Arrange & Act
		var exception = new ForbiddenException("Report", "Generate", "reports:generate");

		// Assert
		exception.Resource.ShouldBe("Report");
		exception.Operation.ShouldBe("Generate");
		exception.RequiredPermission.ShouldBe("reports:generate");
	}

	[Fact]
	public void FormatMessageWithResourceAndOperation()
	{
		// Arrange & Act
		var exception = new ForbiddenException("Document", "Edit");

		// Assert
		exception.Message.ShouldContain("Document");
		exception.Message.ShouldContain("Edit");
		exception.Message.ShouldContain("permission");
	}

	[Fact]
	public void FormatMessageWithRequiredPermission()
	{
		// Arrange & Act
		var exception = new ForbiddenException("System", "Configure", "admin:config");

		// Assert
		exception.Message.ShouldContain("admin:config");
	}

	[Fact]
	public void CreateMissingRoleException()
	{
		// Arrange & Act
		var exception = ForbiddenException.MissingRole("Admin Panel", "Access", "Administrator");

		// Assert
		exception.Resource.ShouldBe("Admin Panel");
		exception.Operation.ShouldBe("Access");
		exception.RequiredPermission.ShouldBe("Role:Administrator");
		exception.Context.ShouldContainKeyAndValue("requiredRole", "Administrator");
	}

	[Fact]
	public void CreateSubscriptionRequiredException()
	{
		// Arrange & Act
		var exception = ForbiddenException.SubscriptionRequired("Advanced Analytics", "Enterprise");

		// Assert
		exception.Message.ShouldContain("Enterprise");
		exception.Message.ShouldContain("subscription");
		exception.Context.ShouldContainKeyAndValue("feature", "Advanced Analytics");
		exception.Context.ShouldContainKeyAndValue("requiredTier", "Enterprise");
	}

	[Fact]
	public void IncludeOperationInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new ForbiddenException("User", "Delete", "users:delete");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "User");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("operation", "Delete");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("requiredPermission", "users:delete");
	}

	[Fact]
	public void IncludeDataInContext()
	{
		// Arrange & Act
		var exception = new ForbiddenException("Settings", "Modify");

		// Assert
		exception.Context.ShouldContainKeyAndValue("resource", "Settings");
		exception.Context.ShouldContainKeyAndValue("operation", "Modify");
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ForbiddenException("Custom forbidden message");

		// Assert
		exception.Message.ShouldBe("Custom forbidden message");
		exception.DispatchStatusCode.ShouldBe(403);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ForbiddenException("Access denied", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ForbiddenException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		// Arrange & Act
		var exception = new ForbiddenException();

		// Assert
		exception.Message.ShouldContain("forbidden");
	}
}
