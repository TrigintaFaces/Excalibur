// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Hosting.Configuration;

namespace Excalibur.Hosting.Tests.Configuration;

/// <summary>
/// Unit tests for <see cref="ConfigurationValidationException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Hosting")]
[Trait("Feature", "Configuration")]
public sealed class ConfigurationValidationExceptionShould : UnitTestBase
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Act
		var exception = new ConfigurationValidationException();

		// Assert
		exception.Message.ShouldBe(string.Empty);
		_ = exception.Errors.ShouldNotBeNull();
		exception.Errors.ShouldBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithMessageOnly()
	{
		// Act
		var exception = new ConfigurationValidationException("Configuration failed");

		// Assert
		exception.Message.ShouldBe("Configuration failed");
		exception.Errors.ShouldBeEmpty();
		exception.InnerException.ShouldBeNull();
	}

	[Fact]
	public void CreateWithNullMessageAsEmpty()
	{
		// Act
		var exception = new ConfigurationValidationException(null as string);

		// Assert
		exception.Message.ShouldBe(string.Empty);
	}

	[Fact]
	public void CreateWithMessageAndErrors()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>
		{
			new("Error 1"),
			new("Error 2")
		};

		// Act
		var exception = new ConfigurationValidationException("Validation failed", errors);

		// Assert
		exception.Message.ShouldBe("Validation failed");
		exception.Errors.Count.ShouldBe(2);
		exception.Errors[0].Message.ShouldBe("Error 1");
		exception.Errors[1].Message.ShouldBe("Error 2");
	}

	[Fact]
	public void CreateWithMessageErrorsAndInnerException()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError> { new("Error") };
		var innerException = new InvalidOperationException("Inner");

		// Act
		var exception = new ConfigurationValidationException("Validation failed", errors, innerException);

		// Assert
		exception.Message.ShouldBe("Validation failed");
		exception.Errors.Count.ShouldBe(1);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void HandleNullErrorsAsEmpty()
	{
		// Act
		var exception = new ConfigurationValidationException("Error", null!);

		// Assert
		_ = exception.Errors.ShouldNotBeNull();
		exception.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void BeOfTypeException()
	{
		// Act
		var exception = new ConfigurationValidationException("Test");

		// Assert
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveReadonlyErrors()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError> { new("Error") };
		var exception = new ConfigurationValidationException("Test", errors);

		// Assert
		_ = exception.Errors.ShouldBeOfType<List<ConfigurationValidationError>>();
	}

	[Fact]
	public void PreserveErrorDetails()
	{
		// Arrange
		var error = new ConfigurationValidationError(
			"Invalid connection",
			"Database:ConnectionString",
			"bad-conn",
			"Use valid format");
		var errors = new List<ConfigurationValidationError> { error };

		// Act
		var exception = new ConfigurationValidationException("Validation failed", errors);

		// Assert
		var resultError = exception.Errors[0];
		resultError.Message.ShouldBe("Invalid connection");
		resultError.ConfigurationPath.ShouldBe("Database:ConnectionString");
		resultError.Value.ShouldBe("bad-conn");
		resultError.Recommendation.ShouldBe("Use valid format");
	}

	[Fact]
	public void BeThrowable()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError> { new("Error") };

		// Act & Assert
		var thrown = Should.Throw<ConfigurationValidationException>(() =>
			throw new ConfigurationValidationException("Test", errors));

		thrown.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void SupportMultipleErrors()
	{
		// Arrange
		var errors = new List<ConfigurationValidationError>
		{
			new("Error 1", "Path:1"),
			new("Error 2", "Path:2"),
			new("Error 3", "Path:3"),
			new("Error 4", "Path:4"),
			new("Error 5", "Path:5")
		};

		// Act
		var exception = new ConfigurationValidationException("Multiple failures", errors);

		// Assert
		exception.Errors.Count.ShouldBe(5);
	}
}
