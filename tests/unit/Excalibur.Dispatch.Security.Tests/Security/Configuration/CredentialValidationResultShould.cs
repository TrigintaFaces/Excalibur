// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Configuration;

/// <summary>
/// Unit tests for <see cref="CredentialValidationResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class CredentialValidationResultShould
{
	[Fact]
	public void ReturnValidResult_WhenSuccess()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyErrors_WhenSuccess()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Success();

		// Assert
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnInvalidResult_WhenFailure()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure("Error");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ContainSingleError_WhenFailureWithOneError()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure("Password too short");

		// Assert
		result.Errors.Length.ShouldBe(1);
		result.Errors[0].ShouldBe("Password too short");
	}

	[Fact]
	public void ContainMultipleErrors_WhenFailureWithMultipleErrors()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure(
			"Password too short",
			"Password must contain uppercase",
			"Password must contain digit");

		// Assert
		result.Errors.Length.ShouldBe(3);
		result.Errors.ShouldContain("Password too short");
		result.Errors.ShouldContain("Password must contain uppercase");
		result.Errors.ShouldContain("Password must contain digit");
	}

	[Fact]
	public void PreserveErrorOrder_WhenFailureWithMultipleErrors()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure("Error A", "Error B", "Error C");

		// Assert
		result.Errors[0].ShouldBe("Error A");
		result.Errors[1].ShouldBe("Error B");
		result.Errors[2].ShouldBe("Error C");
	}

	[Fact]
	public void HaveEmptyErrors_WhenFailureWithNoErrors()
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void AllowParamsArrayForErrors()
	{
		// Arrange
		var errors = new[] { "Error 1", "Error 2" };

		// Act
		var result = CredentialValidationResult.Failure(errors);

		// Assert
		result.Errors.Length.ShouldBe(2);
	}

	[Fact]
	public void BeImmutable_ForIsValid()
	{
		// Arrange
		var success = CredentialValidationResult.Success();
		var failure = CredentialValidationResult.Failure("Error");

		// Assert - IsValid is a get-only property
		success.IsValid.ShouldBeTrue();
		failure.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void BeImmutable_ForErrors()
	{
		// Arrange
		var result = CredentialValidationResult.Failure("Error");

		// Assert - Errors is a get-only property
		result.Errors.Length.ShouldBe(1);
	}

	[Fact]
	public void CreateDistinctInstances_ForMultipleSuccessCalls()
	{
		// Arrange & Act
		var result1 = CredentialValidationResult.Success();
		var result2 = CredentialValidationResult.Success();

		// Assert - should create separate instances
		result1.ShouldNotBeSameAs(result2);
	}

	[Theory]
	[InlineData("Password too short")]
	[InlineData("Password must contain uppercase letter")]
	[InlineData("Password must contain lowercase letter")]
	[InlineData("Password must contain digit")]
	[InlineData("Password must contain special character")]
	[InlineData("Password matches prohibited value")]
	public void SupportVariousErrorMessages(string errorMessage)
	{
		// Arrange & Act
		var result = CredentialValidationResult.Failure(errorMessage);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(errorMessage);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(CredentialValidationResult).IsSealed.ShouldBeTrue();
	}
}
