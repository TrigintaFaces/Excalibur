// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Middleware;

namespace Excalibur.Dispatch.Tests.Messaging.Middleware;

/// <summary>
/// Unit tests for <see cref="MessageValidationResult"/>.
/// </summary>
/// <remarks>
/// Tests the validation result class with Success and Failure factory methods.
/// </remarks>
[Trait("Category", "Unit")]
[Trait("Component", "Middleware")]
[Trait("Priority", "0")]
public sealed class MessageValidationResultShould
{
	#region Success Factory Method Tests

	[Fact]
	public void Success_ReturnsValidResult()
	{
		// Act
		var result = MessageValidationResult.Success();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Success_ReturnsEmptyErrors()
	{
		// Act
		var result = MessageValidationResult.Success();

		// Assert
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Success_MultipleCallsReturnIndependentInstances()
	{
		// Act
		var result1 = MessageValidationResult.Success();
		var result2 = MessageValidationResult.Success();

		// Assert
		result1.ShouldNotBeSameAs(result2);
	}

	#endregion

	#region Failure Factory Method Tests

	[Fact]
	public void Failure_ReturnsInvalidResult()
	{
		// Arrange
		var error = new ValidationError("Field", "Field is required");

		// Act
		var result = MessageValidationResult.Failure(error);

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Failure_ContainsProvidedErrors()
	{
		// Arrange
		var error1 = new ValidationError("Name", "Name is required");
		var error2 = new ValidationError("Email", "Invalid email format");

		// Act
		var result = MessageValidationResult.Failure(error1, error2);

		// Assert
		result.Errors.Count.ShouldBe(2);
		result.Errors[0].PropertyName.ShouldBe("Name");
		result.Errors[1].PropertyName.ShouldBe("Email");
	}

	[Fact]
	public void Failure_WithNoErrors_ReturnsInvalidWithEmptyList()
	{
		// Act
		var result = MessageValidationResult.Failure();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Failure_WithSingleError_PreservesError()
	{
		// Arrange
		var error = new ValidationError("Amount", "Amount must be positive");

		// Act
		var result = MessageValidationResult.Failure(error);

		// Assert
		result.Errors.Count.ShouldBe(1);
		result.Errors[0].PropertyName.ShouldBe("Amount");
		result.Errors[0].ErrorMessage.ShouldBe("Amount must be positive");
	}

	#endregion

	#region Constructor Tests

	[Fact]
	public void Constructor_WithValidTrue_SetsIsValidTrue()
	{
		// Act
		var result = new MessageValidationResult(true, Array.Empty<ValidationError>());

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void Constructor_WithValidFalse_SetsIsValidFalse()
	{
		// Act
		var result = new MessageValidationResult(false, Array.Empty<ValidationError>());

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	[Fact]
	public void Constructor_WithNullErrors_CreatesEmptyList()
	{
		// Act
		var result = new MessageValidationResult(true, null!);

		// Assert
		_ = result.Errors.ShouldNotBeNull();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void Constructor_WithErrors_CreatesReadOnlyList()
	{
		// Arrange
		var errors = new List<ValidationError>
		{
			new("Field1", "Error 1"),
			new("Field2", "Error 2"),
		};

		// Act
		var result = new MessageValidationResult(false, errors);

		// Assert
		_ = result.Errors.ShouldBeAssignableTo<IReadOnlyList<ValidationError>>();
		result.Errors.Count.ShouldBe(2);
	}

	[Fact]
	public void Constructor_WithErrors_CopiesErrorsNotReferenceOriginal()
	{
		// Arrange
		var errors = new List<ValidationError>
		{
			new("Field1", "Error 1"),
		};

		// Act
		var result = new MessageValidationResult(false, errors);

		// Modify original list
		errors.Add(new ValidationError("Field2", "Error 2"));

		// Assert - Result should not be affected
		result.Errors.Count.ShouldBe(1);
	}

	#endregion

	#region Property Immutability Tests

	[Fact]
	public void IsValid_IsReadOnly()
	{
		// Assert - Property has no setter
		var propertyInfo = typeof(MessageValidationResult).GetProperty(nameof(MessageValidationResult.IsValid));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	[Fact]
	public void Errors_IsReadOnly()
	{
		// Assert - Property has no setter
		var propertyInfo = typeof(MessageValidationResult).GetProperty(nameof(MessageValidationResult.Errors));
		_ = propertyInfo.ShouldNotBeNull();
		propertyInfo.CanWrite.ShouldBeFalse();
	}

	#endregion

	#region Usage Scenario Tests

	[Fact]
	public void CanBeUsedInValidationPipeline()
	{
		// Arrange
		var validResult = MessageValidationResult.Success();
		var invalidResult = MessageValidationResult.Failure(
			new ValidationError("Email", "Invalid format"),
			new ValidationError("Password", "Too short"));

		// Act & Assert
		if (validResult.IsValid)
		{
			validResult.Errors.Count.ShouldBe(0);
		}
		else
		{
			Assert.Fail("Valid result should be valid");
		}

		if (!invalidResult.IsValid)
		{
			invalidResult.Errors.Count.ShouldBe(2);
		}
		else
		{
			Assert.Fail("Invalid result should not be valid");
		}
	}

	[Fact]
	public void SuccessAndFailure_AreMutuallyExclusive()
	{
		// Arrange
		var success = MessageValidationResult.Success();
		var failure = MessageValidationResult.Failure(new ValidationError("test", "error"));

		// Assert
		success.IsValid.ShouldNotBe(failure.IsValid);
	}

	[Fact]
	public void CanIterateOverErrors()
	{
		// Arrange
		var result = MessageValidationResult.Failure(
			new ValidationError("Field1", "Error1"),
			new ValidationError("Field2", "Error2"),
			new ValidationError("Field3", "Error3"));

		// Act
		var errorMessages = new List<string>();
		foreach (var error in result.Errors)
		{
			errorMessages.Add(error.ErrorMessage);
		}

		// Assert
		errorMessages.Count.ShouldBe(3);
		errorMessages.ShouldContain("Error1");
		errorMessages.ShouldContain("Error2");
		errorMessages.ShouldContain("Error3");
	}

	[Fact]
	public void CanAccessErrorsByIndex()
	{
		// Arrange
		var result = MessageValidationResult.Failure(
			new ValidationError("First", "First error"),
			new ValidationError("Second", "Second error"));

		// Act & Assert
		result.Errors[0].PropertyName.ShouldBe("First");
		result.Errors[1].PropertyName.ShouldBe("Second");
	}

	#endregion
}
