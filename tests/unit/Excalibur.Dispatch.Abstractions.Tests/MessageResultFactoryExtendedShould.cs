// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Extended unit tests for the <see cref="MessageResult"/> static factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class MessageResultFactoryExtendedShould
{
	[Fact]
	public void Success_Should_ReturnSucceededResult()
	{
		// Act
		var result = MessageResult.Success();

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ErrorMessage.ShouldBeNull();
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void SuccessFromCache_Should_ReturnCacheHitResult()
	{
		// Act
		var result = MessageResult.SuccessFromCache();

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void SuccessFromCache_Generic_Should_ReturnValueWithCacheHit()
	{
		// Act
		var result = MessageResult.SuccessFromCache(42);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public void Success_WithParameters_Should_SetAllProperties()
	{
		// Arrange
		var validationResult = new object();
		var authResult = new object();

		// Act
		var result = MessageResult.Success(null, validationResult, authResult, cacheHit: true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
		result.ValidationResult.ShouldBe(validationResult);
		result.AuthorizationResult.ShouldBe(authResult);
	}

	[Fact]
	public void Success_Generic_Should_ReturnValue()
	{
		// Act
		var result = MessageResult.Success("hello");

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe("hello");
	}

	[Fact]
	public void Success_Generic_WithParameters_Should_SetAllProperties()
	{
		// Act
		var result = MessageResult.Success(
			value: 42,
			validationResult: "valid",
			authorizationResult: "authorized",
			cacheHit: true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
		result.CacheHit.ShouldBeTrue();
		result.ValidationResult.ShouldBe("valid");
		result.AuthorizationResult.ShouldBe("authorized");
	}

	[Fact]
	public void Failed_WithString_Should_ReturnFailedResult()
	{
		// Act
		var result = MessageResult.Failed("Something went wrong");

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void Failed_WithProblemDetails_Should_ReturnFailedResult()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails
		{
			Type = "validation-error",
			Detail = "Name is required",
		};

		// Act
		var result = MessageResult.Failed(problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Name is required");
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.Type.ShouldBe("validation-error");
	}

	[Fact]
	public void Failed_Generic_Should_ReturnFailedResult()
	{
		// Act
		var result = MessageResult.Failed<int>("Error occurred");

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Error occurred");
		result.ReturnValue.ShouldBe(default);
	}

	[Fact]
	public void Failed_Generic_WithProblemDetails_Should_SetBoth()
	{
		// Arrange
		var problemDetails = new MessageProblemDetails { ErrorCode = 404 };

		// Act
		var result = MessageResult.Failed<string>("Not found", problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Not found");
		result.ProblemDetails.ShouldNotBeNull();
		result.ProblemDetails.ErrorCode.ShouldBe(404);
	}
}
