// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Extended unit tests for <see cref="ConfigurationValidationResult"/> factory methods.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationResultExtendedShould : UnitTestBase
{
	[Fact]
	public void CreateSuccessResult()
	{
		// Act
		var result = ConfigurationValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateFailureWithSingleError()
	{
		// Act
		var result = ConfigurationValidationResult.Failure("Something is wrong", "MyConfig:Section");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].Message.ShouldBe("Something is wrong");
		result.Errors[0].ConfigurationPath.ShouldBe("MyConfig:Section");
	}

	[Fact]
	public void CreateFailureWithMultipleErrors()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>
		{
			new("Error 1", "Path1"),
			new("Error 2", "Path2"),
			new("Error 3"),
		};

		// Act
		var result = ConfigurationValidationResult.Failure(errors);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(3);
	}

	[Fact]
	public void CombineAllSuccessResults()
	{
		// Arrange
		var r1 = ConfigurationValidationResult.Success();
		var r2 = ConfigurationValidationResult.Success();

		// Act
		var combined = ConfigurationValidationResult.Combine(r1, r2);

		// Assert
		combined.IsValid.ShouldBeTrue();
		combined.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CombineWithMixedResults()
	{
		// Arrange
		var success = ConfigurationValidationResult.Success();
		var failure = ConfigurationValidationResult.Failure("Error", "Path");

		// Act
		var combined = ConfigurationValidationResult.Combine(success, failure);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void CombineMultipleFailureResults()
	{
		// Arrange
		var f1 = ConfigurationValidationResult.Failure("Error 1");
		var f2 = ConfigurationValidationResult.Failure("Error 2");

		// Act
		var combined = ConfigurationValidationResult.Combine(f1, f2);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void HandleDefaultErrorsAsEmptyList()
	{
		// Act
		var result = new ConfigurationValidationResult(isValid: true);

		// Assert
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void HandleNullErrorsAsEmptyList()
	{
		// Act
		var result = new ConfigurationValidationResult(isValid: false, errors: null);

		// Assert
		result.Errors.ShouldBeEmpty();
	}
}
