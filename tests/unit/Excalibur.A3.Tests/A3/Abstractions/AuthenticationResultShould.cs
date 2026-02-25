// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.A3.Abstractions.Authentication;

namespace Excalibur.Tests.A3.Abstractions;

/// <summary>
/// Unit tests for <see cref="AuthenticationResult"/> record.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "A3")]
public sealed class AuthenticationResultShould : UnitTestBase
{
	#region Constructor Tests

	[Fact]
	public void Create_SuccessfulResult_HasPrincipal()
	{
		// Arrange
		var principal = new AuthenticatedPrincipal("user-123", null, null);

		// Act
		var result = new AuthenticationResult(Succeeded: true, Principal: principal);

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Principal.ShouldBe(principal);
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Create_FailedResult_HasNoNullPrincipal()
	{
		// Arrange & Act
		var result = new AuthenticationResult(Succeeded: false, Principal: null, FailureReason: "Invalid token");

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.Principal.ShouldBeNull();
		result.FailureReason.ShouldBe("Invalid token");
	}

	[Fact]
	public void Create_FailedResultWithoutReason_HasNullReason()
	{
		// Arrange & Act
		var result = new AuthenticationResult(Succeeded: false, Principal: null);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.Principal.ShouldBeNull();
		result.FailureReason.ShouldBeNull();
	}

	[Fact]
	public void Create_SuccessfulWithReason_SetsAllValues()
	{
		// Arrange
		var principal = new AuthenticatedPrincipal("user-123", "tenant-a", null);

		// Act
		var result = new AuthenticationResult(true, principal, "Token refreshed");

		// Assert
		result.Succeeded.ShouldBeTrue();
		result.Principal.ShouldBe(principal);
		result.FailureReason.ShouldBe("Token refreshed");
	}

	#endregion

	#region Equality Tests

	[Fact]
	public void Equality_SameSucceededAndPrincipal_AreEqual()
	{
		// Arrange
		var principal = new AuthenticatedPrincipal("user-123", null, null);
		var result1 = new AuthenticationResult(true, principal);
		var result2 = new AuthenticationResult(true, principal);

		// Act & Assert
		result1.ShouldBe(result2);
	}

	[Fact]
	public void Equality_DifferentSucceeded_AreNotEqual()
	{
		// Arrange
		var principal = new AuthenticatedPrincipal("user-123", null, null);
		var result1 = new AuthenticationResult(true, principal);
		var result2 = new AuthenticationResult(false, null);

		// Act & Assert
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void Equality_DifferentFailureReason_AreNotEqual()
	{
		// Arrange
		var result1 = new AuthenticationResult(false, null, "Reason 1");
		var result2 = new AuthenticationResult(false, null, "Reason 2");

		// Act & Assert
		result1.ShouldNotBe(result2);
	}

	[Fact]
	public void Equality_BothFailedWithSameReason_AreEqual()
	{
		// Arrange
		var result1 = new AuthenticationResult(false, null, "Token expired");
		var result2 = new AuthenticationResult(false, null, "Token expired");

		// Act & Assert
		result1.ShouldBe(result2);
	}

	#endregion

	#region With Expression Tests

	[Fact]
	public void With_CreatesModifiedCopy_Succeeded()
	{
		// Arrange
		var principal = new AuthenticatedPrincipal("user-123", null, null);
		var original = new AuthenticationResult(true, principal);

		// Act
		var modified = original with { Succeeded = false };

		// Assert
		original.Succeeded.ShouldBeTrue();
		modified.Succeeded.ShouldBeFalse();
	}

	[Fact]
	public void With_CreatesModifiedCopy_FailureReason()
	{
		// Arrange
		var original = new AuthenticationResult(false, null);

		// Act
		var modified = original with { FailureReason = "Expired" };

		// Assert
		original.FailureReason.ShouldBeNull();
		modified.FailureReason.ShouldBe("Expired");
	}

	[Fact]
	public void With_CreatesModifiedCopy_Principal()
	{
		// Arrange
		var principal1 = new AuthenticatedPrincipal("user-123", null, null);
		var principal2 = new AuthenticatedPrincipal("user-456", null, null);
		var original = new AuthenticationResult(true, principal1);

		// Act
		var modified = original with { Principal = principal2 };

		// Assert
		original.Principal.SubjectId.ShouldBe("user-123");
		modified.Principal.SubjectId.ShouldBe("user-456");
	}

	#endregion

	#region Common Failure Scenarios

	[Theory]
	[InlineData("Token expired")]
	[InlineData("Invalid signature")]
	[InlineData("Token revoked")]
	[InlineData("Issuer not trusted")]
	[InlineData("Missing required claim")]
	public void Create_WithCommonFailureReasons_Succeeds(string failureReason)
	{
		// Act
		var result = new AuthenticationResult(false, null, failureReason);

		// Assert
		result.Succeeded.ShouldBeFalse();
		result.FailureReason.ShouldBe(failureReason);
	}

	#endregion
}
