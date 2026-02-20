// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// Licensed under the Excalibur License 1.0 - see LICENSE files for details.

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Abstractions.Tests.Authorization;

/// <summary>
/// Unit tests for the <see cref="AuthorizationResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Abstractions")]
public sealed class AuthorizationResultShould
{
	[Fact]
	public void Success_Should_ReturnAuthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.IsAuthorized.ShouldBeTrue();
		result.FailureMessage.ShouldBeNull();
	}

	[Fact]
	public void Failed_Should_ReturnUnauthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Failed("Insufficient permissions");

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.FailureMessage.ShouldBe("Insufficient permissions");
	}

	[Fact]
	public void Implement_IAuthorizationResult()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.ShouldBeAssignableTo<IAuthorizationResult>();
	}

	[Fact]
	public void Support_InitSyntax()
	{
		// Act
		var result = new AuthorizationResult { IsAuthorized = true, FailureMessage = null };

		// Assert
		result.IsAuthorized.ShouldBeTrue();
		result.FailureMessage.ShouldBeNull();
	}
}
