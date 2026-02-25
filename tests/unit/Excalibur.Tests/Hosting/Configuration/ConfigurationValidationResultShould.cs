// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using ExcaliburConfigurationValidationError = Excalibur.Hosting.Configuration.ConfigurationValidationError;
using ExcaliburConfigurationValidationResult = Excalibur.Hosting.Configuration.ConfigurationValidationResult;

namespace Excalibur.Tests.Hosting.Configuration;

[Trait("Category", "Unit")]
public sealed class ExcaliburConfigurationValidationResultShould
{
	[Fact]
	public void CreateSuccessResult()
	{
		// Arrange & Act
		var result = ExcaliburConfigurationValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateFailureResultWithSingleError()
	{
		// Arrange & Act
		var result = ExcaliburConfigurationValidationResult.Failure("Error message", "Config:Path");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].Message.ShouldBe("Error message");
		result.Errors[0].ConfigurationPath.ShouldBe("Config:Path");
	}

	[Fact]
	public void CreateFailureResultWithMultipleErrors()
	{
		// Arrange
		var errors = new List<ExcaliburConfigurationValidationError> { new("Error 1"), new("Error 2") };

		// Act
		var result = ExcaliburConfigurationValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void CombineMultipleResults()
	{
		// Arrange
		var result1 = ExcaliburConfigurationValidationResult.Success();
		var result2 = ExcaliburConfigurationValidationResult.Failure("Error 1");
		var result3 = ExcaliburConfigurationValidationResult.Failure("Error 2");

		// Act
		var combined = ExcaliburConfigurationValidationResult.Combine(result1, result2, result3);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void CombineAllSuccessfulResults()
	{
		// Arrange
		var result1 = ExcaliburConfigurationValidationResult.Success();
		var result2 = ExcaliburConfigurationValidationResult.Success();

		// Act
		var combined = ExcaliburConfigurationValidationResult.Combine(result1, result2);

		// Assert
		combined.IsValid.ShouldBeTrue();
		combined.Errors.ShouldBeEmpty();
	}
}
