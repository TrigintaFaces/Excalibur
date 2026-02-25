// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions.Validation;

namespace Excalibur.Dispatch.Abstractions.Tests.Validation;

/// <summary>
/// Unit tests for <see cref="ValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Validation")]
[Trait("Priority", "0")]
public sealed class ValidationResultShould
{
	#region Default Values Tests

	[Fact]
	public void Default_IsValidIsFalse()
	{
		// Arrange & Act
		var result = new ValidationResult();

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Default_ErrorsIsEmpty()
	{
		// Arrange & Act
		var result = new ValidationResult();

		// Assert
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Default_WarningsIsEmpty()
	{
		// Arrange & Act
		var result = new ValidationResult();

		// Assert
		_ = result.Warnings.ShouldNotBeNull();
		result.Warnings.ShouldBeEmpty();
	}

	#endregion

	#region Init-Only Property Tests

	[Fact]
	public void IsValid_CanBeSetViaInitializer()
	{
		// Act
		var result = new ValidationResult { IsValid = true };

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Errors_CanBeSetViaInitializer()
	{
		// Arrange
		var errors = new List<ValidationError> { new("Test error") };

		// Act
		var result = new ValidationResult { Errors = errors };

		// Assert
		result.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void Warnings_CanBeSetViaInitializer()
	{
		// Arrange
		var warnings = new List<string> { "Test warning" };

		// Act
		var result = new ValidationResult { Warnings = warnings };

		// Assert
		result.Warnings.Count.ShouldBe(1);
	}

	#endregion

	#region Success Factory Tests

	[Fact]
	public void Success_SetsIsValidToTrue()
	{
		// Act
		var result = ValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Success_HasNoErrors()
	{
		// Act
		var result = ValidationResult.Success();

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Success_HasNoWarnings()
	{
		// Act
		var result = ValidationResult.Success();

		// Assert
		result.Warnings.ShouldBeEmpty();
	}

	#endregion

	#region SuccessWithWarnings Factory Tests

	[Fact]
	public void SuccessWithWarnings_SetsIsValidToTrue()
	{
		// Act
		var result = ValidationResult.SuccessWithWarnings("Warning 1");

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void SuccessWithWarnings_SetsWarnings()
	{
		// Act
		var result = ValidationResult.SuccessWithWarnings("Warning 1", "Warning 2");

		// Assert
		result.Warnings.Count.ShouldBe(2);
		result.Warnings.ShouldContain("Warning 1");
		result.Warnings.ShouldContain("Warning 2");
	}

	[Fact]
	public void SuccessWithWarnings_HasNoErrors()
	{
		// Act
		var result = ValidationResult.SuccessWithWarnings("Warning 1");

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region Failure Factory Tests (ValidationError[])

	[Fact]
	public void Failure_WithValidationErrors_SetsIsValidToFalse()
	{
		// Arrange
		var errors = new[] { new ValidationError("Error 1") };

		// Act
		var result = ValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Failure_WithValidationErrors_SetsErrors()
	{
		// Arrange
		var errors = new[] { new ValidationError("Error 1"), new ValidationError("Error 2") };

		// Act
		var result = ValidationResult.Failure(errors);

		// Assert
		result.Errors.Count.ShouldBe(2);
	}

	#endregion

	#region Failure Factory Tests (string[])

	[Fact]
	public void Failure_WithStrings_SetsIsValidToFalse()
	{
		// Act
		var result = ValidationResult.Failure("Error 1");

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Failure_WithStrings_CreatesValidationErrors()
	{
		// Act
		var result = ValidationResult.Failure("Error 1", "Error 2");

		// Assert
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].Message.ShouldBe("Error 1");
		result.Errors[1].Message.ShouldBe("Error 2");
	}

	[Fact]
	public void Failure_WithNullStrings_HasEmptyErrors()
	{
		// Act
		var result = ValidationResult.Failure(errorMessages: null!);

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Failure_WithEmptyStrings_HasEmptyErrors()
	{
		// Act
		var result = ValidationResult.Failure(Array.Empty<string>());

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	#endregion

	#region FailureWithWarnings Factory Tests

	[Fact]
	public void FailureWithWarnings_SetsIsValidToFalse()
	{
		// Arrange
		var errors = new[] { new ValidationError("Error 1") };
		var warnings = new[] { "Warning 1" };

		// Act
		var result = ValidationResult.FailureWithWarnings(errors, warnings);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void FailureWithWarnings_SetsErrors()
	{
		// Arrange
		var errors = new[] { new ValidationError("Error 1"), new ValidationError("Error 2") };
		var warnings = new[] { "Warning 1" };

		// Act
		var result = ValidationResult.FailureWithWarnings(errors, warnings);

		// Assert
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void FailureWithWarnings_SetsWarnings()
	{
		// Arrange
		var errors = new[] { new ValidationError("Error 1") };
		var warnings = new[] { "Warning 1", "Warning 2" };

		// Act
		var result = ValidationResult.FailureWithWarnings(errors, warnings);

		// Assert
		result.Warnings.Count.ShouldBe(2);
	}

	#endregion
}
