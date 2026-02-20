// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Routing;
using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Authentication;

/// <summary>
/// Unit tests for <see cref="AuthenticationFailedResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class AuthenticationFailedResultShould
{
	[Fact]
	public void HaveFalseSucceeded_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void HaveNullProblemDetails_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.ProblemDetails.ShouldBeNull();
	}

	[Fact]
	public void HaveNullRoutingDecision_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.RoutingDecision.ShouldBeNull();
	}

	[Fact]
	public void HaveNullValidationResult_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.ValidationResult.ShouldBeNull();
	}

	[Fact]
	public void HaveNullAuthorizationResult_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.AuthorizationResult.ShouldBeNull();
	}

	[Fact]
	public void HaveNullErrorMessage_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.ErrorMessage.ShouldBeNull();
	}

	[Fact]
	public void HaveFalseCacheHit_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.CacheHit.ShouldBeFalse();
	}

	[Fact]
	public void HaveMissingTokenReason_ByDefault()
	{
		// Arrange & Act
		var result = new AuthenticationFailedResult();

		// Assert
		result.Reason.ShouldBe(AuthenticationFailureReason.MissingToken);
	}

	[Fact]
	public void AllowSettingSucceeded()
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.Succeeded = true;

		// Assert
		result.Succeeded.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingProblemDetails()
	{
		// Arrange
		var result = new AuthenticationFailedResult();
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
		var result = new AuthenticationFailedResult();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		result.RoutingDecision = routingDecision;

		// Assert
		result.RoutingDecision.ShouldBe(routingDecision);
	}

	[Fact]
	public void AllowSettingValidationResult()
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.ValidationResult = "validation-passed";

		// Assert
		result.ValidationResult.ShouldBe("validation-passed");
	}

	[Fact]
	public void AllowSettingAuthorizationResult()
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.AuthorizationResult = "not-authorized";

		// Assert
		result.AuthorizationResult.ShouldBe("not-authorized");
	}

	[Fact]
	public void AllowSettingErrorMessage()
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.ErrorMessage = "Token validation failed";

		// Assert
		result.ErrorMessage.ShouldBe("Token validation failed");
	}

	[Fact]
	public void AllowSettingCacheHit()
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.CacheHit = true;

		// Assert
		result.CacheHit.ShouldBeTrue();
	}

	[Theory]
	[InlineData(AuthenticationFailureReason.MissingToken)]
	[InlineData(AuthenticationFailureReason.InvalidToken)]
	[InlineData(AuthenticationFailureReason.TokenExpired)]
	[InlineData(AuthenticationFailureReason.ValidationError)]
	[InlineData(AuthenticationFailureReason.UnknownError)]
	public void AllowSettingReason(AuthenticationFailureReason reason)
	{
		// Arrange
		var result = new AuthenticationFailedResult();

		// Act
		result.Reason = reason;

		// Assert
		result.Reason.ShouldBe(reason);
	}

	[Fact]
	public void AllowCreatingWithAllProperties()
	{
		// Arrange
		var problemDetails = A.Fake<IMessageProblemDetails>();
		var routingDecision = RoutingDecision.Success("local", []);

		// Act
		var result = new AuthenticationFailedResult
		{
			Succeeded = false,
			ProblemDetails = problemDetails,
			RoutingDecision = routingDecision,
			ValidationResult = "valid",
			AuthorizationResult = "denied",
			ErrorMessage = "Invalid token signature",
			CacheHit = false,
			Reason = AuthenticationFailureReason.InvalidToken,
		};

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.ProblemDetails.ShouldBe(problemDetails);
		result.RoutingDecision.ShouldBe(routingDecision);
		result.ValidationResult.ShouldBe("valid");
		result.AuthorizationResult.ShouldBe("denied");
		result.ErrorMessage.ShouldBe("Invalid token signature");
		result.CacheHit.ShouldBeFalse();
		result.Reason.ShouldBe(AuthenticationFailureReason.InvalidToken);
	}

	[Fact]
	public void ImplementIMessageResult()
	{
		// Assert
		typeof(AuthenticationFailedResult).GetInterfaces().ShouldContain(typeof(IMessageResult));
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(AuthenticationFailedResult).IsSealed.ShouldBeTrue();
	}
}
