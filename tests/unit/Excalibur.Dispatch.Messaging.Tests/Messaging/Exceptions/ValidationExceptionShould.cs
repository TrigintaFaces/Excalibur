// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Exceptions;

namespace Excalibur.Dispatch.Tests.Messaging.Exceptions;

/// <summary>
/// Unit tests for <see cref="ValidationException"/>.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationExceptionShould
{
	[Fact]
	public void InheritFromDispatchException()
	{
		// Arrange & Act
		var exception = new ValidationException();

		// Assert
		_ = exception.ShouldBeAssignableTo<DispatchException>();
		_ = exception.ShouldBeAssignableTo<ApiException>();
		_ = exception.ShouldBeAssignableTo<Exception>();
	}

	[Fact]
	public void HaveDefaultConstructor()
	{
		// Arrange & Act
		var exception = new ValidationException();

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		exception.DispatchStatusCode.ShouldBe(400);
		exception.ValidationErrors.ShouldNotBeNull();
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptMessage()
	{
		// Arrange
		const string message = "Custom validation error";

		// Act
		var exception = new ValidationException(message);

		// Assert
		exception.Message.ShouldBe(message);
		exception.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		exception.DispatchStatusCode.ShouldBe(400);
		exception.ValidationErrors.ShouldBeEmpty();
	}

	[Fact]
	public void AcceptValidationErrors()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>
		{
			["Name"] = ["Name is required", "Name is too short"],
			["Email"] = ["Email is invalid"],
		};

		// Act
		var exception = new ValidationException(errors);

		// Assert
		exception.ErrorCode.ShouldBe(ErrorCodes.ValidationFailed);
		exception.DispatchStatusCode.ShouldBe(400);
		exception.ValidationErrors.ShouldContainKey("Name");
		exception.ValidationErrors["Name"].Length.ShouldBe(2);
		exception.ValidationErrors["Email"].ShouldContain("Email is invalid");
	}

	[Fact]
	public void AcceptMessageAndInnerException()
	{
		// Arrange
		var innerException = new InvalidOperationException("Inner error");
		const string message = "Validation error";

		// Act
		var exception = new ValidationException(message, innerException);

		// Assert
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void AcceptErrorCodeAndMessage()
	{
		// Arrange
		const string errorCode = "CUSTOM_VALIDATION";
		const string message = "Custom validation error";

		// Act
		var exception = new ValidationException(errorCode, message);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
	}

	[Fact]
	public void AcceptErrorCodeMessageAndInnerException()
	{
		// Arrange
		const string errorCode = "CUSTOM_VALIDATION";
		const string message = "Custom validation error";
		var innerException = new ArgumentNullException("param");

		// Act
		var exception = new ValidationException(errorCode, message, innerException);

		// Assert
		exception.ErrorCode.ShouldBe(errorCode);
		exception.Message.ShouldBe(message);
		exception.InnerException.ShouldBe(innerException);
	}

	[Fact]
	public void FormatMessageFromErrors()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>
		{
			["Field1"] = ["Error 1"],
			["Field2"] = ["Error 2"],
		};

		// Act
		var exception = new ValidationException(errors);

		// Assert
		exception.Message.ShouldContain("Field1");
		exception.Message.ShouldContain("Error 1");
		exception.Message.ShouldContain("Field2");
		exception.Message.ShouldContain("Error 2");
	}

	[Fact]
	public void HaveDefaultMessageForEmptyErrors()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>();

		// Act
		var exception = new ValidationException(errors);

		// Assert
		exception.Message.ShouldBe("Validation failed.");
	}

	[Fact]
	public void CreateRequiredFieldException()
	{
		// Arrange
		const string fieldName = "Username";

		// Act
		var exception = ValidationException.RequiredField(fieldName);

		// Assert
		exception.ValidationErrors.ShouldContainKey(fieldName);
		exception.ValidationErrors[fieldName].ShouldContain(s => s.Contains("required", StringComparison.OrdinalIgnoreCase));
		exception.UserMessage.ShouldContain(fieldName);
		exception.SuggestedAction.ShouldContain(fieldName);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.ValidationRequiredFieldMissing);
	}

	[Fact]
	public void CreateInvalidFormatException()
	{
		// Arrange
		const string fieldName = "Email";
		const string expectedFormat = "user@example.com";

		// Act
		var exception = ValidationException.InvalidFormat(fieldName, expectedFormat);

		// Assert
		exception.ValidationErrors.ShouldContainKey(fieldName);
		exception.ValidationErrors[fieldName].ShouldContain(s => s.Contains("invalid format", StringComparison.OrdinalIgnoreCase));
		exception.ValidationErrors[fieldName].ShouldContain(s => s.Contains(expectedFormat, StringComparison.Ordinal));
		exception.UserMessage.ShouldContain(fieldName);
		exception.SuggestedAction.ShouldContain(expectedFormat);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.ValidationInvalidFormat);
	}

	[Fact]
	public void CreateOutOfRangeException()
	{
		// Arrange
		const string fieldName = "Age";
		const int min = 18;
		const int max = 120;

		// Act
		var exception = ValidationException.OutOfRange(fieldName, min, max);

		// Assert
		exception.ValidationErrors.ShouldContainKey(fieldName);
		exception.ValidationErrors[fieldName].ShouldContain(s => s.Contains(min.ToString(), StringComparison.Ordinal));
		exception.ValidationErrors[fieldName].ShouldContain(s => s.Contains(max.ToString(), StringComparison.Ordinal));
		exception.Context.ShouldContainKeyAndValue("min", min);
		exception.Context.ShouldContainKeyAndValue("max", max);
		exception.UserMessage.ShouldContain(fieldName);
		exception.Data["ErrorCode"].ShouldBe(ErrorCodes.ValidationOutOfRange);
	}

	[Fact]
	public void AddErrorToExistingField()
	{
		// Arrange
		var exception = new ValidationException();
		exception.AddError("Name", "Error 1");

		// Act
		exception.AddError("Name", "Error 2");

		// Assert
		exception.ValidationErrors["Name"].Length.ShouldBe(2);
		exception.ValidationErrors["Name"].ShouldContain("Error 1");
		exception.ValidationErrors["Name"].ShouldContain("Error 2");
	}

	[Fact]
	public void AddErrorToNewField()
	{
		// Arrange
		var exception = new ValidationException();

		// Act
		exception.AddError("Email", "Email is required");

		// Assert
		exception.ValidationErrors.ShouldContainKey("Email");
		exception.ValidationErrors["Email"].ShouldContain("Email is required");
	}

	[Fact]
	public void SupportFluentErrorAddition()
	{
		// Arrange & Act
		var exception = new ValidationException()
			.AddError("Name", "Name is required")
			.AddError("Email", "Email is invalid")
			.AddError("Name", "Name is too short");

		// Assert
		exception.ValidationErrors.Count.ShouldBe(2);
		exception.ValidationErrors["Name"].Length.ShouldBe(2);
		exception.ValidationErrors["Email"].Length.ShouldBe(1);
	}

	[Fact]
	public void ConvertToDispatchProblemDetails()
	{
		// Arrange
		var errors = new Dictionary<string, string[]>
		{
			["Field1"] = ["Error message"],
		};
		var exception = new ValidationException(errors);

		// Act
		var problemDetails = exception.ToDispatchProblemDetails();

		// Assert
		problemDetails.ShouldNotBeNull();
		problemDetails.Extensions.ShouldContainKey("errors");
		var errorsExtension = problemDetails.Extensions["errors"] as IDictionary<string, string[]>;
		errorsExtension.ShouldNotBeNull();
		errorsExtension.ShouldContainKey("Field1");
	}

	[Fact]
	public void ConvertToBaseProblemDetailsWhenNoErrors()
	{
		// Arrange
		var exception = new ValidationException("No errors");

		// Act
		var problemDetails = exception.ToDispatchProblemDetails();

		// Assert
		problemDetails.ShouldNotBeNull();
		// When no validation errors, should not add errors extension
		problemDetails.Extensions.ShouldNotContainKey("errors");
	}

	[Fact]
	public void HaveSerializableAttribute()
	{
		// Assert
		typeof(ValidationException)
			.GetCustomAttributes(typeof(SerializableAttribute), false)
			.ShouldNotBeEmpty();
	}

	[Fact]
	public void UseOrdinalStringComparerForErrors()
	{
		// Arrange - Testing case sensitivity
		var errors = new Dictionary<string, string[]>
		{
			["name"] = ["lowercase error"],
		};
		var exception = new ValidationException(errors);

		// Act & Assert
		exception.ValidationErrors.ShouldContainKey("name");
		exception.ValidationErrors.ShouldNotContainKey("Name"); // Case-sensitive
	}

	[Fact]
	public void CopyErrorsFromInputDictionary()
	{
		// Arrange
		var originalErrors = new Dictionary<string, string[]>
		{
			["Field"] = ["Error"],
		};

		// Act
		var exception = new ValidationException(originalErrors);

		// Modify original
		originalErrors["Field"] = ["Modified"];

		// Assert - Should have copied, not referenced
		exception.ValidationErrors["Field"].ShouldContain("Error");
	}
}
