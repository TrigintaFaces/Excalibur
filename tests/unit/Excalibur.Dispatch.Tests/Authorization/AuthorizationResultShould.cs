// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;

namespace Excalibur.Dispatch.Tests.Authorization;

/// <summary>
/// Unit tests for <see cref="AuthorizationResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Dispatch")]
public sealed class AuthorizationResultShould
{
	#region Success Tests

	[Fact]
	public void Success_ReturnsAuthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Success_HasNullFailureMessage()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.FailureMessage.ShouldBeNull();
	}

	#endregion

	#region Failed Tests

	[Fact]
	public void Failed_ReturnsUnauthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Failed("Access denied");

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public void Failed_SetsFailureMessage()
	{
		// Arrange
		var message = "User does not have permission";

		// Act
		var result = AuthorizationResult.Failed(message);

		// Assert
		result.FailureMessage.ShouldBe(message);
	}

	[Theory]
	[InlineData("Access denied")]
	[InlineData("Insufficient permissions")]
	[InlineData("Resource not found")]
	[InlineData("Token expired")]
	public void Failed_WithVariousMessages_SetsMessage(string message)
	{
		// Act
		var result = AuthorizationResult.Failed(message);

		// Assert
		result.FailureMessage.ShouldBe(message);
		result.IsAuthorized.ShouldBeFalse();
	}

	#endregion

	#region Init Properties Tests

	[Fact]
	public void CanCreate_WithInitSyntax_Authorized()
	{
		// Act
		var result = new AuthorizationResult { IsAuthorized = true };

		// Assert
		result.IsAuthorized.ShouldBeTrue();
		result.FailureMessage.ShouldBeNull();
	}

	[Fact]
	public void CanCreate_WithInitSyntax_Unauthorized()
	{
		// Act
		var result = new AuthorizationResult
		{
			IsAuthorized = false,
			FailureMessage = "Custom failure"
		};

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.FailureMessage.ShouldBe("Custom failure");
	}

	#endregion

	#region Interface Implementation Tests

	[Fact]
	public void ImplementsIAuthorizationResult()
	{
		// Arrange
		var result = AuthorizationResult.Success();

		// Assert
		result.ShouldBeAssignableTo<IAuthorizationResult>();
	}

	[Fact]
	public void InterfaceProperties_MatchClassProperties()
	{
		// Arrange
		var result = AuthorizationResult.Failed("Test message");

		// Act
		IAuthorizationResult interfaceRef = result;

		// Assert
		interfaceRef.IsAuthorized.ShouldBe(result.IsAuthorized);
		interfaceRef.FailureMessage.ShouldBe(result.FailureMessage);
	}

	#endregion

	#region Default Values Tests

	[Fact]
	public void DefaultConstructor_HasFalseIsAuthorized()
	{
		// Act
		var result = new AuthorizationResult();

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public void DefaultConstructor_HasNullFailureMessage()
	{
		// Act
		var result = new AuthorizationResult();

		// Assert
		result.FailureMessage.ShouldBeNull();
	}

	#endregion
}
