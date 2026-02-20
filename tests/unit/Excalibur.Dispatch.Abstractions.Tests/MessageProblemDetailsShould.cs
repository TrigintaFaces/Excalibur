// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for the <see cref="MessageProblemDetails"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class MessageProblemDetailsShould
{
	[Fact]
	public void Have_DefaultValues_WhenCreated()
	{
		// Act
		var details = new MessageProblemDetails();

		// Assert
		details.Type.ShouldBe("about:blank");
		details.Title.ShouldBe("Error");
		details.ErrorCode.ShouldBe(0);
		details.Status.ShouldBeNull();
		details.Detail.ShouldBe(string.Empty);
		details.Instance.ShouldBe(string.Empty);
		details.Extensions.ShouldNotBeNull();
		details.Extensions.Count.ShouldBe(0);
	}

	[Fact]
	public void Allow_SettingAllProperties()
	{
		// Act
		var details = new MessageProblemDetails
		{
			Type = "urn:error:custom",
			Title = "Custom Error",
			ErrorCode = 1234,
			Status = 422,
			Detail = "Validation failed for field X",
			Instance = "/api/orders/123",
		};

		// Assert
		details.Type.ShouldBe("urn:error:custom");
		details.Title.ShouldBe("Custom Error");
		details.ErrorCode.ShouldBe(1234);
		details.Status.ShouldBe(422);
		details.Detail.ShouldBe("Validation failed for field X");
		details.Instance.ShouldBe("/api/orders/123");
	}

	[Fact]
	public void Support_ExtensionsDictionary()
	{
		// Arrange
		var details = new MessageProblemDetails();

		// Act
		details.Extensions["retryable"] = true;
		details.Extensions["field"] = "email";

		// Assert
		details.Extensions.Count.ShouldBe(2);
		details.Extensions["retryable"].ShouldBe(true);
		details.Extensions["field"].ShouldBe("email");
	}

	[Fact]
	public void ValidationError_Should_CreateCorrectProblemDetails()
	{
		// Act
		var result = MessageProblemDetails.ValidationError("Name is required");

		// Assert
		result.ShouldNotBeNull();
		result.Type.ShouldBe("validation-error");
		result.Title.ShouldBe("Validation Error");
		result.Detail.ShouldBe("Name is required");
		var concrete = result.ShouldBeOfType<MessageProblemDetails>();
		concrete.Status.ShouldBe(400);
	}

	[Fact]
	public void AuthorizationError_Should_CreateCorrectProblemDetails()
	{
		// Act
		var result = MessageProblemDetails.AuthorizationError("Insufficient permissions");

		// Assert
		result.ShouldNotBeNull();
		result.Type.ShouldBe("authorization-error");
		result.Title.ShouldBe("Authorization Error");
		result.Detail.ShouldBe("Insufficient permissions");
		var concrete = result.ShouldBeOfType<MessageProblemDetails>();
		concrete.Status.ShouldBe(403);
	}

	[Fact]
	public void NotFound_Should_CreateCorrectProblemDetails()
	{
		// Act
		var result = MessageProblemDetails.NotFound("Order 123 not found");

		// Assert
		result.ShouldNotBeNull();
		result.Type.ShouldBe("not-found");
		result.Title.ShouldBe("Not Found");
		result.Detail.ShouldBe("Order 123 not found");
		var concrete = result.ShouldBeOfType<MessageProblemDetails>();
		concrete.Status.ShouldBe(404);
	}

	[Fact]
	public void InternalError_Should_CreateCorrectProblemDetails()
	{
		// Act
		var result = MessageProblemDetails.InternalError("Database connection failed");

		// Assert
		result.ShouldNotBeNull();
		result.Type.ShouldBe("internal-error");
		result.Title.ShouldBe("Internal Error");
		result.Detail.ShouldBe("Database connection failed");
		var concrete = result.ShouldBeOfType<MessageProblemDetails>();
		concrete.Status.ShouldBe(500);
	}

	[Fact]
	public void Implement_IMessageProblemDetails()
	{
		// Arrange & Act
		var details = new MessageProblemDetails();

		// Assert
		details.ShouldBeAssignableTo<IMessageProblemDetails>();
	}
}
