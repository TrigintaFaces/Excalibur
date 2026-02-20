// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using System.Net;

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

// Use alias to disambiguate from Excalibur.Dispatch.Abstractions.ResourceException
using ResourceException = Excalibur.Dispatch.Exceptions.ResourceException;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Tests for <see cref="ConflictException"/> to verify resource conflict
/// error handling with HTTP 409 status code support.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ConflictExceptionShould
{
	[Fact]
	public void InheritFromResourceException()
	{
		// Arrange & Act
		var exception = new ConflictException();

		// Assert
		_ = exception.ShouldBeAssignableTo<ResourceException>();
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
	}

	[Fact]
	public void Use409StatusCode()
	{
		// Arrange & Act
		var exception = new ConflictException();

		// Assert
		exception.DispatchStatusCode.ShouldBe((int)HttpStatusCode.Conflict);
	}

	[Fact]
	public void UseResourceConflictErrorCode()
	{
		// Arrange & Act
		var exception = new ConflictException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ResourceConflict);
	}

	[Fact]
	public void SetResourceFieldAndReasonWhenCreated()
	{
		// Arrange
		var resource = "User";
		var field = "email";
		var reason = "A user with this email already exists.";

		// Act
		var exception = new ConflictException(resource, field, reason);

		// Assert
		exception.Resource.ShouldBe(resource);
		exception.Field.ShouldBe(field);
		exception.Reason.ShouldBe(reason);
	}

	[Fact]
	public void CreateWithReasonFactory()
	{
		// Arrange & Act
		var exception = ConflictException.WithReason("Order", "Cannot cancel a shipped order.");

		// Assert
		exception.Resource.ShouldBe("Order");
		exception.Reason.ShouldBe("Cannot cancel a shipped order.");
	}

	[Fact]
	public void CreateAlreadyExistsConflict()
	{
		// Arrange & Act
		var exception = ConflictException.AlreadyExists("User", "user@example.com");

		// Assert - AlreadyExists sets message but not Resource property
		exception.Message.ShouldContain("User");
		exception.Message.ShouldContain("user@example.com");
		exception.Message.ShouldContain("already exists");
		exception.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void CreateInvalidStateTransitionConflict()
	{
		// Arrange & Act
		var exception = ConflictException.InvalidStateTransition("Order", "Pending", "Cancelled");

		// Assert - InvalidStateTransition adds context but doesn't set Resource property
		exception.Context.ShouldContainKeyAndValue("currentState", "Pending");
		exception.Context.ShouldContainKeyAndValue("targetState", "Cancelled");
		exception.Message.ShouldContain("Pending");
		exception.Message.ShouldContain("Cancelled");
		exception.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void IncludeFieldInDispatchProblemDetailsExtensions()
	{
		// Arrange
		var exception = new ConflictException("User", "email", "Email already in use.");

		// Act
		var dispatchProblemDetails = exception.ToDispatchProblemDetails();

		// Assert - ToDispatchProblemDetails() includes context in Extensions
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("resource", "User");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("field", "email");
		dispatchProblemDetails.Extensions.ShouldContainKeyAndValue("reason", "Email already in use.");
	}

	[Fact]
	public void IncludeDataInContext()
	{
		// Arrange & Act
		var exception = new ConflictException("Product", "sku", "SKU already exists.");

		// Assert
		exception.Context.ShouldContainKeyAndValue("resource", "Product");
		exception.Context.ShouldContainKeyAndValue("field", "sku");
		exception.Context.ShouldContainKeyAndValue("reason", "SKU already exists.");
	}

	[Fact]
	public void CreateFromMessageOnly()
	{
		// Arrange & Act
		var exception = new ConflictException("Custom conflict message");

		// Assert
		exception.Message.ShouldBe("Custom conflict message");
		exception.DispatchStatusCode.ShouldBe(409);
	}

	[Fact]
	public void IncludeInnerExceptionWhenProvided()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");

		// Act
		var exception = new ConflictException("Conflict occurred", innerException);

		// Assert
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ConflictException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void HaveDefaultMessage()
	{
		// Arrange & Act
		var exception = new ConflictException();

		// Assert
		exception.Message.ShouldContain("conflict");
	}
}
