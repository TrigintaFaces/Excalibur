// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationResult"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationResultShould : UnitTestBase
{
	[Fact]
	public void CreateValidResultViaConstructor()
	{
		// Act
		var result = new ConfigurationValidationResult(isValid: true);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void CreateInvalidResultViaConstructor()
	{
		// Act
		var result = new ConfigurationValidationResult(isValid: false);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void HaveEmptyErrorsByDefault()
	{
		// Act
		var result = new ConfigurationValidationResult(isValid: true);

		// Assert
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void StoreErrorsFromConstructor()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>
		{
			new("Error 1"),
			new("Error 2")
		};

		// Act
		var result = new ConfigurationValidationResult(isValid: false, errors);

		// Assert
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].Message.ShouldBe("Error 1");
		result.Errors[1].Message.ShouldBe("Error 2");
	}

	[Fact]
	public void CreateSuccessResultViaStaticMethod()
	{
		// Act
		var result = ConfigurationValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateFailureResultWithMessage()
	{
		// Act
		var result = ConfigurationValidationResult.Failure("Something went wrong");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].Message.ShouldBe("Something went wrong");
	}

	[Fact]
	public void CreateFailureResultWithMessageAndPath()
	{
		// Act
		var result = ConfigurationValidationResult.Failure(
			"Invalid connection string",
			"Database:ConnectionString");

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].Message.ShouldBe("Invalid connection string");
		result.Errors[0].ConfigurationPath.ShouldBe("Database:ConnectionString");
	}

	[Fact]
	public void CreateFailureResultWithErrorList()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>
		{
			new("Error A", "Path:A"),
			new("Error B", "Path:B"),
			new("Error C", "Path:C")
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
		var result1 = ConfigurationValidationResult.Success();
		var result2 = ConfigurationValidationResult.Success();
		var result3 = ConfigurationValidationResult.Success();

		// Act
		var combined = ConfigurationValidationResult.Combine(result1, result2, result3);

		// Assert
		combined.IsValid.ShouldBeTrue();
		combined.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CombineWithOneFailure()
	{
		// Arrange
		var result1 = ConfigurationValidationResult.Success();
		var result2 = ConfigurationValidationResult.Failure("Error");
		var result3 = ConfigurationValidationResult.Success();

		// Act
		var combined = ConfigurationValidationResult.Combine(result1, result2, result3);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(1);
		combined.Errors[0].Message.ShouldBe("Error");
	}

	[Fact]
	public void CombineMultipleFailures()
	{
		// Arrange
		var result1 = ConfigurationValidationResult.Failure("Error 1");
		var result2 = ConfigurationValidationResult.Failure("Error 2");
		var result3 = ConfigurationValidationResult.Failure("Error 3");

		// Act
		var combined = ConfigurationValidationResult.Combine(result1, result2, result3);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(3);
	}

	[Fact]
	public void CombineWithMultipleErrorsPerResult()
	{
		// Arrange
		var errors1 = new List<ConfigurationValidationError>
		{
			new("Error 1"),
			new("Error 2")
		};
		var errors2 = new List<ConfigurationValidationError>
		{
			new("Error 3")
		};
		var result1 = ConfigurationValidationResult.Failure(errors1);
		var result2 = ConfigurationValidationResult.Failure(errors2);

		// Act
		var combined = ConfigurationValidationResult.Combine(result1, result2);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(3);
	}

	[Fact]
	public void CombineEmptyResultsArray()
	{
		// Act
		var combined = ConfigurationValidationResult.Combine();

		// Assert
		combined.IsValid.ShouldBeTrue();
		combined.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void CombineSingleSuccessResult()
	{
		// Arrange
		var result = ConfigurationValidationResult.Success();

		// Act
		var combined = ConfigurationValidationResult.Combine(result);

		// Assert
		combined.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void CombineSingleFailureResult()
	{
		// Arrange
		var result = ConfigurationValidationResult.Failure("Error");

		// Act
		var combined = ConfigurationValidationResult.Combine(result);

		// Assert
		combined.IsValid.ShouldBeFalse();
		combined.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void PreserveErrorDetailsWhenCombining()
	{
		// Arrange
		var error1 = new ConfigurationValidationError("Error 1", "Path1", "value1", "Fix 1");
		var error2 = new ConfigurationValidationError("Error 2", "Path2", "value2", "Fix 2");
		var result1 = ConfigurationValidationResult.Failure(new List<ConfigurationValidationError> { error1 });
		var result2 = ConfigurationValidationResult.Failure(new List<ConfigurationValidationError> { error2 });

		// Act
		var combined = ConfigurationValidationResult.Combine(result1, result2);

		// Assert
		combined.Errors[0].Message.ShouldBe("Error 1");
		combined.Errors[0].ConfigurationPath.ShouldBe("Path1");
		combined.Errors[0].Value.ShouldBe("value1");
		combined.Errors[0].Recommendation.ShouldBe("Fix 1");
		combined.Errors[1].Message.ShouldBe("Error 2");
		combined.Errors[1].ConfigurationPath.ShouldBe("Path2");
	}
}
