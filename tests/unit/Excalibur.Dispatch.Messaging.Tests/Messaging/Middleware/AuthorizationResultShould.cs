// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="AuthorizationResult"/>.
/// </summary>
/// <remarks>
/// Tests the authorization result class with Success and Failure factory methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class AuthorizationResultShould
{
	#region Success Factory Method Tests

	[Fact]
	public void Success_ReturnsAuthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.IsAuthorized.ShouldBeTrue();
	}

	[Fact]
	public void Success_ReturnsNullReason()
	{
		// Act
		var result = AuthorizationResult.Success();

		// Assert
		result.Reason.ShouldBeNull();
	}

	[Fact]
	public void Success_MultipleCallsReturnIndependentInstances()
	{
		// Act
		var result1 = AuthorizationResult.Success();
		var result2 = AuthorizationResult.Success();

		// Assert
		result1.ShouldNotBeSameAs(result2);
	}

	#endregion

	#region Failure Factory Method Tests

	[Fact]
	public void Failure_ReturnsUnauthorizedResult()
	{
		// Act
		var result = AuthorizationResult.Failure("Access denied");

		// Assert
		result.IsAuthorized.ShouldBeFalse();
	}

	[Fact]
	public void Failure_SetsReason()
	{
		// Arrange
		const string reason = "User does not have required role";

		// Act
		var result = AuthorizationResult.Failure(reason);

		// Assert
		result.Reason.ShouldBe(reason);
	}

	[Fact]
	public void Failure_WithEmptyReason_AcceptsEmptyString()
	{
		// Act
		var result = AuthorizationResult.Failure(string.Empty);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.Reason.ShouldBe(string.Empty);
	}

	[Fact]
	public void Failure_WithDetailedReason_PreservesFullMessage()
	{
		// Arrange
		const string detailedReason = "User 'john.doe@example.com' lacks 'Admin' role required for operation 'DeleteUser'";

		// Act
		var result = AuthorizationResult.Failure(detailedReason);

		// Assert
		result.Reason.ShouldBe(detailedReason);
	}

	#endregion

	#region Property Immutability Tests

	[Fact]
	public void IsAuthorized_IsReadOnly()
	{
		// Arrange
		var result = AuthorizationResult.Success();

		// Assert - Property has no setter (compile-time check implied)
		var propertyInfo = typeof(AuthorizationResult).GetProperty(nameof(AuthorizationResult.IsAuthorized));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Reason_IsReadOnly()
	{
		// Arrange
		var result = AuthorizationResult.Failure("test");

		// Assert - Property has no setter (compile-time check implied)
		var propertyInfo = typeof(AuthorizationResult).GetProperty(nameof(AuthorizationResult.Reason));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInConditionalCheck()
	{
		// Arrange
		var successResult = AuthorizationResult.Success();
		var failureResult = AuthorizationResult.Failure("Not allowed");

		// Act & Assert
		if (successResult.IsAuthorized)
		{
			// Expected path for success
			successResult.Reason.ShouldBeNull();
		}
		else
		{
			Assert.Fail("Success result should be authorized");
		}

		if (!failureResult.IsAuthorized)
		{
			// Expected path for failure
			_ = failureResult.Reason.ShouldNotBeNull();
		}
		else
		{
			Assert.Fail("Failure result should not be authorized");
		}
	}

	[Fact]
	public void SuccessAndFailure_AreMutuallyExclusive()
	{
		// Arrange
		var success = AuthorizationResult.Success();
		var failure = AuthorizationResult.Failure("reason");

		// Assert
		success.IsAuthorized.ShouldNotBe(failure.IsAuthorized);
	}

	[Theory]
	[InlineData("Missing permission: read")]
	[InlineData("Token expired")]
	[InlineData("IP address not whitelisted")]
	[InlineData("Account suspended")]
	public void Failure_AcceptsVariousReasons(string reason)
	{
		// Act
		var result = AuthorizationResult.Failure(reason);

		// Assert
		result.IsAuthorized.ShouldBeFalse();
		result.Reason.ShouldBe(reason);
	}

	#endregion
}
