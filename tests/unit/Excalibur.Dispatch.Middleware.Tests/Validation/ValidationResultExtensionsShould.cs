// SPDX-FileCopyrightText: Copyright (c) 2026 The Excalibur Project
// SPDX-License-Identifier: LicenseRef-Excalibur-1.0 OR AGPL-3.0-or-later OR SSPL-1.0 OR Apache-2.0

using Excalibur.Dispatch.Abstractions;
using Excalibur.Dispatch.Abstractions.Serialization;
using Excalibur.Dispatch.Abstractions.Validation;
using Excalibur.Dispatch.Validation.FluentValidation;

using FluentValidation;
using FluentValidation.Results;

using ValidationResult = FluentValidation.Results.ValidationResult;
using DispatchValidationError = Excalibur.Dispatch.Abstractions.Validation.ValidationError;

namespace Excalibur.Dispatch.Middleware.Tests.Validation;

[Trait("Category", "Unit")]
[Trait("Component", "Core")]
public sealed class ValidationResultExtensionsShould
{
	#region Test Messages

	private sealed record ExtTestMessage(string Name, int Age) : IDispatchMessage;

	private sealed record ExtMessageWithEmail(string Name, string Email) : IDispatchMessage;

	#endregion Test Messages

	#region Test Validators

	private sealed class ExtTestMessageValidator : AbstractValidator<ExtTestMessage>
	{
		public ExtTestMessageValidator()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required")
				.MaximumLength(50)
				.WithMessage("Name must not exceed 50 characters");

			_ = RuleFor(x => x.Age)
				.InclusiveBetween(18, 120)
				.WithMessage("Age must be between 18 and 120");
		}
	}

	private sealed class ExtTestMessageValidatorWithErrorCode : AbstractValidator<ExtTestMessage>
	{
		public ExtTestMessageValidatorWithErrorCode()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required")
				.WithErrorCode("ERR_NAME_EMPTY");

			_ = RuleFor(x => x.Age)
				.GreaterThan(0)
				.WithMessage("Age must be positive")
				.WithErrorCode("ERR_AGE_INVALID");
		}
	}

	private sealed class ExtEmailValidator : AbstractValidator<ExtMessageWithEmail>
	{
		public ExtEmailValidator()
		{
			_ = RuleFor(x => x.Name)
				.NotEmpty()
				.WithMessage("Name is required");

			_ = RuleFor(x => x.Email)
				.NotEmpty()
				.WithMessage("Email is required")
				.EmailAddress()
				.WithMessage("Invalid email format");
		}
	}

	#endregion Test Validators

	#region ToDispatchResult Tests - Success Path

	[Fact]
	public void ConvertValidFluentResultToSuccessDispatchResult()
	{
		// Arrange
		var fluentResult = new ValidationResult();

		// Act
		var dispatchResult = fluentResult.ToDispatchResult();

		// Assert
		Assert.NotNull(dispatchResult);
		dispatchResult.IsValid.ShouldBeTrue();
		dispatchResult.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ConvertValidFluentResultWithEmptyErrorsToSuccess()
	{
		// Arrange
		var fluentResult = new ValidationResult(new List<ValidationFailure>());

		// Act
		// Cast to concrete type to avoid IValidationResult generic type argument issues
		var dispatchResult = (SerializableValidationResult)fluentResult.ToDispatchResult();

		// Assert
		dispatchResult.IsValid.ShouldBeTrue();
		dispatchResult.Errors.ShouldBeEmpty();
	}

	#endregion ToDispatchResult Tests - Success Path

	#region ToDispatchResult Tests - Failure Path

	[Fact]
	public void ConvertInvalidFluentResultToFailedDispatchResult()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("Name", "Name is required"),
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		var dispatchResult = fluentResult.ToDispatchResult();

		// Assert
		Assert.NotNull(dispatchResult);
		dispatchResult.IsValid.ShouldBeFalse();
		dispatchResult.Errors.Count.ShouldBe(1);
	}

	[Fact]
	public void PreservePropertyNameInConvertedErrors()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("UserName", "Username is required"),
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		// Cast to concrete type to avoid IValidationResult generic type argument issues
		var dispatchResult = (SerializableValidationResult)fluentResult.ToDispatchResult();

		// Assert
		var error = (DispatchValidationError)dispatchResult.Errors.First();
		error.PropertyName.ShouldBe("UserName");
		error.Message.ShouldBe("Username is required");
	}

	[Fact]
	public void PreserveErrorCodeInConvertedErrors()
	{
		// Arrange
		var failure = new ValidationFailure("Name", "Name is required")
		{
			ErrorCode = "ERR_NAME_REQUIRED",
		};
		var fluentResult = new ValidationResult(new List<ValidationFailure> { failure });

		// Act
		var dispatchResult = (SerializableValidationResult)fluentResult.ToDispatchResult();

		// Assert
		var error = (DispatchValidationError)dispatchResult.Errors.First();
		error.ErrorCode.ShouldBe("ERR_NAME_REQUIRED");
	}

	[Fact]
	public void ConvertMultipleFailuresToMultipleErrors()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("Name", "Name is required"),
			new("Email", "Email is required"),
			new("Age", "Age must be positive"),
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		var dispatchResult = (SerializableValidationResult)fluentResult.ToDispatchResult();

		// Assert
		dispatchResult.IsValid.ShouldBeFalse();
		dispatchResult.Errors.Count.ShouldBe(3);

		var errors = dispatchResult.Errors.Cast<DispatchValidationError>().ToList();
		errors.Select(e => e.PropertyName).ShouldBe(new[] { "Name", "Email", "Age" });
	}

	[Fact]
	public void ReturnSerializableValidationResultType()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("Name", "Name is required"),
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		var dispatchResult = fluentResult.ToDispatchResult();

		// Assert
		Assert.IsType<SerializableValidationResult>(dispatchResult);
	}

	[Fact]
	public void ReturnSerializableValidationResultTypeOnSuccess()
	{
		// Arrange
		var fluentResult = new ValidationResult();

		// Act
		var dispatchResult = fluentResult.ToDispatchResult();

		// Assert
		Assert.IsType<SerializableValidationResult>(dispatchResult);
	}

	#endregion ToDispatchResult Tests - Failure Path

	#region ToDispatchResult Tests - Null Input

	[Fact]
	public void ThrowArgumentNullExceptionWhenFluentResultIsNull()
	{
		// Arrange
		ValidationResult fluentResult = null!;

		// Act & Assert
		_ = Should.Throw<ArgumentNullException>(() => fluentResult.ToDispatchResult());
	}

	#endregion ToDispatchResult Tests - Null Input

	#region ToExcaliburResult Tests

	[Fact]
	public void ReturnSameResultAsToDispatchResult()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("Name", "Name is required"),
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		var dispatchResult = (SerializableValidationResult)fluentResult.ToDispatchResult();
		var excaliburResult = (SerializableValidationResult)fluentResult.ToExcaliburResult();

		// Assert
		excaliburResult.IsValid.ShouldBe(dispatchResult.IsValid);
		excaliburResult.Errors.Count.ShouldBe(dispatchResult.Errors.Count);
	}

	[Fact]
	public void ReturnSuccessFromToExcaliburResultWhenValid()
	{
		// Arrange
		var fluentResult = new ValidationResult();

		// Act
		var result = (SerializableValidationResult)fluentResult.ToExcaliburResult();

		// Assert
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnFailedFromToExcaliburResultWhenInvalid()
	{
		// Arrange
		var failures = new List<ValidationFailure>
		{
			new("Email", "Invalid email format") { ErrorCode = "ERR_EMAIL" },
		};
		var fluentResult = new ValidationResult(failures);

		// Act
		var result = (SerializableValidationResult)fluentResult.ToExcaliburResult();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBe(1);
		var error = (DispatchValidationError)result.Errors.First();
		error.PropertyName.ShouldBe("Email");
		error.ErrorCode.ShouldBe("ERR_EMAIL");
	}

	#endregion ToExcaliburResult Tests

	#region ValidateWith Tests

	[Fact]
	public void ReturnSuccessWhenSyncValidationPasses()
	{
		// Arrange
		var message = new ExtTestMessage("John Doe", 25);

		// Act
		// Cast to SerializableValidationResult to avoid IValidationResult type argument issues
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public void ReturnFailedWhenSyncValidationFails()
	{
		// Arrange
		var message = new ExtTestMessage("", 25);

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		Assert.NotNull(result);
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public void ReturnCorrectErrorDetailsFromSyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 25);

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		var error = (DispatchValidationError)result.Errors.First();
		error.PropertyName.ShouldBe("Name");
		error.Message.ShouldBe("Name is required");
	}

	[Fact]
	public void ReturnMultipleErrorsFromSyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 10);

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public void ReturnErrorCodeFromSyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 0);

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidatorWithErrorCode>();

		// Assert
		result.IsValid.ShouldBeFalse();
		var errors = result.Errors.Cast<DispatchValidationError>().ToList();
		errors.ShouldContain(e => e.ErrorCode == "ERR_NAME_EMPTY");
		errors.ShouldContain(e => e.ErrorCode == "ERR_AGE_INVALID");
	}

	[Theory]
	[InlineData(18)]
	[InlineData(120)]
	public void ReturnSuccessFromSyncValidationAtBoundaryValues(int age)
	{
		// Arrange
		var message = new ExtTestMessage("John", age);

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnSerializableValidationResultFromSyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("John", 25);

		// Act
		var result = message.ValidateWith<ExtTestMessage, ExtTestMessageValidator>();

		// Assert
		Assert.IsType<SerializableValidationResult>(result);
	}

	#endregion ValidateWith Tests

	#region ValidateWithAsync Tests

	[Fact]
	public async Task ReturnSuccessWhenAsyncValidationPasses()
	{
		// Arrange
		var message = new ExtTestMessage("John Doe", 25);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.IsValid.ShouldBeTrue();
		result.Errors.ShouldBeEmpty();
	}

	[Fact]
	public async Task ReturnFailedWhenAsyncValidationFails()
	{
		// Arrange
		var message = new ExtTestMessage("", 25);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.ShouldNotBeNull();
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThan(0);
	}

	[Fact]
	public async Task ReturnCorrectErrorDetailsFromAsyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 25);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		var error = (DispatchValidationError)result.Errors.First();
		error.PropertyName.ShouldBe("Name");
		error.Message.ShouldBe("Name is required");
	}

	[Fact]
	public async Task ReturnMultipleErrorsFromAsyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 10);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		result.Errors.Count.ShouldBeGreaterThanOrEqualTo(2);
	}

	[Fact]
	public async Task ReturnErrorCodesFromAsyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("", 0);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidatorWithErrorCode>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeFalse();
		var errors = result.Errors.Cast<DispatchValidationError>().ToList();
		errors.ShouldContain(e => e.ErrorCode == "ERR_NAME_EMPTY");
		errors.ShouldContain(e => e.ErrorCode == "ERR_AGE_INVALID");
	}

	[Fact]
	public async Task ReturnSerializableValidationResultTypeFromAsync()
	{
		// Arrange
		var message = new ExtTestMessage("John", 25);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		_ = result.ShouldBeOfType<SerializableValidationResult>();
	}

	[Theory]
	[InlineData(18)]
	[InlineData(120)]
	public async Task ReturnSuccessFromAsyncValidationAtBoundaryValues(int age)
	{
		// Arrange
		var message = new ExtTestMessage("John", age);

		// Act
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(CancellationToken.None).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public async Task HandleCancellationTokenInAsyncValidation()
	{
		// Arrange
		var message = new ExtTestMessage("John", 25);
		using var cts = new CancellationTokenSource();

		// Act - should complete without cancellation
		var result = await message.ValidateWithAsync<ExtTestMessage, ExtTestMessageValidator>(cts.Token).ConfigureAwait(false);

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	#endregion ValidateWithAsync Tests

	#region ValidateWith Tests - Email Validator

	[Fact]
	public void ReturnSuccessWhenEmailIsValid()
	{
		// Arrange
		var message = new ExtMessageWithEmail("John", "john@example.com");

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtMessageWithEmail, ExtEmailValidator>();

		// Assert
		result.IsValid.ShouldBeTrue();
	}

	[Fact]
	public void ReturnFailedWhenEmailIsInvalid()
	{
		// Arrange
		var message = new ExtMessageWithEmail("John", "not-an-email");

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtMessageWithEmail, ExtEmailValidator>();

		// Assert
		result.IsValid.ShouldBeFalse();
		var error = (DispatchValidationError)result.Errors.First();
		error.PropertyName.ShouldBe("Email");
	}

	[Fact]
	public void ReturnFailedWhenEmailIsEmpty()
	{
		// Arrange
		var message = new ExtMessageWithEmail("John", "");

		// Act
		var result = (SerializableValidationResult)message.ValidateWith<ExtMessageWithEmail, ExtEmailValidator>();

		// Assert
		result.IsValid.ShouldBeFalse();
	}

	#endregion ValidateWith Tests - Email Validator
}
