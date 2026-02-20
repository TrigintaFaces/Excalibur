// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Depth coverage tests for <see cref="MessageProblemDetails"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageProblemDetailsDepthShould
{
	[Fact]
	public void Extensions_UsesOrdinalComparer()
	{
		// Arrange
		var details = new MessageProblemDetails();

		// Act
		details.Extensions["Key"] = "uppercase";
		details.Extensions["key"] = "lowercase";

		// Assert — ordinal comparer treats Key and key as different
		details.Extensions.Count.ShouldBe(2);
	}

	[Fact]
	public void Extensions_SupportsNullValues()
	{
		// Arrange
		var details = new MessageProblemDetails();

		// Act
		details.Extensions["nullable"] = null;

		// Assert
		details.Extensions.ShouldContainKey("nullable");
		details.Extensions["nullable"].ShouldBeNull();
	}

	[Fact]
	public void Extensions_SupportsComplexValues()
	{
		// Arrange
		var details = new MessageProblemDetails();
		var complexValue = new { Field = "email", Code = 42 };

		// Act
		details.Extensions["complex"] = complexValue;

		// Assert
		details.Extensions["complex"].ShouldBe(complexValue);
	}

	[Fact]
	public void ValidationError_ReturnsProblemDetailsType()
	{
		// Act
		var result = MessageProblemDetails.ValidationError("test");

		// Assert
		result.ShouldBeOfType<MessageProblemDetails>();
	}

	[Fact]
	public void AuthorizationError_ReturnsProblemDetailsType()
	{
		// Act
		var result = MessageProblemDetails.AuthorizationError("test");

		// Assert
		result.ShouldBeOfType<MessageProblemDetails>();
	}

	[Fact]
	public void NotFound_ReturnsProblemDetailsType()
	{
		// Act
		var result = MessageProblemDetails.NotFound("test");

		// Assert
		result.ShouldBeOfType<MessageProblemDetails>();
	}

	[Fact]
	public void InternalError_ReturnsProblemDetailsType()
	{
		// Act
		var result = MessageProblemDetails.InternalError("test");

		// Assert
		result.ShouldBeOfType<MessageProblemDetails>();
	}

	[Fact]
	public void Type_DefaultsToAboutBlank()
	{
		// Arrange & Act
		var details = new MessageProblemDetails();

		// Assert
		details.Type.ShouldBe("about:blank");
	}

	[Fact]
	public void Title_DefaultsToError()
	{
		// Arrange & Act
		var details = new MessageProblemDetails();

		// Assert
		details.Title.ShouldBe("Error");
	}

	[Fact]
	public void ErrorCode_CanBeSet()
	{
		// Arrange
		var details = new MessageProblemDetails { ErrorCode = 42 };

		// Assert
		details.ErrorCode.ShouldBe(42);
	}

	[Fact]
	public void Status_CanBeSetToNull()
	{
		// Arrange
		var details = new MessageProblemDetails { Status = 200 };

		// Act
		details.Status = null;

		// Assert
		details.Status.ShouldBeNull();
	}

	[Fact]
	public void Detail_DefaultsToEmptyString()
	{
		// Assert
		new MessageProblemDetails().Detail.ShouldBe(string.Empty);
	}

	[Fact]
	public void Instance_DefaultsToEmptyString()
	{
		// Assert
		new MessageProblemDetails().Instance.ShouldBe(string.Empty);
	}

	[Fact]
	public void ValidationError_SetsErrorCodeToZero()
	{
		// Act
		var result = (MessageProblemDetails)MessageProblemDetails.ValidationError("test");

		// Assert
		result.ErrorCode.ShouldBe(0);
	}

	[Fact]
	public void Extensions_IsEmptyByDefault()
	{
		// Assert
		new MessageProblemDetails().Extensions.ShouldBeEmpty();
	}
}
