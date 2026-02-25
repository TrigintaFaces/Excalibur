// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Jobs.Workflows;

namespace Excalibur.Jobs.Tests.Workflows;

/// <summary>
/// Unit tests for <see cref="WorkflowValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Jobs")]
[Trait("Feature", "Workflows")]
public sealed class WorkflowValidationResultShould : UnitTestBase
{
	[Fact]
	public void CreateSuccessResult()
	{
		// Act
		var result = WorkflowValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.ErrorMessage.ShouldBeNull();
		result.ValidationErrors.ShouldBeNull();
	}

	[Fact]
	public void CreateFailureResultWithSingleError()
	{
		// Act
		var result = WorkflowValidationResult.Failure("Something went wrong");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Something went wrong");
	}

	[Fact]
	public void CreateFailureResultWithMultipleErrors()
	{
		// Act
		var result = WorkflowValidationResult.Failure("Error 1", "Error 2", "Error 3");

		// Assert
		result.IsValid.ShouldBeFalse();
		_ = result.ValidationErrors.ShouldNotBeNull();
		result.ValidationErrors.Count.ShouldBe(3);
		result.ValidationErrors.ShouldContain("Error 1");
		result.ValidationErrors.ShouldContain("Error 2");
		result.ValidationErrors.ShouldContain("Error 3");
	}

	[Fact]
	public void CombineMultipleErrorsIntoErrorMessage()
	{
		// Act
		var result = WorkflowValidationResult.Failure("Error 1", "Error 2");

		// Assert
		result.ErrorMessage.ShouldContain("Error 1");
		result.ErrorMessage.ShouldContain("Error 2");
		result.ErrorMessage.ShouldContain(";");
	}

	[Fact]
	public void AllowSettingIsValidViaInitializer()
	{
		// Act
		var result = new WorkflowValidationResult { IsValid = true };

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void AllowSettingErrorMessageViaInitializer()
	{
		// Act
		var result = new WorkflowValidationResult { ErrorMessage = "Custom error" };

		// Assert
		result.ErrorMessage.ShouldBe("Custom error");
	}

	[Fact]
	public void AllowSettingValidationErrorsViaInitializer()
	{
		// Arrange
		var errors = new List<string> { "Error A", "Error B" };

		// Act
		var result = new WorkflowValidationResult { ValidationErrors = errors };

		// Assert
		result.ValidationErrors.ShouldBe(errors);
	}

	[Fact]
	public void HaveDefaultValuesOnConstruction()
	{
		// Act
		var result = new WorkflowValidationResult();

		// Assert
		result.IsValid.ShouldBeFalse(); // Default bool is false
		result.ErrorMessage.ShouldBeNull();
		result.ValidationErrors.ShouldBeNull();
	}

	[Fact]
	public void HandleSingleErrorInParamsArray()
	{
		// Act
		var result = WorkflowValidationResult.Failure("Single error");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.ErrorMessage.ShouldBe("Single error");
	}

	[Fact]
	public void HandleEmptyParamsArray()
	{
		// Act
		var result = WorkflowValidationResult.Failure();

		// Assert
		result.IsValid.ShouldBeFalse();
		_ = result.ValidationErrors.ShouldNotBeNull();
		result.ValidationErrors.Count.ShouldBe(0);
		result.ErrorMessage.ShouldBe(string.Empty);
	}
}
