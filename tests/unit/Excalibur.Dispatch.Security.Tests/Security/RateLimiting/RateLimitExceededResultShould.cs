// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.RateLimiting;

/// <summary>
/// Unit tests for <see cref="RateLimitExceededResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class RateLimitExceededResultShould
{
	[Fact]
	public void HaveFalseSucceeded_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullProblemDetails_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void HaveNullRoutingDecision_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.RoutingDecision.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidationResult_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.ValidationResult.ShouldBeNull();
	}

	[Fact]
	public void HaveNullAuthorizationResult_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.AuthorizationResult.ShouldBeNull();
	}

	[Fact]
	public void HaveNullErrorMessage_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseCacheHit_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.CacheHit.ShouldBeFalse();
	}

	[Fact]
	public void HaveZeroRetryAfterMilliseconds_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.RetryAfterMilliseconds.ShouldBe(0);
	}

	[Fact]
	public void HaveNullRateLimitKey_ByDefault()
	{
		// Arrange & Act
		var result = new RateLimitExceededResult();

		// Assert
		result.RateLimitKey.ShouldBeNull();
	}

	[Fact]
	public void AllowSettingSucceeded()
	{
		// Arrange
		var result = new RateLimitExceededResult();

		// Act
		result.Succeeded = true;

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingProblemDetails()
	{
		// Arrange
		var result = new RateLimitExceededResult();
		var problemDetails = A.Fake<IMessageProblemDetails>();

		// Act
		result.ProblemDetails = problemDetails;

		// Assert
		result.ProblemDetails.ShouldBe(problemDetails);
	}

	[Fact]
	public void AllowSettingRoutingDecision()
	{
		// Arrange
		var result = new RateLimitExceededResult();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		result.RoutingDecision = routingDecision;

		// Assert
		result.RoutingDecision.ShouldBe(routingDecision);
	}

	[Fact]
	public void AllowSettingErrorMessage()
	{
		// Arrange
		var result = new RateLimitExceededResult();

		// Act
		result.ErrorMessage = "Rate limit exceeded for tenant-123";

		// Assert
		result.ErrorMessage.ShouldBe("Rate limit exceeded for tenant-123");
	}

	[Fact]
	public void AllowSettingRetryAfterMilliseconds()
	{
		// Arrange
		var result = new RateLimitExceededResult();

		// Act
		result.RetryAfterMilliseconds = 5000;

		// Assert
		result.RetryAfterMilliseconds.ShouldBe(5000);
	}

	[Fact]
	public void AllowSettingRateLimitKey()
	{
		// Arrange
		var result = new RateLimitExceededResult();

		// Act
		result.RateLimitKey = "tenant:premium";

		// Assert
		result.RateLimitKey.ShouldBe("tenant:premium");
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		var result = new RateLimitExceededResult
		{
			Succeeded = false,
			ProblemDetails = problemDetails,
			RoutingDecision = routingDecision,
			ValidationResult = "valid",
			AuthorizationResult = "authorized",
			ErrorMessage = "Too many requests",
			CacheHit = false,
			RetryAfterMilliseconds = 10000,
			RateLimitKey = "user:12345",
		};

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.RoutingDecision.ShouldBe(routingDecision);
		result.ValidationResult.ShouldBe("valid");
		result.AuthorizationResult.ShouldBe("authorized");
		result.ErrorMessage.ShouldBe("Too many requests");
		result.CacheHit.ShouldBeFalse();
		result.RetryAfterMilliseconds.ShouldBe(10000);
		result.RateLimitKey.ShouldBe("user:12345");
	}

	[Fact]
	public void ImplementIMessageResult()
	{
		// Assert
		typeof(RateLimitExceededResult).GetInterfaces().ShouldContain(typeof(IMessageResult));
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(RateLimitExceededResult).IsSealed.ShouldBeTrue();
	}
}
