// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Security;

namespace Excalibur.Dispatch.Security.Tests.Security.Validation;

/// <summary>
/// Unit tests for <see cref="InputValidationException"/> class.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Security")]
public sealed class InputValidationExceptionShould
{
	[Fact]
	public void CreateWithDefaultConstructor()
	{
		// Arrange & Act
		var exception = new InputValidationException();

		// Assert
		exception.ShouldNotBeNull();
		exception.Message.ShouldNotBeEmpty();
		exception.ValidationErrors.ShouldNotBeNull();
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateWithMessage()
	{
		// Arrange
		var message = "Input validation failed";

		// Act
		var exception = new InputValidationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateWithMessageAndErrors()
	{
		// Arrange
		var message = "Validation failed";
		var errors = new List<string> { "Field is required", "Value too long" };

		// Act
		var exception = new InputValidationException(message, errors);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ValidationErrors.Count.ShouldBe(2);
		exception.ValidationErrors.ShouldContain("Field is required");
		exception.ValidationErrors.ShouldContain("Value too long");
	}

	[Fact]
	public void CreateWithMessageAndInnerException()
	{
		// Arrange
		var message = "Input validation failed";
		var innerException = new ArgumentException("Invalid argument");

		// Act
		var exception = new InputValidationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void HandleNullErrorsCollection()
	{
		// Arrange
		var message = "Validation failed";
		IEnumerable<string>? errors = null;

		// Act
		var exception = new InputValidationException(message, errors);

		// Assert
		exception.ValidationErrors.ShouldNotBeNull();
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void CreateCopyOfErrorsCollection()
	{
		// Arrange
		var message = "Validation failed";
		var errors = new List<string> { "Error 1" };

		// Act
		var exception = new InputValidationException(message, errors);
		errors.Add("Error 2"); // Modify original list

		// Assert - should not affect exception's errors
		exception.ValidationErrors.Count.ShouldBe(1);
	}

	[Fact]
	public void InheritFromException()
	{
		// Assert
		typeof(InputValidationException).BaseType.ShouldBe(typeof(Exception));
	}

	[Fact]
	public void BeSealed()
	{
		// Assert
		typeof(InputValidationException).IsSealed.ShouldBeTrue();
	}

	[Fact]
	public void BeThrowable()
	{
		// Arrange
		var message = "Test validation error";

		// Act & Assert
		Should.Throw<InputValidationException>(() => throw new InputValidationException(message))
			.Message.ShouldBe(message);
	}

	[Fact]
	public void PreserveValidationErrors_WhenThrown()
	{
		// Arrange
		var errors = new List<string> { "SQL injection detected", "XSS pattern found" };

		// Act & Assert
		var exception = Should.Throw<InputValidationException>(() =>
			throw new InputValidationException("Security validation failed", errors));

		exception.ValidationErrors.Count.ShouldBe(2);
		exception.ValidationErrors.ShouldContain("SQL injection detected");
	}

	[Fact]
	public void HaveReadOnlyValidationErrors()
	{
		// Arrange
		var errors = new List<string> { "Error" };
		var exception = new InputValidationException("Test", errors);

		// Assert
		exception.ValidationErrors.ShouldBeAssignableTo<IReadOnlyList<string>>();
	}
}
