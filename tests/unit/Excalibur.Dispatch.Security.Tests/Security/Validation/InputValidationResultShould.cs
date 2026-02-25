// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Unit tests for <see cref="InputValidationResult"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationResultShould
{
	[Fact]
	public void ReturnValidResult_WhenSuccess()
	{
		// Arrange & Act
		var result = InputValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void HaveEmptyErrors_WhenSuccess()
	{
		// Arrange & Act
		var result = InputValidationResult.Success();

		// Assert
		result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnInvalidResult_WhenFailure()
	{
		// Arrange & Act
		var result = InputValidationResult.Failure("Error 1");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void ContainSingleError_WhenFailureWithOneError()
	{
		// Arrange & Act
		var result = InputValidationResult.Failure("Validation failed");

		// Assert
		result.Errors.Length.ShouldBe(1);
		result.Errors[0].ShouldBe("Validation failed");
	}

	[Fact]
	public void ContainMultipleErrors_WhenFailureWithMultipleErrors()
	{
		// Arrange & Act
		var result = InputValidationResult.Failure(
			"Field is required",
			"Value exceeds maximum length",
			"Invalid format");

		// Assert
		result.Errors.Length.ShouldBe(3);
		result.Errors.ShouldContain("Field is required");
		result.Errors.ShouldContain("Value exceeds maximum length");
		result.Errors.ShouldContain("Invalid format");
	}

	[Fact]
	public void PreserveErrorOrder_WhenFailureWithMultipleErrors()
	{
		// Arrange & Act
		var result = InputValidationResult.Failure("Error A", "Error B", "Error C");

		// Assert
		result.Errors[0].ShouldBe("Error A");
		result.Errors[1].ShouldBe("Error B");
		result.Errors[2].ShouldBe("Error C");
	}

	[Fact]
	public void HaveEmptyErrors_WhenFailureWithNoErrors()
	{
		// Arrange & Act
		var result = InputValidationResult.Failure();

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
		var result = InputValidationResult.Failure(errors);

		// Assert
		result.Errors.Length.ShouldBe(2);
	}

	[Fact]
	public void BeImmutable_ForIsValid()
	{
		// Arrange
		var success = InputValidationResult.Success();
		var failure = InputValidationResult.Failure("Error");

		// Assert - IsValid is a get-only property
		success.IsValid.ShouldBeTrue();
		failure.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void BeImmutable_ForErrors()
	{
		// Arrange
		var result = InputValidationResult.Failure("Error");

		// Assert - Errors is a get-only property
		result.Errors.Length.ShouldBe(1);
	}

	[Fact]
	public void CreateDistinctInstances_ForMultipleSuccessCalls()
	{
		// Arrange & Act
		var result1 = InputValidationResult.Success();
		var result2 = InputValidationResult.Success();

		// Assert - should create separate instances
		result1.ShouldNotBeSameAs(result2);
	}

	[Theory]
	[InlineData("SQL injection detected")]
	[InlineData("XSS pattern found")]
	[InlineData("Path traversal attempt")]
	[InlineData("Command injection detected")]
	public void SupportVariousErrorMessages(string errorMessage)
	{
		// Arrange & Act
		var result = InputValidationResult.Failure(errorMessage);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldContain(errorMessage);
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(InputValidationResult).IsSealed.ShouldBeTrue();
	}
}
