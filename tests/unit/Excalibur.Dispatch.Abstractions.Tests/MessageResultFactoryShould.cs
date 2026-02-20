// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

namespace Excalibur.Dispatch.Abstractions.Tests;

/// <summary>
/// Unit tests for <see cref="MessageResult"/> static factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class MessageResultFactoryShould
{
	[Fact]
	public void Success_ReturnsSuccessfulResult()
	{
		// Act
		var result = MessageResult.Success();

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void SuccessFromCache_ReturnsCacheHitResult()
	{
		// Act
		var result = MessageResult.SuccessFromCache();

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void Success_WithRoutingAndValidation_ReturnsSuccessfulResult()
	{
		// Act
		var result = MessageResult.Success(
			routingDecision: null,
			validationResult: "valid",
			authorizationResult: "authorized",
			cacheHit: true);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.CacheHit.ShouldBeTrue();
	}

	[Fact]
	public void SuccessT_ReturnsResultWithValue()
	{
		// Act
		var result = MessageResult.Success(42);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe(42);
	}

	[Fact]
	public void SuccessT_WithFullContext_ReturnsResultWithValue()
	{
		// Act
		var result = MessageResult.Success(
			value: "test-value",
			routingDecision: null,
			validationResult: null,
			authorizationResult: null,
			cacheHit: false);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.ReturnValue.ShouldBe("test-value");
	}

	[Fact]
	public void Failed_WithErrorMessage_ReturnsFailedResult()
	{
		// Act
		var result = MessageResult.Failed("Something went wrong");

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void Failed_WithProblemDetails_ReturnsFailedResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		A.CallTo(() => problemDetails.Detail).Returns("Detailed error");

		// Act
		var result = MessageResult.Failed(problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void Failed_WithNullProblemDetails_DoesNotThrow()
	{
		// Act
		var result = MessageResult.Failed((IMessageProblemDetails?)null!);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void FailedT_WithErrorMessage_ReturnsFailedResult()
	{
		// Act
		var result = MessageResult.Failed<int>("Error occurred");

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Error occurred");
	}

	[Fact]
	public void FailedT_WithProblemDetails_ReturnsFailedResult()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();

		// Act
		var result = MessageResult.Failed<string>("Error", problemDetails);

		// Assert
		result.Succeeded.ShouldBeFalse();
	}
}
